using System.Text;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Ipfs;
using Ipfs.CoreApi;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeFileWorkflowService(
    IpfsClientFactory clientFactory,
    IExplorerIndexStore explorerIndexStore) : INodeFileWorkflow
{
    public async Task<NodeFileSnapshot> InspectFileSystemAsync(string path, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var node = await lease.Client.FileSystem.ListFileAsync(path, cancellationToken).ConfigureAwait(false);
        return ToFileSnapshot(path, node);
    }

    public async Task<NodeFileSnapshot> UploadBrowserFileAsync(
        IBrowserFile browserFile,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browserFile);

        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = browserFile.OpenReadStream(maxAllowedSize: 1024L * 1024L * 1024L, cancellationToken: cancellationToken);
        var node = await lease.Client.FileSystem.AddAsync(
            stream,
            browserFile.Name,
            new AddFileOptions { Pin = pin, Wrap = wrap },
            cancellationToken).ConfigureAwait(false);

        var snapshot = ToFileSnapshot(browserFile.Name, node);
        RememberIndexedRoot(snapshot, browserFile.Name, pin);
        return snapshot;
    }

    public async Task<NodeFileSnapshot> UploadLocalFileAsync(
        string filePath,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("A local file path is required.");
        }

        var normalizedPath = Path.GetFullPath(filePath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("The selected file could not be found.", normalizedPath);
        }

        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = File.OpenRead(normalizedPath);
        var fileName = Path.GetFileName(normalizedPath);
        var node = await lease.Client.FileSystem.AddAsync(
            stream,
            fileName,
            new AddFileOptions { Pin = pin, Wrap = wrap },
            cancellationToken).ConfigureAwait(false);

        var snapshot = ToFileSnapshot(fileName, node);
        RememberIndexedRoot(snapshot, fileName, pin);
        return snapshot;
    }

    public async Task<NodeFileSnapshot> UploadLocalDirectoryAsync(
        string directoryPath,
        bool pin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("A local folder path is required.");
        }

        var normalizedPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"The selected folder '{normalizedPath}' could not be found.");
        }

        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var node = await lease.Client.FileSystem.AddDirectoryAsync(
            normalizedPath,
            recursive: true,
            new AddFileOptions { Pin = pin },
            cancellationToken).ConfigureAwait(false);

        var requestedName = Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var snapshot = ToFileSnapshot(requestedName, node);
        RememberIndexedRoot(snapshot, requestedName, pin);
        return snapshot;
    }

    public async Task<NodeFileSnapshot> UploadTextAsync(
        string name,
        string content,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content ?? string.Empty), writable: false);
        var node = await lease.Client.FileSystem.AddAsync(
            stream,
            string.IsNullOrWhiteSpace(name) ? "note.txt" : name.Trim(),
            new AddFileOptions { Pin = pin, Wrap = wrap },
            cancellationToken).ConfigureAwait(false);

        var requestedName = string.IsNullOrWhiteSpace(name) ? "note.txt" : name.Trim();
        var snapshot = ToFileSnapshot(requestedName, node);
        RememberIndexedRoot(snapshot, requestedName, pin);
        return snapshot;
    }

    public async Task<string> ReadFilePreviewAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        using var stream = await lease.Client.FileSystem.ReadFileAsync(path, 0, maxBytes, cancellationToken).ConfigureAwait(false);
        return await NodePreviewTextReader.ReadUtf8PreviewAsync(stream, maxBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListPinsAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.Pin.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(cid => cid.ToString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> PinAsync(string path, bool recursive, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var pinned = (await lease.Client.Pin.AddAsync(path, recursive, cancellationToken).ConfigureAwait(false))
            .Select(cid => cid.ToString())
            .ToList();

        var snapshot = await InspectFileSystemAsync(path, cancellationToken).ConfigureAwait(false);
        RememberIndexedRoot(snapshot, NodeOperatorDisplay.ResolveDisplayName(path, snapshot.ResolvedId), isPinned: true);
        explorerIndexStore.MarkPinnedRootsSeen(pinned, DateTimeOffset.UtcNow);
        return pinned;
    }

    public async Task<IReadOnlyList<string>> UnpinAsync(string cidText, bool recursive, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var cid = Cid.Decode(cidText);
        var removed = (await lease.Client.Pin.RemoveAsync(cid, recursive, cancellationToken).ConfigureAwait(false))
            .Select(item => item.ToString())
            .ToList();
        foreach (var item in removed)
        {
            explorerIndexStore.MarkUnpinned(item);
        }

        return removed;
    }

    private Task<IpfsClientLease> CreateReadLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, cancellationToken);

    private Task<IpfsClientLease> CreateMutationLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Mutation, cancellationToken);

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

    internal static NodeFileSnapshot ToFileSnapshot(string request, IFileSystemNode node)
        => new()
        {
            RequestedPath = request,
            ResolvedId = node.Id.ToString(),
            IsDirectory = node.IsDirectory,
            Size = node.Size,
            Links = node.Links
                .Select(link => new NodeLinkSnapshot(
                    link.Name,
                    link.Id.ToString(),
                    link.Size,
                    link.ContentSize,
                    link.IsDirectory,
                    link.ChildCount))
                .ToList()
        };
}
