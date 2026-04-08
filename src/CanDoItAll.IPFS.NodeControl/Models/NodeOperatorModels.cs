namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed record NodeCapabilityNote(string Feature, string Status, string Tone, string Detail);

public sealed record NodeLinkSnapshot(
    string Name,
    string Target,
    long Size,
    long ContentSize,
    bool IsDirectory,
    int ChildCount);

public sealed class NodeFileSnapshot
{
    public required string RequestedPath { get; init; }

    public required string ResolvedId { get; init; }

    public required bool IsDirectory { get; init; }

    public required long Size { get; init; }

    public IReadOnlyList<NodeLinkSnapshot> Links { get; init; } = [];
}

public sealed record NodeExplorerBreadcrumb(string Label, string Path);

public sealed record NodeExplorerItemSnapshot(
    string DisplayName,
    string Path,
    string Target,
    bool IsDirectory,
    string TypeLabel,
    long Size,
    int ChildCount);

public sealed class NodeExplorerSnapshot
{
    public required NodeFileSnapshot Current { get; init; }

    public required string NormalizedPath { get; init; }

    public string? ParentPath { get; init; }

    public IReadOnlyList<NodeExplorerBreadcrumb> Breadcrumbs { get; init; } = [];

    public IReadOnlyList<NodeExplorerItemSnapshot> Entries { get; init; } = [];
}

public sealed class NodePreviewSnapshot
{
    public required string DisplayName { get; init; }

    public required string Target { get; init; }

    public required string Path { get; init; }

    public required bool IsDirectory { get; init; }

    public required string TypeLabel { get; init; }

    public required long Size { get; init; }

    public required int ChildCount { get; init; }

    public required string PreviewText { get; init; }
}

public sealed class NodeBlockSnapshot
{
    public required string Cid { get; init; }

    public required long Size { get; init; }

    public required string Utf8Preview { get; init; }

    public required string Base64Preview { get; init; }
}

public sealed class NodeObjectSnapshot
{
    public required string Cid { get; init; }

    public required int LinkCount { get; init; }

    public required long LinkSize { get; init; }

    public required long BlockSize { get; init; }

    public required long DataSize { get; init; }

    public required long CumulativeSize { get; init; }

    public required string DataPreview { get; init; }

    public IReadOnlyList<NodeLinkSnapshot> Links { get; init; } = [];
}

public sealed class NodeDagSnapshot
{
    public required string Request { get; init; }

    public required string Json { get; init; }
}

public sealed class NodeNamePublishSnapshot
{
    public required string NamePath { get; init; }

    public required string ContentPath { get; init; }
}

public sealed record NodeKeySnapshot(string Name, string Id);

public sealed record NodePeerSnapshot(
    string Id,
    string AgentVersion,
    string ProtocolVersion,
    string ConnectedAddress,
    string Latency,
    IReadOnlyList<string> Addresses);

public sealed class ResolvedPeerConnectionTarget
{
    public required string RequestedHost { get; init; }

    public required string ApiBaseUrl { get; init; }

    public required string PeerId { get; init; }

    public required string DialAddress { get; init; }

    public required string AgentVersion { get; init; }

    public required IReadOnlyList<string> AdvertisedAddresses { get; init; }
}

public sealed class NodeNetworkSnapshot
{
    public IReadOnlyList<NodePeerSnapshot> ConnectedPeers { get; init; } = [];

    public IReadOnlyList<NodePeerSnapshot> KnownPeers { get; init; } = [];

    public IReadOnlyList<string> BootstrapPeers { get; init; } = [];

    public IReadOnlyList<string> AddressFilters { get; init; } = [];

    public IReadOnlyList<string> PubSubTopics { get; init; } = [];

    public IReadOnlyList<string> BitswapWantlist { get; init; } = [];

    public ulong BitswapBlocksReceived { get; init; }

    public ulong BitswapBlocksSent { get; init; }

    public ulong BitswapDataReceived { get; init; }

    public ulong BitswapDataSent { get; init; }
}
