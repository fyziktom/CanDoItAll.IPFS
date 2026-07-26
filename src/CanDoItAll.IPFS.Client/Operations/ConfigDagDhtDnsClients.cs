using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.CoreApi;
using Ipfs.Engine.Client.Mapping;
using Ipfs.Engine.Client.Models;
using Ipfs.Engine.Client.Transport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ipfs.Engine.Client.Operations
{
    public sealed class ConfigClient : IConfigApi
    {
        private readonly IIpfsApiTransport transport;

        internal ConfigClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public Task<JObject> GetAsync(CancellationToken cancel = default)
        {
            return transport.PostJsonAsync<JObject>("config/show", query: null, cancel);
        }

        public async Task<JToken> GetAsync(string key, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", key);
            var dto = await transport.PostJsonAsync<ConfigDetailDto>("config", query, cancel).ConfigureAwait(false);
            return dto.Value ?? JValue.CreateNull();
        }

        public async Task ReplaceAsync(JObject config)
        {
            using (var stream = new MemoryStream())
            using (var textWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(textWriter))
            {
                await config.WriteToAsync(writer).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                stream.Position = 0;
                await transport.SendMultipartAsync("config/replace", query: null, stream, "config.json", "application/json", CancellationToken.None).ConfigureAwait(false);
            }
        }

        public async Task SetAsync(string key, string value, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.AddRepeated(query, "arg", new[] { key, value });
            await transport.PostJsonAsync<ConfigDetailDto>("config", query, cancel).ConfigureAwait(false);
        }

        public async Task SetAsync(string key, JToken value, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.AddRepeated(query, "arg", new[] { key, value.ToString(Formatting.None) });
            QueryStringBuilder.Add(query, "json", true);
            await transport.PostJsonAsync<ConfigDetailDto>("config", query, cancel).ConfigureAwait(false);
        }
    }

    public sealed class DagClient : IDagApi
    {
        private readonly IIpfsApiTransport transport;

        internal DagClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public Task<JObject> GetAsync(Cid id, CancellationToken cancel = default)
        {
            return transport.PostJsonAsync<JObject>("dag/get", BuildArgQuery(id.ToString()), cancel);
        }

        public Task<JToken> GetAsync(string path, CancellationToken cancel = default)
        {
            return transport.PostJsonAsync<JToken>("dag/get", BuildArgQuery(path), cancel);
        }

        public async Task<T> GetAsync<T>(Cid id, CancellationToken cancel = default)
        {
            var token = await GetAsync(id, cancel).ConfigureAwait(false);
            return token.ToObject<T>()!;
        }

        public async Task<Cid> PutAsync(JObject data, string contentType = "dag-cbor", string multiHash = MultiHash.DefaultAlgorithmName, string encoding = MultiBase.DefaultAlgorithmName, bool pin = true, CancellationToken cancel = default)
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                data.WriteTo(jsonWriter);
                jsonWriter.Flush();
                stream.Position = 0;
                return await PutAsync(stream, contentType, multiHash, encoding, pin, cancel).ConfigureAwait(false);
            }
        }

        public async Task<Cid> PutAsync(Stream data, string contentType = "dag-cbor", string multiHash = MultiHash.DefaultAlgorithmName, string encoding = MultiBase.DefaultAlgorithmName, bool pin = true, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "format", contentType);
            QueryStringBuilder.Add(query, "hash", multiHash);
            QueryStringBuilder.Add(query, "cid-base", encoding);
            QueryStringBuilder.Add(query, "pin", pin);

            var dto = await transport.PostMultipartJsonAsync<LinkedDataCidDto>("dag/put", query, data, "dag.json", "application/json", cancel).ConfigureAwait(false);
            var cid = Cid.Decode(dto.Cid!.Link!);
            if (pin)
            {
                await new PinClient(transport).AddAsync(cid.ToString(), recursive: false, cancel).ConfigureAwait(false);
            }
            return cid;
        }

        public Task<Cid> PutAsync(object data, string contentType = "dag-cbor", string multiHash = MultiHash.DefaultAlgorithmName, string encoding = MultiBase.DefaultAlgorithmName, bool pin = true, CancellationToken cancel = default)
        {
            var token = JObject.FromObject(data);
            return PutAsync(token, contentType, multiHash, encoding, pin, cancel);
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }

    public sealed class DhtClient : IDhtApi
    {
        private readonly IIpfsApiTransport transport;

        internal DhtClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<Peer> FindPeerAsync(MultiHash id, CancellationToken cancel = default)
        {
            var dto = await transport.PostJsonAsync<DhtPeerDto>("dht/findpeer", BuildArgQuery(id.ToString()), cancel).ConfigureAwait(false);
            return DtoMapper.ToDhtPeer(dto);
        }

        public async Task<IEnumerable<Peer>> FindProvidersAsync(Cid id, int limit = 20, Action<Peer>? providerFound = null, CancellationToken cancel = default)
        {
            var query = BuildArgQuery(id.ToString());
            QueryStringBuilder.Add(query, "num-providers", limit);
            var dtos = await transport.PostJsonAsync<List<DhtPeerDto>>("dht/findprovs", query, cancel).ConfigureAwait(false);
            var peers = new List<Peer>();
            foreach (var dto in dtos)
            {
                var peer = DtoMapper.ToDhtPeer(dto);
                peers.Add(peer);
                providerFound?.Invoke(peer);
            }
            return peers;
        }

        public Task ProvideAsync(Cid cid, bool advertise = true, CancellationToken cancel = default)
        {
            throw ApiClientErrors.MissingServerCapability(nameof(ProvideAsync), "The server does not expose DHT provide.");
        }

        public Task<byte[]> GetAsync(byte[] key, CancellationToken cancel = default)
        {
            throw ApiClientErrors.MissingServerCapability(nameof(GetAsync), "The server does not expose DHT value lookups.");
        }

        public Task<bool> TryGetAsync(byte[] key, out byte[] value, CancellationToken cancel = default)
        {
            value = Array.Empty<byte>();
            throw ApiClientErrors.MissingServerCapability(nameof(TryGetAsync), "The server does not expose DHT value lookups.");
        }

        public Task PutAsync(byte[] key, out byte[] value, CancellationToken cancel = default)
        {
            value = Array.Empty<byte>();
            throw ApiClientErrors.MissingServerCapability(nameof(PutAsync), "The server does not expose DHT value writes.");
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }

    public sealed class DnsClient : IDnsApi
    {
        private readonly IIpfsApiTransport transport;

        internal DnsClient(IIpfsApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<string> ResolveAsync(string name, bool recursive = false, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", name);
            QueryStringBuilder.Add(query, "recursive", recursive);
            var dto = await transport.PostJsonAsync<PathDto>("dns", query, cancel).ConfigureAwait(false);
            return dto.Path ?? string.Empty;
        }
    }
}
