using System.IO;
using System.Text;
using System.Globalization;
using Ipfs;
using Ipfs.CoreApi;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.Json;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeOperatorService(IpfsClientFactory clientFactory, IExplorerIndexStore explorerIndexStore)
{
    private const string VirtualExplorerPrefix = "/virtual";
    private const string VirtualUnsortedPath = "/virtual/unsorted";
    private static readonly JsonSerializerOptions NodeIdentitySerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly IReadOnlyList<NodeCapabilityNote> CapabilityNotes =
    [
        new("Single file upload", "Available", "success", "Uploads go through the existing add route and can be pinned or wrapped."),
        new("Directory upload", "Available", "success", "Folder uploads now preserve nested files and subfolders through the add route."),
        new("CID and DAG browsing", "Available", "success", "file/ls, cat, object, and dag routes are wired into the UI."),
        new("Pin and unpin", "Available", "success", "pin/add, pin/ls, and pin/rm are exposed."),
        new("Block and object management", "Available", "success", "block get/put/rm/stat and object get/data/stat are exposed."),
        new("IPNS and keys", "Partial", "warning", "Key create/list/remove/rename and name publish/resolve work. Key import/export stays intentionally unavailable until the engine host exposes secure support."),
        new("Swarm and bootstrap", "Available", "success", "Peers, known addresses, connect, disconnect, filters, and bootstrap management are supported."),
        new("DHT tools", "Partial", "warning", "findpeer and findprovs work, but provide and DHT value storage routes are not exposed."),
        new("PubSub", "Available", "success", "Topic listing, peer lookup, publish, and live subscribe can run from the UI."),
        new("Node maintenance", "Partial", "warning", "Config read/write, repo gc, repo version, and shutdown are supported. Repo verify remains unavailable because the current HTTP surface returns 501.")
    ];

    public IReadOnlyList<NodeCapabilityNote> GetCapabilityNotes() => CapabilityNotes;

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
        return await ReadUtf8PreviewAsync(stream, maxBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListPinsAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.Pin.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(cid => cid.ToString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<NodeExplorerItemSnapshot> GetCachedPinnedExplorerItems()
        => OrderExplorerItems(explorerIndexStore.ListPinnedRoots().Select(CreateExplorerItemSnapshot));

    public async Task<bool> HasTrustedCachedPinnedExplorerItemsAsync(CancellationToken cancellationToken)
    {
        var cachedRoots = explorerIndexStore.ListPinnedRoots();
        if (cachedRoots.Count == 0)
        {
            return false;
        }

        var pinnedTargetSet = (await ListPinsAsync(cancellationToken).ConfigureAwait(false))
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

        var current = await InspectFileSystemAsync(path, cancellationToken).ConfigureAwait(false);
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

        var snapshot = await InspectFileSystemAsync(path, cancellationToken).ConfigureAwait(false);
        var normalizedPath = NormalizeBrowsePath(snapshot.RequestedPath);
        var safeDisplayName = ResolveDisplayName(displayName, ResolveDisplayName(normalizedPath, snapshot.ResolvedId));

        return new NodePreviewSnapshot
        {
            DisplayName = safeDisplayName,
            Target = snapshot.ResolvedId,
            Path = normalizedPath,
            IsDirectory = snapshot.IsDirectory,
            TypeLabel = GetTypeLabel(snapshot.IsDirectory),
            Size = snapshot.Size,
            ChildCount = snapshot.IsDirectory ? snapshot.Links.Count : 0,
            PreviewText = snapshot.IsDirectory
                ? string.Empty
                : await ReadFilePreviewAsync(snapshot.ResolvedId, 65536, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task<IReadOnlyList<string>> PinAsync(string path, bool recursive, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var pinned = (await lease.Client.Pin.AddAsync(path, recursive, cancellationToken).ConfigureAwait(false))
            .Select(cid => cid.ToString())
            .ToList();

        var snapshot = await InspectFileSystemAsync(path, cancellationToken).ConfigureAwait(false);
        RememberIndexedRoot(snapshot, ResolveDisplayName(path, snapshot.ResolvedId), isPinned: true);
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

    public async Task<NodeBlockSnapshot> GetBlockAsync(string cidText, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var block = await lease.Client.Block.GetAsync(Cid.Decode(cidText), cancellationToken).ConfigureAwait(false);
        var bytes = block.DataBytes;

        return new NodeBlockSnapshot
        {
            Cid = block.Id.ToString(),
            Size = block.Size,
            Utf8Preview = GetUtf8Preview(bytes, 4096),
            Base64Preview = Convert.ToBase64String(bytes.Length > 768 ? bytes[..768] : bytes)
        };
    }

    public async Task<string> PutBlockTextAsync(string text, bool pin, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var cid = await lease.Client.Block.PutAsync(Encoding.UTF8.GetBytes(text ?? string.Empty), pin: pin, cancel: cancellationToken).ConfigureAwait(false);
        return cid.ToString();
    }

    public async Task<string?> RemoveBlockAsync(string cidText, bool ignoreMissing, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var removed = await lease.Client.Block.RemoveAsync(Cid.Decode(cidText), ignoreMissing, cancellationToken).ConfigureAwait(false);
        return removed?.ToString();
    }

    public async Task<NodeObjectSnapshot> GetObjectAsync(string cidText, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var cid = Cid.Decode(cidText);
        var node = await lease.Client.Object.GetAsync(cid, cancellationToken).ConfigureAwait(false);
        var stat = await lease.Client.Object.StatAsync(cid, cancellationToken).ConfigureAwait(false);
        using var dataStream = await lease.Client.Object.DataAsync(cid, cancellationToken).ConfigureAwait(false);

        return new NodeObjectSnapshot
        {
            Cid = cid.ToString(),
            LinkCount = stat.LinkCount,
            LinkSize = stat.LinkSize,
            BlockSize = stat.BlockSize,
            DataSize = stat.DataSize,
            CumulativeSize = stat.CumulativeSize,
            DataPreview = await ReadUtf8PreviewAsync(dataStream, 4096, cancellationToken).ConfigureAwait(false),
            Links = node.Links
                .Select(link => new NodeLinkSnapshot(link.Name, link.Id.ToString(), link.Size, link.Size, false, 0))
                .ToList()
        };
    }

    public async Task<string> CreateEmptyDirectoryAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var node = await lease.Client.Object.NewDirectoryAsync(cancellationToken).ConfigureAwait(false);
        return node.Id.ToString();
    }

    public async Task<NodeDagSnapshot> GetDagAsync(string request, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = await lease.Client.Dag.GetAsync(request, cancellationToken).ConfigureAwait(false);
        return new NodeDagSnapshot
        {
            Request = request,
            Json = token.ToString(Formatting.Indented)
        };
    }

    public async Task<string> PutDagJsonAsync(string json, bool pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("A DAG payload is required.");
        }

        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = JToken.Parse(json);
        var objectValue = token as JObject ?? new JObject { ["value"] = token };
        var cid = await lease.Client.Dag.PutAsync(objectValue, pin: pin, cancel: cancellationToken).ConfigureAwait(false);
        return cid.ToString();
    }

    public async Task<string> ResolveNameAsync(string name, bool recursive, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Client.Name.ResolveAsync(name, recursive, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NodeNamePublishSnapshot> PublishNameAsync(
        string path,
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var result = await lease.Client.Name.PublishAsync(path, resolve: true, key: key, lifetime: lifetime, cancel: cancellationToken).ConfigureAwait(false);
        return new NodeNamePublishSnapshot
        {
            NamePath = result.NamePath,
            ContentPath = result.ContentPath
        };
    }

    public async Task<IReadOnlyList<NodeKeySnapshot>> ListKeysAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.Key.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(key => new NodeKeySnapshot(key.Name, key.Id.ToString()))
            .OrderBy(key => key.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<NodeKeySnapshot> CreateKeyAsync(string name, string keyType, int size, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var key = await lease.Client.Key.CreateAsync(name, keyType, size, cancellationToken).ConfigureAwait(false);
        return new NodeKeySnapshot(key.Name, key.Id.ToString());
    }

    public async Task<NodeKeySnapshot> RenameKeyAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var key = await lease.Client.Key.RenameAsync(oldName, newName, cancellationToken).ConfigureAwait(false);
        return new NodeKeySnapshot(key.Name, key.Id.ToString());
    }

    public async Task<string?> RemoveKeyAsync(string name, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var removed = await lease.Client.Key.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
        return removed?.Name;
    }

    public async Task<NodeNetworkSnapshot> GetNetworkSnapshotAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);

        var connectedPeersTask = lease.Client.Swarm.PeersAsync(cancellationToken);
        var knownPeersTask = lease.Client.Swarm.AddressesAsync(cancellationToken);
        var bootstrapTask = lease.Client.Bootstrap.ListAsync(cancellationToken);
        var filtersTask = lease.Client.Swarm.ListAddressFiltersAsync(false, cancellationToken);
        var topicsTask = lease.Client.PubSub.SubscribedTopicsAsync(cancellationToken);
        var bitswapTask = lease.Client.Stats.BitswapAsync(cancellationToken);

        await Task.WhenAll(connectedPeersTask, knownPeersTask, bootstrapTask, filtersTask, topicsTask, bitswapTask).ConfigureAwait(false);

        var bitswap = bitswapTask.Result;
        return new NodeNetworkSnapshot
        {
            ConnectedPeers = connectedPeersTask.Result.Select(ToPeerSnapshot).OrderBy(peer => peer.Id, StringComparer.Ordinal).ToList(),
            KnownPeers = knownPeersTask.Result.Select(ToPeerSnapshot).OrderBy(peer => peer.Id, StringComparer.Ordinal).ToList(),
            BootstrapPeers = bootstrapTask.Result.Select(address => address.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            AddressFilters = filtersTask.Result.Select(address => address.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            PubSubTopics = topicsTask.Result.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            BitswapWantlist = (bitswap.Wantlist ?? []).Select(cid => cid.ToString()).ToList(),
            BitswapBlocksReceived = bitswap.BlocksReceived,
            BitswapBlocksSent = bitswap.BlocksSent,
            BitswapDataReceived = bitswap.DataReceived,
            BitswapDataSent = bitswap.DataSent
        };
    }

    public async Task ConnectAsync(string address, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.Swarm.ConnectAsync((MultiAddress)address, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedPeerConnectionTarget> ConnectByKnownNodeApiAsync(
        string hostOrApiUrl,
        int apiPort,
        int swarmPort,
        CancellationToken cancellationToken)
    {
        var request = ResolveKnownNodeApiRequest(hostOrApiUrl, apiPort, swarmPort);
        using var httpClient = new HttpClient
        {
            BaseAddress = request.ApiBaseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };

        using var response = await httpClient.PostAsync("api/v0/id", content: null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var identity = await System.Text.Json.JsonSerializer.DeserializeAsync<NodeIdentityResponse>(
            stream,
            NodeIdentitySerializerOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The node API at {request.ApiBaseAddress} returned an empty identity response.");
        if (string.IsNullOrWhiteSpace(identity.Id))
        {
            throw new InvalidOperationException($"The node API at {request.ApiBaseAddress} did not return a peer id.");
        }

        var dialAddress = BuildExplicitDialAddress(request.DialHost, request.SwarmPort, identity.Id.Trim());
        await ConnectAsync(dialAddress, cancellationToken).ConfigureAwait(false);

        return new ResolvedPeerConnectionTarget
        {
            RequestedHost = request.DialHost,
            ApiBaseUrl = request.ApiBaseAddress.ToString(),
            PeerId = identity.Id.Trim(),
            DialAddress = dialAddress,
            AgentVersion = string.IsNullOrWhiteSpace(identity.AgentVersion) ? "unknown" : identity.AgentVersion.Trim(),
            AdvertisedAddresses = (identity.Addresses ?? [])
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Select(address => address.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }

    public async Task DisconnectAsync(string address, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.Swarm.DisconnectAsync((MultiAddress)address, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> AddBootstrapAsync(string address, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var result = await lease.Client.Bootstrap.AddAsync((MultiAddress)address, cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }

    public async Task<string?> RemoveBootstrapAsync(string address, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var result = await lease.Client.Bootstrap.RemoveAsync((MultiAddress)address, cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }

    public async Task<IReadOnlyList<string>> RestoreDefaultBootstrapAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.Bootstrap.AddDefaultsAsync(cancellationToken).ConfigureAwait(false))
            .Select(address => address.ToString())
            .ToList();
    }

    public async Task RemoveAllBootstrapAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.Bootstrap.RemoveAllAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> AddAddressFilterAsync(string address, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var result = await lease.Client.Swarm.AddAddressFilterAsync((MultiAddress)address, false, cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }

    public async Task<string?> RemoveAddressFilterAsync(string address, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var result = await lease.Client.Swarm.RemoveAddressFilterAsync((MultiAddress)address, false, cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }

    public async Task<NodePeerSnapshot> FindPeerAsync(string peerId, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var peer = await lease.Client.Dht.FindPeerAsync((MultiHash)peerId, cancellationToken).ConfigureAwait(false);
        return ToPeerSnapshot(peer);
    }

    public async Task<IReadOnlyList<NodePeerSnapshot>> FindProvidersAsync(string cidText, int limit, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.Dht.FindProvidersAsync(Cid.Decode(cidText), limit, cancel: cancellationToken).ConfigureAwait(false))
            .Select(ToPeerSnapshot)
            .ToList();
    }

    public async Task<IReadOnlyList<NodePeerSnapshot>> ListPubSubPeersAsync(string? topic, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.PubSub.PeersAsync(string.IsNullOrWhiteSpace(topic) ? null : topic.Trim(), cancellationToken).ConfigureAwait(false))
            .Select(ToPeerSnapshot)
            .ToList();
    }

    public async Task PublishPubSubAsync(string topic, string message, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.PubSub.PublishAsync(topic, message, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetFullConfigAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = await lease.Client.Config.GetAsync(cancellationToken).ConfigureAwait(false);
        return token.ToString(Formatting.Indented);
    }

    public async Task<string> GetConfigValueAsync(string key, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = await lease.Client.Config.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return token.ToString(Formatting.Indented);
    }

    public async Task SetConfigValueAsync(string key, string value, bool treatAsJson, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        if (treatAsJson)
        {
            await lease.Client.Config.SetAsync(key, JToken.Parse(value), cancellationToken).ConfigureAwait(false);
            return;
        }

        await lease.Client.Config.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetRepositoryVersionAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Client.BlockRepository.VersionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RunRepositoryGcAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateAdminLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.BlockRepository.RemoveGarbageAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task VerifyRepositoryAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateAdminLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.BlockRepository.VerifyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ShutdownNodeAsync()
    {
        using var lease = await CreateAdminLeaseAsync(CancellationToken.None).ConfigureAwait(false);
        await lease.Client.Generic.ShutdownAsync().ConfigureAwait(false);
    }

    private Task<IpfsClientLease> CreateReadLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, cancellationToken);

    private Task<IpfsClientLease> CreateMutationLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Mutation, cancellationToken);

    private Task<IpfsClientLease> CreateAdminLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Admin, cancellationToken);

    private async Task RefreshExplorerIndexAsync(CancellationToken cancellationToken)
    {
        var pinned = await ListPinsAsync(cancellationToken).ConfigureAwait(false);
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

            missingSnapshots[cid] = await InspectFileSystemAsync(cid, cancellationToken).ConfigureAwait(false);
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
            RememberIndexedRoot(snapshot, ResolveDisplayName(cid, snapshot.ResolvedId), isPinned: true, firstPinnedAtUtc: seenAtUtc);
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
                : await InspectFileSystemAsync(current, cancellationToken).ConfigureAwait(false);
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
            ResolveDisplayName(displayName, snapshot.ResolvedId),
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
            DisplayName = ResolveDisplayName(displayName, GetVirtualFolderDisplayName(explorerSnapshot.NormalizedPath)),
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
            ResolveDisplayName(record.DisplayName, record.Target),
            NormalizeBrowsePath(record.Target),
            record.Target,
            record.IsDirectory,
            GetTypeLabel(record.IsDirectory),
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
                return CreateExplorerItemSnapshot(link, browsePath, ResolveDisplayName(link.Name, link.Target));
            }));
    }

    private static NodeFileSnapshot ToFileSnapshot(string request, IFileSystemNode node)
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

    private static NodeExplorerItemSnapshot CreateExplorerItemSnapshot(
        NodeFileSnapshot snapshot,
        string browsePath,
        string displayName)
        => new(
            string.IsNullOrWhiteSpace(displayName) ? snapshot.ResolvedId : displayName,
            browsePath,
            snapshot.ResolvedId,
            snapshot.IsDirectory,
            GetTypeLabel(snapshot.IsDirectory),
            snapshot.Size,
            snapshot.IsDirectory ? snapshot.Links.Count : 0);

    private static NodeExplorerItemSnapshot CreateExplorerItemSnapshot(
        NodeLinkSnapshot link,
        string browsePath,
        string displayName)
        => new(
            string.IsNullOrWhiteSpace(displayName) ? link.Target : displayName,
            browsePath,
            link.Target,
            link.IsDirectory,
            GetTypeLabel(link.IsDirectory),
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

    private static string GetTypeLabel(bool isDirectory)
        => isDirectory ? "File folder" : "File";

    private static string ResolveDisplayName(string? value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var trimmed = value.Trim();
            var lastSlash = trimmed.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < trimmed.Length - 1
                ? trimmed[(lastSlash + 1)..]
                : trimmed;
        }

        return fallback;
    }

    private static NodePeerSnapshot ToPeerSnapshot(Peer peer)
        => new(
            peer.Id?.ToString() ?? string.Empty,
            string.IsNullOrWhiteSpace(peer.AgentVersion) ? "unknown" : peer.AgentVersion,
            string.IsNullOrWhiteSpace(peer.ProtocolVersion) ? "unknown" : peer.ProtocolVersion,
            peer.ConnectedAddress?.ToString() ?? "not connected",
            peer.Latency?.ToString() ?? "n/a",
            (peer.Addresses ?? []).Select(address => address.ToString()).ToList());

    private static KnownNodeApiRequest ResolveKnownNodeApiRequest(string hostOrApiUrl, int apiPort, int swarmPort)
    {
        if (string.IsNullOrWhiteSpace(hostOrApiUrl))
        {
            throw new InvalidOperationException("A node host or API URL is required.");
        }

        var trimmed = hostOrApiUrl.Trim();
        var resolvedApiPort = Math.Clamp(apiPort, 1, 65535);
        var resolvedSwarmPort = Math.Clamp(swarmPort, 1, 65535);
        var scheme = Uri.UriSchemeHttp;
        var dialHost = trimmed;

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            if (!string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only http and https node API URLs are supported.");
            }

            scheme = absoluteUri.Scheme;
            dialHost = absoluteUri.Host;
            if (!absoluteUri.IsDefaultPort)
            {
                resolvedApiPort = absoluteUri.Port;
            }
        }

        if (string.IsNullOrWhiteSpace(dialHost))
        {
            throw new InvalidOperationException("A valid node host is required.");
        }

        var apiBaseAddress = new UriBuilder(scheme, dialHost, resolvedApiPort).Uri;
        return new KnownNodeApiRequest(apiBaseAddress, dialHost, resolvedSwarmPort);
    }

    private static string BuildExplicitDialAddress(string host, int swarmPort, string peerId)
    {
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            var protocol = ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? "ip6"
                : "ip4";
            return $"/{protocol}/{ipAddress}/tcp/{swarmPort}/ipfs/{peerId}";
        }

        return $"/dns/{host}/tcp/{swarmPort}/ipfs/{peerId}";
    }

    private static async Task<string> ReadUtf8PreviewAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes];
        var read = await stream.ReadAsync(buffer.AsMemory(0, maxBytes), cancellationToken).ConfigureAwait(false);
        return GetUtf8Preview(buffer[..read], maxBytes);
    }

    private static string GetUtf8Preview(byte[] bytes, int maxBytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (LooksBinary(bytes))
        {
            return string.Empty;
        }

        var preview = Encoding.UTF8.GetString(bytes);
        if (bytes.Length < maxBytes)
        {
            return preview;
        }

        return $"{preview}\n\n... preview truncated ...";
    }

    private static bool LooksBinary(byte[] bytes)
    {
        var controlCharacters = 0;
        var inspected = Math.Min(bytes.Length, 512);

        for (var index = 0; index < inspected; index++)
        {
            var current = bytes[index];
            if (current == 0)
            {
                return true;
            }

            if (current < 32 && current is not (byte)'\r' and not (byte)'\n' and not (byte)'\t')
            {
                controlCharacters++;
            }
        }

        return controlCharacters > inspected / 8;
    }

    private sealed record KnownNodeApiRequest(Uri ApiBaseAddress, string DialHost, int SwarmPort);

    private sealed class NodeIdentityResponse
    {
        public string Id { get; set; } = string.Empty;

        public string AgentVersion { get; set; } = string.Empty;

        public List<string> Addresses { get; set; } = [];
    }
}
