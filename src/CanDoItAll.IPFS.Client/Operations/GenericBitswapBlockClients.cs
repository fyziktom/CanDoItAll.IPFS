using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.CoreApi;
using Ipfs.Engine.Client.Mapping;
using Ipfs.Engine.Client.Models;
using Ipfs.Engine.Client.Transport;

namespace Ipfs.Engine.Client.Operations
{
    public sealed class GenericClient : IGenericApi
    {
        private readonly IIpfsApiTransport transport;

        internal GenericClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<Peer> IdAsync(MultiHash? peer = null, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", peer?.ToString());
            var dto = await transport.PostJsonAsync<PeerInfoDto>("id", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToPeer(dto);
        }

        public Task<IEnumerable<PingResult>> PingAsync(MultiHash peer, int count = 10, CancellationToken cancel = default)
        {
            throw ApiClientErrors.MissingServerCapability(nameof(PingAsync), "The server does not expose a ping route yet.");
        }

        public Task<IEnumerable<PingResult>> PingAsync(MultiAddress address, int count = 10, CancellationToken cancel = default)
        {
            throw ApiClientErrors.MissingServerCapability(nameof(PingAsync), "The server does not expose a ping route yet.");
        }

        public async Task<string> ResolveAsync(string name, bool recursive = false, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", name);
            QueryStringBuilder.Add(query, "recursive", recursive);
            var dto = await transport.PostJsonAsync<PathDto>("resolve", query, cancel).ConfigureAwait(false);
            return dto.Path ?? string.Empty;
        }

        public Task ShutdownAsync()
        {
            return transport.SendAsync("shutdown", query: null, CancellationToken.None);
        }

        public Task<Dictionary<string, string>> VersionAsync(CancellationToken cancel = default)
        {
            return transport.PostJsonAsync<Dictionary<string, string>>("version", query: null, cancel);
        }
    }

    public sealed class BitswapClient : IBitswapApi
    {
        private readonly IIpfsApiTransport transport;

        internal BitswapClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public Task<IDataBlock> GetAsync(Cid id, CancellationToken cancel = default)
        {
            throw ApiClientErrors.MissingServerCapability(nameof(GetAsync), "The server does not expose remote bitswap fetch.");
        }

        public async Task<BitswapLedger> LedgerAsync(Peer peer, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", peer?.Id?.ToString());
            var dto = await transport.PostJsonAsync<BitswapLedgerDto>("bitswap/ledger", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToBitswapLedger(dto);
        }

        public Task UnwantAsync(Cid id, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", id.ToString());
            return transport.SendAsync("bitswap/unwant", query, cancel);
        }

        public async Task<IEnumerable<Cid>> WantsAsync(MultiHash? peer = null, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", peer?.ToString());
            var dto = await transport.PostJsonAsync<BitswapWantsDto>("bitswap/wantlist", query, cancel).ConfigureAwait(false);
            var links = dto.Keys ?? new List<BitswapLinkDto>();
            var cids = new List<Cid>();
            foreach (var link in links)
            {
                if (!string.IsNullOrWhiteSpace(link.Link))
                {
                    cids.Add(Cid.Decode(link.Link));
                }
            }
            return cids;
        }
    }

    public sealed class BlockClient : IBlockApi
    {
        private readonly IIpfsApiTransport transport;

        internal BlockClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<IDataBlock> GetAsync(Cid id, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", id.ToString());
            using (var stream = await transport.PostStreamAsync("block/get", query, cancel).ConfigureAwait(false))
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory, 81920, cancel).ConfigureAwait(false);
                return new ClientDataBlock(id, memory.ToArray());
            }
        }

        public async Task<Cid> PutAsync(byte[] data, string contentType = Cid.DefaultContentType, string multiHash = MultiHash.DefaultAlgorithmName, string encoding = MultiBase.DefaultAlgorithmName, bool pin = false, CancellationToken cancel = default)
        {
            using (var stream = new MemoryStream(data, writable: false))
            {
                return await PutAsync(stream, contentType, multiHash, encoding, pin, cancel).ConfigureAwait(false);
            }
        }

        public async Task<Cid> PutAsync(Stream data, string contentType = Cid.DefaultContentType, string multiHash = MultiHash.DefaultAlgorithmName, string encoding = MultiBase.DefaultAlgorithmName, bool pin = false, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "format", contentType);
            QueryStringBuilder.Add(query, "mhtype", multiHash);
            QueryStringBuilder.Add(query, "cid-base", encoding);

            var dto = await transport.PostMultipartJsonAsync<KeyDto>("block/put", query, data, "block.bin", "application/octet-stream", cancel).ConfigureAwait(false);
            var cid = Cid.Decode(dto.Key!);

            if (pin)
            {
                await new PinClient(transport).AddAsync(cid.ToString(), recursive: false, cancel).ConfigureAwait(false);
            }

            return cid;
        }

        public async Task<Cid?> RemoveAsync(Cid id, bool ignoreNonexistent = false, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", id.ToString());
            QueryStringBuilder.Add(query, "force", ignoreNonexistent);

            var dto = await transport.PostJsonOrDefaultAsync<HashDto>("block/rm", query, cancel).ConfigureAwait(false);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Hash))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dto.Error))
            {
                if (ignoreNonexistent)
                {
                    return null;
                }

                throw new KeyNotFoundException(dto.Error);
            }

            return Cid.Decode(dto.Hash);
        }

        public async Task<IDataBlock> StatAsync(Cid id, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", id.ToString());
            var dto = await transport.PostJsonAsync<BlockStatsDto>("block/stat", query, cancel).ConfigureAwait(false);
            return new ClientDataBlock(Cid.Decode(dto.Key!), dataBytes: null, size: dto.Size);
        }
    }

    public sealed class BlockRepositoryClient : IBlockRepositoryApi
    {
        private readonly IIpfsApiTransport transport;

        internal BlockRepositoryClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public Task RemoveGarbageAsync(CancellationToken cancel = default)
        {
            return transport.SendAsync("repo/gc", query: null, cancel);
        }

        public Task<RepositoryData> StatisticsAsync(CancellationToken cancel = default)
        {
            return transport.PostJsonAsync<RepositoryData>("repo/stat", query: null, cancel);
        }

        public Task VerifyAsync(CancellationToken cancel = default)
        {
            return transport.SendAsync("repo/verify", query: null, cancel);
        }

        public async Task<string> VersionAsync(CancellationToken cancel = default)
        {
            var dto = await transport.PostJsonAsync<VersionBlockRepositoryDto>("repo/version", query: null, cancel).ConfigureAwait(false);
            return dto.Version ?? string.Empty;
        }
    }

    public sealed class BootstrapClient : IBootstrapApi
    {
        private readonly IIpfsApiTransport transport;

        internal BootstrapClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<MultiAddress?> AddAsync(MultiAddress address, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", address.ToString());
            var dto = await transport.PostJsonAsync<BootstrapPeersDto>("bootstrap/add", query, cancel).ConfigureAwait(false);
            foreach (var peer in dto.Peers ?? Array.Empty<string>())
            {
                return peer;
            }

            return null;
        }

        public async Task<IEnumerable<MultiAddress>> AddDefaultsAsync(CancellationToken cancel = default)
        {
            var dto = await transport.PostJsonAsync<BootstrapPeersDto>("bootstrap/add/default", query: null, cancel).ConfigureAwait(false);
            var peers = new List<MultiAddress>();
            foreach (var peer in dto.Peers ?? Array.Empty<string>())
            {
                peers.Add(peer);
            }
            return peers;
        }

        public async Task<IEnumerable<MultiAddress>> ListAsync(CancellationToken cancel = default)
        {
            var dto = await transport.PostJsonAsync<BootstrapPeersDto>("bootstrap/list", query: null, cancel).ConfigureAwait(false);
            var peers = new List<MultiAddress>();
            foreach (var peer in dto.Peers ?? Array.Empty<string>())
            {
                peers.Add(peer);
            }
            return peers;
        }

        public Task RemoveAllAsync(CancellationToken cancel = default)
        {
            return transport.SendAsync("bootstrap/rm/all", query: null, cancel);
        }

        public async Task<MultiAddress?> RemoveAsync(MultiAddress address, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", address.ToString());
            var dto = await transport.PostJsonAsync<BootstrapPeersDto>("bootstrap/rm", query, cancel).ConfigureAwait(false);
            foreach (var peer in dto.Peers ?? Array.Empty<string>())
            {
                return peer;
            }

            return null;
        }
    }
}
