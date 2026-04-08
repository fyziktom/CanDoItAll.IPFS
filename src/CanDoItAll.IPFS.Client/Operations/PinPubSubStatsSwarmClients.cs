using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.CoreApi;
using Ipfs.Engine.Client.Mapping;
using Ipfs.Engine.Client.Models;
using Ipfs.Engine.Client.Transport;

namespace Ipfs.Engine.Client.Operations
{
    public sealed class PinClient : ApiClientBase, IPinApi
    {
        internal PinClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public async Task<IEnumerable<Cid>> AddAsync(string path, bool recursive = true, CancellationToken cancel = default)
        {
            var query = BuildArgQuery(path);
            QueryStringBuilder.Add(query, "recursive", recursive);
            var dto = await Transport.PostJsonAsync<PinsDto>("pin/add", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToCids(dto.Pins);
        }

        public async Task<IEnumerable<Cid>> ListAsync(CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<PinDetailsDto>("pin/ls", query: null, cancel).ConfigureAwait(false);
            return dto.Keys == null ? Array.Empty<Cid>() : DtoMapper.ToCids(dto.Keys.Keys);
        }

        public async Task<IEnumerable<Cid>> RemoveAsync(Cid id, bool recursive = true, CancellationToken cancel = default)
        {
            var query = BuildArgQuery(id.ToString());
            QueryStringBuilder.Add(query, "recursive", recursive);
            var dto = await Transport.PostJsonAsync<PinsDto>("pin/rm", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToCids(dto.Pins);
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }

    public sealed class PubSubClient : ApiClientBase, IPubSubApi
    {
        internal PubSubClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public async Task<IEnumerable<Peer>> PeersAsync(string? topic = null, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", topic);
            var dto = await Transport.PostJsonAsync<PubsubPeersDto>("pubsub/peers", query, cancel).ConfigureAwait(false);
            return (dto.Strings ?? Array.Empty<string>())
                .Select(id => new Peer { Id = id })
                .ToArray();
        }

        public Task PublishAsync(string topic, string message, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.AddRepeated(query, "arg", new[] { topic, message });
            return Transport.SendAsync("pubsub/pub", query, cancel);
        }

        public Task PublishAsync(string topic, byte[] message, CancellationToken cancel = default)
        {
            throw MissingServerCapability(nameof(PublishAsync), "The current pubsub publish route only accepts text safely.");
        }

        public Task PublishAsync(string topic, Stream message, CancellationToken cancel = default)
        {
            throw MissingServerCapability(nameof(PublishAsync), "The server does not expose stream publish.");
        }

        public Task SubscribeAsync(string topic, Action<IPublishedMessage> handler, CancellationToken cancellationToken = default)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return Transport.ReadNdjsonAsync<MessageDto>("pubsub/sub", BuildArgQuery(topic), dto =>
            {
                handler(DtoMapper.ToPublishedMessage(dto));
                return Task.CompletedTask;
            }, cancellationToken);
        }

        public async Task<IEnumerable<string>> SubscribedTopicsAsync(CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<PubsubTopicsDto>("pubsub/ls", query: null, cancel).ConfigureAwait(false);
            return dto.Strings ?? Array.Empty<string>();
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }

    public sealed class StatsClient : ApiClientBase, IStatsApi
    {
        internal StatsClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public Task<BandwidthData> BandwidthAsync(CancellationToken cancel = default)
        {
            return Transport.PostJsonAsync<BandwidthData>("stats/bw", query: null, cancel);
        }

        public async Task<BitswapData> BitswapAsync(CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<StatsBitswapDto>("stats/bitswap", query: null, cancel).ConfigureAwait(false);
            return DtoMapper.ToBitswapData(dto);
        }

        public Task<RepositoryData> RepositoryAsync(CancellationToken cancel = default)
        {
            return Transport.PostJsonAsync<RepositoryData>("stats/repo", query: null, cancel);
        }
    }

    public sealed class SwarmClient : ApiClientBase, ISwarmApi
    {
        internal SwarmClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public async Task<MultiAddress?> AddAddressFilterAsync(MultiAddress address, bool persist = false, CancellationToken cancel = default)
        {
            if (persist)
            {
                throw MissingServerCapability(nameof(AddAddressFilterAsync), "The server only supports non-persistent filters today.");
            }

            var dto = await Transport.PostJsonAsync<FiltersDto>("swarm/filters/add", BuildArgQuery(address.ToString()), cancel).ConfigureAwait(false);
            return dto.Strings?.FirstOrDefault();
        }

        public async Task<IEnumerable<Peer>> AddressesAsync(CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<AddrsDto>("swarm/addrs", query: null, cancel).ConfigureAwait(false);
            var peers = new List<Peer>();
            if (dto.Addrs == null)
            {
                return peers;
            }

            foreach (var pair in dto.Addrs)
            {
                peers.Add(new Peer
                {
                    Id = pair.Key,
                    Addresses = pair.Value.Select(address => (MultiAddress)address).ToArray()
                });
            }
            return peers;
        }

        public Task ConnectAsync(MultiAddress address, CancellationToken cancel = default)
        {
            return Transport.SendAsync("swarm/connect", BuildArgQuery(address.ToString()), cancel);
        }

        public Task DisconnectAsync(MultiAddress address, CancellationToken cancel = default)
        {
            return Transport.SendAsync("swarm/disconnect", BuildArgQuery(address.ToString()), cancel);
        }

        public async Task<IEnumerable<MultiAddress>> ListAddressFiltersAsync(bool persist = false, CancellationToken cancel = default)
        {
            if (persist)
            {
                throw MissingServerCapability(nameof(ListAddressFiltersAsync), "The server only supports non-persistent filters today.");
            }

            var dto = await Transport.PostJsonAsync<FiltersDto>("swarm/filters", query: null, cancel).ConfigureAwait(false);
            return (dto.Strings ?? Array.Empty<string>()).Select(value => (MultiAddress)value).ToArray();
        }

        public async Task<IEnumerable<Peer>> PeersAsync(CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<ConnectedPeersDto>("swarm/peers", query: null, cancel).ConfigureAwait(false);
            return (dto.Peers ?? Array.Empty<ConnectedPeerDto>()).Select(DtoMapper.ToConnectedPeer).ToArray();
        }

        public async Task<MultiAddress?> RemoveAddressFilterAsync(MultiAddress address, bool persist = false, CancellationToken cancel = default)
        {
            if (persist)
            {
                throw MissingServerCapability(nameof(RemoveAddressFilterAsync), "The server only supports non-persistent filters today.");
            }

            var dto = await Transport.PostJsonAsync<FiltersDto>("swarm/filters/rm", BuildArgQuery(address.ToString()), cancel).ConfigureAwait(false);
            return dto.Strings?.FirstOrDefault();
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }
}
