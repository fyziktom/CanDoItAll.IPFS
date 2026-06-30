using CanDoItAll.IPFS.NodeControl.Models;
using Ipfs;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeDashboardService(IpfsClientFactory clientFactory)
{
    public async Task<NodeSummarySnapshot> GetSummaryAsync(CancellationToken cancellationToken)
    {
        using var lease = await clientFactory.CreateLeaseAsync(
            NodeConnectionRequestCategory.ReadOnlyUi,
            cancellationToken).ConfigureAwait(false);

        var peerTask = lease.Client.Generic.IdAsync(cancel: cancellationToken);
        var versionTask = lease.Client.Generic.VersionAsync(cancellationToken);
        var repositoryTask = lease.Client.Stats.RepositoryAsync(cancellationToken);
        var bandwidthTask = lease.Client.Stats.BandwidthAsync(cancellationToken);
        var connectedPeersTask = lease.Client.Swarm.PeersAsync(cancellationToken);
        var knownPeersTask = lease.Client.Swarm.AddressesAsync(cancellationToken);
        var bootstrapTask = lease.Client.Bootstrap.ListAsync(cancellationToken);
        var filtersTask = lease.Client.Swarm.ListAddressFiltersAsync(false, cancellationToken);
        var topicsTask = lease.Client.PubSub.SubscribedTopicsAsync(cancellationToken);
        var bitswapTask = lease.Client.Stats.BitswapAsync(cancellationToken);
        var pinsTask = lease.Client.Pin.ListAsync(cancellationToken);

        await Task.WhenAll(
            peerTask,
            versionTask,
            repositoryTask,
            bandwidthTask,
            connectedPeersTask,
            knownPeersTask,
            bootstrapTask,
            filtersTask,
            topicsTask,
            bitswapTask,
            pinsTask).ConfigureAwait(false);

        var peer = await peerTask.ConfigureAwait(false);
        var version = await versionTask.ConfigureAwait(false);
        var repository = await repositoryTask.ConfigureAwait(false);
        var bandwidth = await bandwidthTask.ConfigureAwait(false);
        var connectedPeers = (await connectedPeersTask.ConfigureAwait(false))
            .Select(ToPeerSnapshot)
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToList();
        var knownPeerCount = (await knownPeersTask.ConfigureAwait(false)).Count();
        var bootstrapPeerCount = (await bootstrapTask.ConfigureAwait(false)).Count();
        var addressFilterCount = (await filtersTask.ConfigureAwait(false)).Count();
        var pubSubTopicCount = (await topicsTask.ConfigureAwait(false)).Count();
        var bitswap = await bitswapTask.ConfigureAwait(false);
        var pinnedCidCount = (await pinsTask.ConfigureAwait(false))
            .Select(cid => cid.ToString())
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new NodeSummarySnapshot
        {
            PeerId = peer.Id.ToString(),
            AgentVersion = peer.AgentVersion,
            ProtocolVersion = peer.ProtocolVersion,
            ApiVersion = version.TryGetValue("Version", out var apiVersion) ? apiVersion : "unknown",
            Addresses = peer.Addresses.Select(address => address.ToString()).ToList(),
            ConnectedPeers = connectedPeers,
            ConnectedPeerCount = connectedPeers.Count,
            KnownPeerCount = knownPeerCount,
            BootstrapPeerCount = bootstrapPeerCount,
            AddressFilterCount = addressFilterCount,
            PubSubTopicCount = pubSubTopicCount,
            RepoObjectCount = repository.NumObjects,
            RepoSizeBytes = repository.RepoSize,
            RepoStorageMaxBytes = repository.StorageMax,
            PinnedCidCount = pinnedCidCount,
            RepoPath = repository.RepoPath,
            RepoVersion = repository.Version,
            TotalInBytes = bandwidth.TotalIn,
            TotalOutBytes = bandwidth.TotalOut,
            RateIn = bandwidth.RateIn,
            RateOut = bandwidth.RateOut,
            BitswapPeerCount = (bitswap.Peers ?? []).Count(),
            BitswapWantlistCount = (bitswap.Wantlist ?? []).Count(),
            BitswapProvideBufferLength = bitswap.ProvideBufLen,
            BitswapBlocksReceived = bitswap.BlocksReceived,
            BitswapBlocksSent = bitswap.BlocksSent,
            BitswapDataReceivedBytes = bitswap.DataReceived,
            BitswapDataSentBytes = bitswap.DataSent,
            BitswapDuplicateBlocksReceived = bitswap.DupBlksReceived,
            BitswapDuplicateDataReceivedBytes = bitswap.DupDataReceived,
            SnapshotUtc = DateTimeOffset.UtcNow
        };
    }

    private static NodePeerSnapshot ToPeerSnapshot(Peer peer)
        => new(
            peer.Id?.ToString() ?? string.Empty,
            string.IsNullOrWhiteSpace(peer.AgentVersion) ? "unknown" : peer.AgentVersion,
            string.IsNullOrWhiteSpace(peer.ProtocolVersion) ? "unknown" : peer.ProtocolVersion,
            peer.ConnectedAddress?.ToString() ?? "not connected",
            peer.Latency?.ToString() ?? "n/a",
            (peer.Addresses ?? []).Select(address => address.ToString()).ToList());
}
