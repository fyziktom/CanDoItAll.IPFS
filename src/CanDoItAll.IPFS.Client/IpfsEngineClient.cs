using System;
using System.Net.Http;
using Ipfs.CoreApi;
using Ipfs.Engine.Client.Operations;
using Ipfs.Engine.Client.Transport;

namespace Ipfs.Engine.Client
{
    /// <summary>
    ///   A typed HTTP client for an <c>Ipfs.Engine</c> node.
    /// </summary>
    public sealed class IpfsEngineClient : ICoreApi
    {
        /// <summary>
        ///   Creates a new client for the configured node.
        /// </summary>
        public IpfsEngineClient(HttpClient httpClient, IpfsNodeClientOptions? options = null)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            Options = options ?? new IpfsNodeClientOptions();
            Transport = new IpfsHttpTransport(httpClient, Options);

            Generic = new GenericClient(Transport);
            Bitswap = new BitswapClient(Transport);
            Block = new BlockClient(Transport);
            BlockRepository = new BlockRepositoryClient(Transport);
            Bootstrap = new BootstrapClient(Transport);
            Config = new ConfigClient(Transport);
            Dag = new DagClient(Transport);
            Dht = new DhtClient(Transport);
            Dns = new DnsClient(Transport);
            FileSystem = new FileSystemClient(Transport);
            Key = new KeyClient(Transport);
            Name = new NameClient(Transport);
            Object = new ObjectClient(Transport);
            Pin = new PinClient(Transport);
            PubSub = new PubSubClient(Transport);
            Stats = new StatsClient(Transport);
            Swarm = new SwarmClient(Transport);
        }

        public IpfsNodeClientOptions Options { get; }

        internal IpfsHttpTransport Transport { get; }

        public GenericClient Generic { get; }
        public BitswapClient Bitswap { get; }
        public BlockClient Block { get; }
        public BlockRepositoryClient BlockRepository { get; }
        public BootstrapClient Bootstrap { get; }
        public ConfigClient Config { get; }
        public DagClient Dag { get; }
        public DhtClient Dht { get; }
        public DnsClient Dns { get; }
        public FileSystemClient FileSystem { get; }
        public KeyClient Key { get; }
        public NameClient Name { get; }
        public ObjectClient Object { get; }
        public PinClient Pin { get; }
        public PubSubClient PubSub { get; }
        public StatsClient Stats { get; }
        public SwarmClient Swarm { get; }

        IGenericApi ICoreApi.Generic => Generic;
        IBitswapApi ICoreApi.Bitswap => Bitswap;
        IBlockApi ICoreApi.Block => Block;
        IBlockRepositoryApi ICoreApi.BlockRepository => BlockRepository;
        IBootstrapApi ICoreApi.Bootstrap => Bootstrap;
        IConfigApi ICoreApi.Config => Config;
        IDagApi ICoreApi.Dag => Dag;
        IDhtApi ICoreApi.Dht => Dht;
        IDnsApi ICoreApi.Dns => Dns;
        IFileSystemApi ICoreApi.FileSystem => FileSystem;
        IKeyApi ICoreApi.Key => Key;
        INameApi ICoreApi.Name => Name;
        IObjectApi ICoreApi.Object => Object;
        IPinApi ICoreApi.Pin => Pin;
        IPubSubApi ICoreApi.PubSub => PubSub;
        IStatsApi ICoreApi.Stats => Stats;
        ISwarmApi ICoreApi.Swarm => Swarm;
    }
}
