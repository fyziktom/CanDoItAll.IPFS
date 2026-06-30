namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed class NodeSummarySnapshot
{
    public required string PeerId { get; init; }

    public required string AgentVersion { get; init; }

    public required string ProtocolVersion { get; init; }

    public required string ApiVersion { get; init; }

    public required IReadOnlyList<string> Addresses { get; init; }

    public required IReadOnlyList<NodePeerSnapshot> ConnectedPeers { get; init; }

    public required int ConnectedPeerCount { get; init; }

    public required int KnownPeerCount { get; init; }

    public required int BootstrapPeerCount { get; init; }

    public required int AddressFilterCount { get; init; }

    public required int PubSubTopicCount { get; init; }

    public required ulong RepoObjectCount { get; init; }

    public required ulong RepoSizeBytes { get; init; }

    public required ulong RepoStorageMaxBytes { get; init; }

    public required int PinnedCidCount { get; init; }

    public required string RepoPath { get; init; }

    public required string RepoVersion { get; init; }

    public required ulong TotalInBytes { get; init; }

    public required ulong TotalOutBytes { get; init; }

    public required double RateIn { get; init; }

    public required double RateOut { get; init; }

    public required int BitswapPeerCount { get; init; }

    public required int BitswapWantlistCount { get; init; }

    public required int BitswapProvideBufferLength { get; init; }

    public required ulong BitswapBlocksReceived { get; init; }

    public required ulong BitswapBlocksSent { get; init; }

    public required ulong BitswapDataReceivedBytes { get; init; }

    public required ulong BitswapDataSentBytes { get; init; }

    public required ulong BitswapDuplicateBlocksReceived { get; init; }

    public required ulong BitswapDuplicateDataReceivedBytes { get; init; }

    public required DateTimeOffset SnapshotUtc { get; init; }
}
