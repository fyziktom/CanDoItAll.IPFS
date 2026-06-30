using System.Globalization;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeExplorerWorkflowService(
    INodeFileWorkflow fileWorkflow,
    IExplorerIndexStore explorerIndexStore) : INodeExplorerWorkflow
{
    private const string VirtualExplorerPrefix = "/virtual";
    private const string VirtualUnsortedPath = "/virtual/unsorted";

    public IReadOnlyList<NodeExplorerItemSnapshot> GetCachedPinnedExplorerItems()
        => OrderExplorerItems(explorerIndexStore.ListPinnedRoots().Select(CreateExplorerItemSnapshot));

    public async Task<bool> HasTrustedCachedPinnedExplorerItemsAsync(CancellationToken cancellationToken)
    {
        var cachedRoots = explorerIndexStore.ListPinnedRoots();
        if (cachedRoots.Count == 0)
        {
            return false;
        }

        var pinnedTargetSet = (await fileWorkflow.ListPinsAsync(cancellationToken).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        if (cachedRoots.Any(root => !pinnedTargetSet.Contains(root.Target)))
        {
            explorerIndexStore.MarkMissingPinnedRootsAsUnpinned(pinnedTargetSet);
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<NodeExplorerItemSnapshot>> ListPinnedExplorerItemsAsync(CancellationToken cancellationToken)
    {
        await RefreshExplorerIndexAsync(cancellationToken).ConfigureAwait(false);
        return GetCachedPinnedExplorerItems();
    }

    public async Task<NodeExplorerSnapshot> GetExplorerSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        if (TryBuildVirtualExplorerSnapshot(path, out var virtualSnapshot))
        {
            return virtualSnapshot;
        }

        var current = await fileWorkflow.InspectFileSystemAsync(path, cancellationToken).ConfigureAwait(false);
        var normalizedPath = NormalizeBrowsePath(current.RequestedPath);
        var entries = current.IsDirectory
            ? BuildExplorerEntries(normalizedPath, current.Links)
            : [];

        return new NodeExplorerSnapshot
        {
            Current = current,
            NormalizedPath = normalizedPath,
            ParentPath = TryGetParentPath(normalizedPath),
            Breadcrumbs = BuildBreadcrumbs(normalizedPath),
            Entries = entries
        };
    }

    public async Task<NodePreviewSnapshot> GetPreviewSnapshotAsync(string path, string? displayName, CancellationToken cancellationToken)
    {
        if (TryBuildVirtualPreviewSnapshot(path, displayName, out var virtualPreview))
        {
            return virtualPreview;
        }

        var snapshot = await fileWorkflow.InspectFileSystemAsync(path, cancellationToken).ConfigureAwait(false);
        var normalizedPath = NormalizeBrowsePath(snapshot.RequestedPath);
        var safeDisplayName = NodeOperatorDisplay.ResolveDisplayName(
            displayName,
            NodeOperatorDisplay.ResolveDisplayName(normalizedPath, snapshot.ResolvedId));

        return new NodePreviewSnapshot
        {
            DisplayName = safeDisplayName,
            Target = snapshot.ResolvedId,
            Path = normalizedPath,
            IsDirectory = snapshot.IsDirectory,
            TypeLabel = NodeOperatorDisplay.GetTypeLabel(snapshot.IsDirectory),
            Size = snapshot.Size,
            ChildCount = snapshot.IsDirectory ? snapshot.Links.Count : 0,
            PreviewText = snapshot.IsDirectory
                ? string.Empty
                : await fileWorkflow.ReadFilePreviewAsync(snapshot.ResolvedId, 65536, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task RefreshExplorerIndexAsync(CancellationToken cancellationToken)
    {
        var pinned = await fileWorkflow.ListPinsAsync(cancellationToken).ConfigureAwait(false);
        var indexedPinned = pinned
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var indexedPinnedSet = indexedPinned.ToHashSet(StringComparer.Ordinal);

        // Do not traverse cached roots that are no longer pinned by the live node.
        explorerIndexStore.MarkMissingPinnedRootsAsUnpinned(indexedPinned);

        var indexedRootsByTarget = explorerIndexStore.ListPinnedRoots()
            .Where(root => indexedPinnedSet.Contains(root.Target))
            .ToDictionary(root => root.Target, StringComparer.Ordinal);
        var missingSnapshots = new Dictionary<string, NodeFileSnapshot>(StringComparer.Ordinal);

        foreach (var cid in indexedPinned)
        {
            if (indexedRootsByTarget.ContainsKey(cid))
            {
                continue;
            }

            missingSnapshots[cid] = await fileWorkflow.InspectFileSystemAsync(cid, cancellationToken).ConfigureAwait(false);
        }

        var directoryRoots = indexedRootsByTarget.Values
            .Where(root => root.IsDirectory)
            .Select(root => root.Target)
            .Concat(missingSnapshots.Values
                .Where(snapshot => snapshot.IsDirectory)
                .Select(snapshot => snapshot.ResolvedId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var hiddenPinnedTargets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var directoryRoot in directoryRoots)
        {
            var descendants = await GetDescendantPinnedTargetsAsync(
                directoryRoot,
                indexedPinned,
                missingSnapshots,
                cancellationToken).ConfigureAwait(false);

            hiddenPinnedTargets.UnionWith(descendants);
        }

        var visiblePinned = indexedPinned
            .Where(cid => !hiddenPinnedTargets.Contains(cid))
            .ToArray();
        var seenAtUtc = DateTimeOffset.UtcNow;

        foreach (var cid in visiblePinned)
        {
            if (indexedRootsByTarget.ContainsKey(cid))
            {
                continue;
            }

            var snapshot = missingSnapshots[cid];
            RememberIndexedRoot(snapshot, NodeOperatorDisplay.ResolveDisplayName(cid, snapshot.ResolvedId), isPinned: true, firstPinnedAtUtc: seenAtUtc);
        }

        explorerIndexStore.MarkPinnedRootsSeen(visiblePinned, seenAtUtc);
        explorerIndexStore.MarkMissingPinnedRootsAsUnpinned(visiblePinned);
    }

    private async Task<IReadOnlyCollection<string>> GetDescendantPinnedTargetsAsync(
        string directoryRoot,
        IReadOnlyCollection<string> pinnedTargets,
        IReadOnlyDictionary<string, NodeFileSnapshot> snapshotCache,
        CancellationToken cancellationToken)
    {
        var pinnedTargetSet = pinnedTargets is HashSet<string> hashSet
            ? hashSet
            : pinnedTargets.ToHashSet(StringComparer.Ordinal);
        var descendants = new HashSet<string>(StringComparer.Ordinal);
        var visitedDirectories = new HashSet<string>(StringComparer.Ordinal);
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directoryRoot);

        while (pendingDirectories.Count > 0)
        {
            var current = pendingDirectories.Pop();
            if (!visitedDirectories.Add(current))
            {
                continue;
            }

            var snapshot = snapshotCache.TryGetValue(current, out var cachedSnapshot)
                ? cachedSnapshot
                : await fileWorkflow.InspectFileSystemAsync(current, cancellationToken).ConfigureAwait(false);
            if (!snapshot.IsDirectory)
            {
                continue;
            }

            foreach (var link in snapshot.Links)
            {
                if (!string.Equals(link.Target, directoryRoot, StringComparison.Ordinal)
                    && pinnedTargetSet.Contains(link.Target))
                {
                    descendants.Add(link.Target);
                }

                if (link.IsDirectory)
                {
                    pendingDirectories.Push(link.Target);
                }
            }
        }

        return descendants;
    }

    private void RememberIndexedRoot(
        NodeFileSnapshot snapshot,
        string? displayName,
        bool isPinned,
        DateTimeOffset? firstPinnedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var now = DateTimeOffset.UtcNow;
        explorerIndexStore.UpsertRoot(new ExplorerIndexedRootRecord(
            snapshot.ResolvedId,
            NodeOperatorDisplay.ResolveDisplayName(displayName, snapshot.ResolvedId),
            snapshot.IsDirectory,
            snapshot.Size,
            snapshot.IsDirectory ? snapshot.Links.Count : 0,
            firstPinnedAtUtc ?? now,
            now,
            now,
            isPinned));
    }

    private static IReadOnlyList<ExplorerIndexedRootRecord> GetVirtualUnsortedSourceRoots(
        IReadOnlyList<ExplorerIndexedRootRecord> roots)
        => roots
            .Where(root => !root.IsDirectory)
            .ToList();

    private bool TryBuildVirtualExplorerSnapshot(string path, out NodeExplorerSnapshot snapshot)
    {
        snapshot = null!;
        if (!TryParseVirtualUnsortedPath(path, out var year, out var month))
        {
            return false;
        }

        var roots = GetVirtualUnsortedSourceRoots(explorerIndexStore.ListPinnedRoots());
        var normalizedPath = BuildVirtualUnsortedPath(year, month);
        var entries = BuildVirtualExplorerEntries(roots, year, month);
        var currentSize = GetVirtualAggregateSize(roots, year, month);
        snapshot = new NodeExplorerSnapshot
        {
            Current = new NodeFileSnapshot
            {
                RequestedPath = normalizedPath,
                ResolvedId = normalizedPath,
                IsDirectory = true,
                Size = currentSize,
                Links = []
            },
            NormalizedPath = normalizedPath,
            ParentPath = TryGetParentPath(normalizedPath),
            Breadcrumbs = BuildBreadcrumbs(normalizedPath),
            Entries = entries
        };
        return true;
    }

    private bool TryBuildVirtualPreviewSnapshot(string path, string? displayName, out NodePreviewSnapshot previewSnapshot)
    {
        previewSnapshot = null!;
        if (!TryBuildVirtualExplorerSnapshot(path, out var explorerSnapshot))
        {
            return false;
        }

        previewSnapshot = new NodePreviewSnapshot
        {
            DisplayName = NodeOperatorDisplay.ResolveDisplayName(displayName, GetVirtualFolderDisplayName(explorerSnapshot.NormalizedPath)),
            Target = explorerSnapshot.NormalizedPath,
            Path = explorerSnapshot.NormalizedPath,
            IsDirectory = true,
            TypeLabel = "Virtual folder",
            Size = explorerSnapshot.Current.Size,
            ChildCount = explorerSnapshot.Entries.Count,
            PreviewText = string.Empty
        };
        return true;
    }

    private static IReadOnlyList<NodeExplorerItemSnapshot> BuildVirtualExplorerEntries(
        IReadOnlyList<ExplorerIndexedRootRecord> roots,
        int? year,
        int? month)
    {
        if (month.HasValue)
        {
            return OrderExplorerItems(
                roots.Where(root =>
                        root.FirstPinnedAtUtc.Year == year
                        && root.FirstPinnedAtUtc.Month == month)
                    .Select(CreateExplorerItemSnapshot));
        }

        if (year.HasValue)
        {
            return roots.Where(root => root.FirstPinnedAtUtc.Year == year)
                .GroupBy(root => root.FirstPinnedAtUtc.Month)
                .OrderByDescending(group => group.Key)
                .Select(group =>
                {
                    var totalSize = group.Sum(root => root.Size);
                    return CreateVirtualFolderItem(
                        CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(group.Key),
                        BuildVirtualUnsortedPath(year, group.Key),
                        totalSize,
                        group.Count());
                })
                .ToList();
        }

        return roots.GroupBy(root => root.FirstPinnedAtUtc.Year)
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var totalSize = group.Sum(root => root.Size);
                return CreateVirtualFolderItem(
                    group.Key.ToString(CultureInfo.InvariantCulture),
                    BuildVirtualUnsortedPath(group.Key, null),
                    totalSize,
                    group.Count());
            })
            .ToList();
    }

    private static long GetVirtualAggregateSize(
        IReadOnlyList<ExplorerIndexedRootRecord> roots,
        int? year,
        int? month)
    {
        IEnumerable<ExplorerIndexedRootRecord> filtered = roots;
        if (year.HasValue)
        {
            filtered = filtered.Where(root => root.FirstPinnedAtUtc.Year == year);
        }

        if (month.HasValue)
        {
            filtered = filtered.Where(root => root.FirstPinnedAtUtc.Month == month);
        }

        return filtered.Sum(root => root.Size);
    }

    private static NodeExplorerItemSnapshot CreateExplorerItemSnapshot(ExplorerIndexedRootRecord record)
        => new(
            NodeOperatorDisplay.ResolveDisplayName(record.DisplayName, record.Target),
            NormalizeBrowsePath(record.Target),
            record.Target,
            record.IsDirectory,
            NodeOperatorDisplay.GetTypeLabel(record.IsDirectory),
            record.Size,
            record.ChildCount);

    private static NodeExplorerItemSnapshot CreateVirtualFolderItem(
        string displayName,
        string path,
        long size,
        int childCount)
        => new(
            displayName,
            path,
            path,
            true,
            "Virtual folder",
            size,
            childCount);

    private static bool IsVirtualExplorerPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
           && path.Trim().StartsWith(VirtualExplorerPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseVirtualUnsortedPath(string path, out int? year, out int? month)
    {
        year = null;
        month = null;
        if (!IsVirtualExplorerPath(path))
        {
            return false;
        }

        var segments = path.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !string.Equals(segments[0], "virtual", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "unsorted", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length >= 3)
        {
            if (!int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear))
            {
                return false;
            }

            year = parsedYear;
        }

        if (segments.Length >= 4)
        {
            if (!int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMonth)
                || parsedMonth is < 1 or > 12)
            {
                return false;
            }

            month = parsedMonth;
        }

        return segments.Length <= 4;
    }

    private static string BuildVirtualUnsortedPath(int? year, int? month)
    {
        if (!year.HasValue)
        {
            return VirtualUnsortedPath;
        }

        if (!month.HasValue)
        {
            return $"{VirtualUnsortedPath}/{year.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        return $"{VirtualUnsortedPath}/{year.Value.ToString(CultureInfo.InvariantCulture)}/{month.Value.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static string GetVirtualFolderDisplayName(string normalizedPath)
    {
        if (!TryParseVirtualUnsortedPath(normalizedPath, out var year, out var month))
        {
            return normalizedPath;
        }

        if (!year.HasValue)
        {
            return "UNSORTED";
        }

        if (!month.HasValue)
        {
            return year.Value.ToString(CultureInfo.InvariantCulture);
        }

        return CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month.Value);
    }

    private static IReadOnlyList<NodeExplorerItemSnapshot> BuildExplorerEntries(
        string currentPath,
        IReadOnlyList<NodeLinkSnapshot> links)
    {
        if (links.Count == 0)
        {
            return [];
        }

        return OrderExplorerItems(
            links.Select(link =>
            {
                var browsePath = BuildChildBrowsePath(currentPath, link.Name, link.Target);
                return CreateExplorerItemSnapshot(link, browsePath, NodeOperatorDisplay.ResolveDisplayName(link.Name, link.Target));
            }));
    }

    private static NodeExplorerItemSnapshot CreateExplorerItemSnapshot(
        NodeLinkSnapshot link,
        string browsePath,
        string displayName)
        => new(
            string.IsNullOrWhiteSpace(displayName) ? link.Target : displayName,
            browsePath,
            link.Target,
            link.IsDirectory,
            NodeOperatorDisplay.GetTypeLabel(link.IsDirectory),
            link.ContentSize,
            link.IsDirectory ? link.ChildCount : 0);

    private static IReadOnlyList<NodeExplorerItemSnapshot> OrderExplorerItems(IEnumerable<NodeExplorerItemSnapshot> items)
        => items
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Target, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<NodeExplorerBreadcrumb> BuildBreadcrumbs(string normalizedPath)
    {
        if (TryParseVirtualUnsortedPath(normalizedPath, out var year, out var month))
        {
            var virtualBreadcrumbs = new List<NodeExplorerBreadcrumb>
            {
                new("UNSORTED", VirtualUnsortedPath)
            };

            if (year.HasValue)
            {
                virtualBreadcrumbs.Add(new(
                    year.Value.ToString(CultureInfo.InvariantCulture),
                    BuildVirtualUnsortedPath(year, null)));
            }

            if (month.HasValue)
            {
                virtualBreadcrumbs.Add(new(
                    CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month.Value),
                    BuildVirtualUnsortedPath(year, month)));
            }

            return virtualBreadcrumbs;
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return [];
        }

        var breadcrumbs = new List<NodeExplorerBreadcrumb>(segments.Length - 1);
        for (var index = 1; index < segments.Length; index++)
        {
            breadcrumbs.Add(new NodeExplorerBreadcrumb(
                segments[index],
                $"/{segments[0]}/{string.Join("/", segments.Skip(1).Take(index))}"));
        }

        return breadcrumbs;
    }

    private static string? TryGetParentPath(string normalizedPath)
    {
        if (TryParseVirtualUnsortedPath(normalizedPath, out var year, out var month))
        {
            if (month.HasValue)
            {
                return BuildVirtualUnsortedPath(year, null);
            }

            if (year.HasValue)
            {
                return VirtualUnsortedPath;
            }

            return null;
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 2)
        {
            return null;
        }

        return $"/{segments[0]}/{string.Join("/", segments.Skip(1).Take(segments.Length - 2))}";
    }

    private static string NormalizeBrowsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A CID or path is required.");
        }

        var trimmed = path.Trim();
        if (IsVirtualExplorerPath(trimmed))
        {
            return trimmed.StartsWith("/", StringComparison.Ordinal)
                ? trimmed
                : $"/{trimmed.TrimStart('/')}";
        }

        if (trimmed.StartsWith("/ipfs/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/ipns/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed.StartsWith("/", StringComparison.Ordinal)
            ? trimmed
            : $"/ipfs/{trimmed}";
    }

    private static string BuildChildBrowsePath(string currentPath, string? childName, string childCid)
    {
        var normalizedCurrent = NormalizeBrowsePath(currentPath).TrimEnd('/');
        return string.IsNullOrWhiteSpace(childName)
            ? NormalizeBrowsePath(childCid)
            : $"{normalizedCurrent}/{childName.Trim()}";
    }
}
