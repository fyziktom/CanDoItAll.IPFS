using System.Net;
using System.Text.Json;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Models;
using Ipfs;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeNetworkWorkflowService : INodeNetworkWorkflow
{
    private static readonly JsonSerializerOptions NodeIdentitySerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IpfsClientFactory clientFactory;
    private readonly IHttpClientFactory httpClientFactory;

    public NodeNetworkWorkflowService(IpfsClientFactory clientFactory)
        : this(clientFactory, NodeControlServiceCollectionExtensions.CreateCompatibilityHttpClientFactory())
    {
    }

    public NodeNetworkWorkflowService(IpfsClientFactory clientFactory, IHttpClientFactory httpClientFactory)
    {
        this.clientFactory = clientFactory;
        this.httpClientFactory = httpClientFactory;
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

        var connectedPeers = await connectedPeersTask.ConfigureAwait(false);
        var knownPeers = await knownPeersTask.ConfigureAwait(false);
        var bootstrapPeers = await bootstrapTask.ConfigureAwait(false);
        var addressFilters = await filtersTask.ConfigureAwait(false);
        var pubSubTopics = await topicsTask.ConfigureAwait(false);
        var bitswap = await bitswapTask.ConfigureAwait(false);

        return new NodeNetworkSnapshot
        {
            ConnectedPeers = connectedPeers.Select(ToPeerSnapshot).OrderBy(peer => peer.Id, StringComparer.Ordinal).ToList(),
            KnownPeers = knownPeers.Select(ToPeerSnapshot).OrderBy(peer => peer.Id, StringComparer.Ordinal).ToList(),
            BootstrapPeers = bootstrapPeers.Select(address => address.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            AddressFilters = addressFilters.Select(address => address.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            PubSubTopics = pubSubTopics.OrderBy(value => value, StringComparer.Ordinal).ToList(),
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
        using var httpClient = httpClientFactory.CreateClient(NodeControlHttpClientNames.NodeRead);
        httpClient.BaseAddress = request.ApiBaseAddress;
        httpClient.Timeout = TimeSpan.FromSeconds(15);

        using var response = await httpClient.PostAsync("api/v0/id", content: null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var identity = await JsonSerializer.DeserializeAsync<NodeIdentityResponse>(
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

    private Task<IpfsClientLease> CreateReadLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, cancellationToken);

    private Task<IpfsClientLease> CreateMutationLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Mutation, cancellationToken);

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

    private sealed record KnownNodeApiRequest(Uri ApiBaseAddress, string DialHost, int SwarmPort);

    private sealed class NodeIdentityResponse
    {
        public string Id { get; set; } = string.Empty;

        public string AgentVersion { get; set; } = string.Empty;

        public List<string> Addresses { get; set; } = [];
    }
}
