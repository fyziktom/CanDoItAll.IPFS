namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed class NodeSummarySnapshot
{
    public required string PeerId { get; init; }

    public required string AgentVersion { get; init; }

    public required string ProtocolVersion { get; init; }

    public required string ApiVersion { get; init; }

    public required IReadOnlyList<string> Addresses { get; init; }

    public required int ConnectedPeerCount { get; init; }

    public required ulong RepoObjectCount { get; init; }

    public required ulong RepoSizeBytes { get; init; }

    public required ulong RepoStorageMaxBytes { get; init; }

    public required string RepoPath { get; init; }

    public required string RepoVersion { get; init; }

    public required ulong TotalInBytes { get; init; }

    public required ulong TotalOutBytes { get; init; }

    public required double RateIn { get; init; }

    public required double RateOut { get; init; }
}
