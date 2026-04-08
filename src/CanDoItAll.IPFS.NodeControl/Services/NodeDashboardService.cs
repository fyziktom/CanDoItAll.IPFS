using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeDashboardService(IpfsClientFactory clientFactory)
{
    public async Task<NodeSummarySnapshot> GetSummaryAsync(CancellationToken cancellationToken)
    {
        using var lease = await clientFactory.CreateLeaseAsync(
            NodeConnectionRequestCategory.ReadOnlyUi,
            cancellationToken).ConfigureAwait(false);

        var peer = await lease.Client.Generic.IdAsync(cancel: cancellationToken).ConfigureAwait(false);
        var version = await lease.Client.Generic.VersionAsync(cancellationToken).ConfigureAwait(false);
        var repository = await lease.Client.Stats.RepositoryAsync(cancellationToken).ConfigureAwait(false);
        var bandwidth = await lease.Client.Stats.BandwidthAsync(cancellationToken).ConfigureAwait(false);
        var connectedPeers = (await lease.Client.Swarm.PeersAsync(cancellationToken).ConfigureAwait(false)).ToList();

        return new NodeSummarySnapshot
        {
            PeerId = peer.Id.ToString(),
            AgentVersion = peer.AgentVersion,
            ProtocolVersion = peer.ProtocolVersion,
            ApiVersion = version.TryGetValue("Version", out var apiVersion) ? apiVersion : "unknown",
            Addresses = peer.Addresses.Select(address => address.ToString()).ToList(),
            ConnectedPeerCount = connectedPeers.Count,
            RepoObjectCount = repository.NumObjects,
            RepoSizeBytes = repository.RepoSize,
            RepoStorageMaxBytes = repository.StorageMax,
            RepoPath = repository.RepoPath,
            RepoVersion = repository.Version,
            TotalInBytes = bandwidth.TotalIn,
            TotalOutBytes = bandwidth.TotalOut,
            RateIn = bandwidth.RateIn,
            RateOut = bandwidth.RateOut
        };
    }
}
