using System;
using System.Collections.Generic;
using System.Linq;
using Ipfs.CoreApi;
using Ipfs.Engine.Client.Models;

namespace Ipfs.Engine.Client.Mapping
{
    internal static class DtoMapper
    {
        public static Peer ToPeer(PeerInfoDto dto)
        {
            return new Peer
            {
                Id = dto.ID,
                PublicKey = dto.PublicKey,
                Addresses = (dto.Addresses ?? Array.Empty<string>()).Select(a => (MultiAddress)a).ToArray(),
                AgentVersion = dto.AgentVersion,
                ProtocolVersion = dto.ProtocolVersion
            };
        }

        public static Peer ToConnectedPeer(ConnectedPeerDto dto)
        {
            MultiAddress? connectedAddress = null;
            if (!string.IsNullOrWhiteSpace(dto.Addr))
            {
                connectedAddress = dto.Addr;
            }

            return new Peer
            {
                Id = dto.Peer,
                ConnectedAddress = connectedAddress,
                Latency = ParseLatency(dto.Latency)
            };
        }

        public static Peer ToDhtPeer(DhtPeerDto dto)
        {
            var response = dto.Responses?.FirstOrDefault();
            var id = response?.ID ?? dto.ID;
            var peerId = (MultiHash)id!;
            var addresses = (response?.Addrs ?? Array.Empty<string>())
                .Select(addr => ((MultiAddress)addr).WithPeerId(peerId))
                .ToArray();

            return new Peer
            {
                Id = peerId,
                Addresses = addresses
            };
        }

        public static BitswapLedger ToBitswapLedger(BitswapLedgerDto dto)
        {
            return new BitswapLedger
            {
                Peer = new Peer { Id = dto.Peer! },
                BlocksExchanged = dto.Exchanged,
                DataReceived = dto.Recv,
                DataSent = dto.Sent
            };
        }

        public static BitswapData ToBitswapData(StatsBitswapDto dto)
        {
            return new BitswapData
            {
                ProvideBufLen = dto.ProvideBufLen,
                Wantlist = (dto.Wantlist ?? Array.Empty<BitswapLinkDto>())
                    .Where(x => !string.IsNullOrWhiteSpace(x.Link))
                    .Select(x => Cid.Decode(x.Link!))
                    .ToArray(),
                Peers = (dto.Peers ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => (MultiHash)x)
                    .ToArray(),
                BlocksReceived = dto.BlocksReceived,
                DataReceived = dto.DataReceived,
                BlocksSent = dto.BlocksSent,
                DataSent = dto.DataSent,
                DupBlksReceived = dto.DupBlksReceived,
                DupDataReceived = dto.DupDataReceived
            };
        }

        public static IEnumerable<Cid> ToCids(IEnumerable<string>? values)
        {
            return (values ?? Array.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(Cid.Decode)
                .ToArray();
        }

        public static ClientKey ToKey(CryptoKeyDto dto)
        {
            return new ClientKey(dto.Name ?? string.Empty, dto.Id ?? string.Empty);
        }

        public static NamedContent ToNamedContent(NamedContentDto dto)
        {
            return new NamedContent
            {
                NamePath = dto.Name,
                ContentPath = dto.Value
            };
        }

        public static ObjectStat ToObjectStat(ObjectStatDto dto)
        {
            return new ObjectStat
            {
                LinkCount = dto.NumLinks,
                LinkSize = dto.LinksSize,
                BlockSize = dto.BlockSize,
                DataSize = dto.DataSize,
                CumulativeSize = dto.CumulativeSize
            };
        }

        public static IEnumerable<IMerkleLink> ToMerkleLinks(IEnumerable<ObjectLinkDto>? links)
        {
            return (links ?? Array.Empty<ObjectLinkDto>())
                .Where(link => !string.IsNullOrWhiteSpace(link.Hash))
                .Select(link => (IMerkleLink)new ClientMerkleLink(link.Name, Cid.Decode(link.Hash!), link.Size))
                .ToArray();
        }

        public static IEnumerable<IFileSystemLink> ToFileSystemLinks(IEnumerable<FileSystemLinkDto>? links)
        {
            return (links ?? Array.Empty<FileSystemLinkDto>())
                .Where(link => !string.IsNullOrWhiteSpace(link.Hash))
                .Select(link => (IFileSystemLink)new ClientMerkleLink(
                    link.Name,
                    Cid.Decode(link.Hash!),
                    link.Size,
                    isDirectory: string.Equals(link.Type, "Directory", StringComparison.OrdinalIgnoreCase),
                    contentSize: link.ContentSize,
                    childCount: link.ChildCount))
                .ToArray();
        }

        public static ClientFileSystemNode ToFileSystemNode(FileSystemDetailDto dto)
        {
            return new ClientFileSystemNode(
                id: Cid.Decode(dto.Hash!),
                isDirectory: string.Equals(dto.Type, "Directory", StringComparison.OrdinalIgnoreCase),
                size: dto.Size,
                links: ToFileSystemLinks(dto.Links));
        }

        public static DagNode ToDagNode(ObjectDataDetailDto dto, bool dataIsBase64)
        {
            var data = dto.Data == null
                ? Array.Empty<byte>()
                : dataIsBase64
                    ? Convert.FromBase64String(dto.Data)
                    : System.Text.Encoding.UTF8.GetBytes(dto.Data);

            var node = new DagNode(data, ToMerkleLinks(dto.Links));
            if (!string.IsNullOrWhiteSpace(dto.Hash))
            {
                node.Id = Cid.Decode(dto.Hash);
            }
            return node;
        }

        public static ClientPublishedMessage ToPublishedMessage(MessageDto dto)
        {
            var senderBytes = string.IsNullOrWhiteSpace(dto.from)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(dto.from);
            var sender = new Peer
            {
                Id = new MultiHash(senderBytes)
            };

            var sequenceNumber = string.IsNullOrWhiteSpace(dto.seqno)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(dto.seqno);
            var data = string.IsNullOrWhiteSpace(dto.data)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(dto.data);

            return new ClientPublishedMessage(sender, dto.topicIDs ?? Array.Empty<string>(), sequenceNumber, data);
        }

        private static TimeSpan? ParseLatency(string? latency)
        {
            if (string.IsNullOrWhiteSpace(latency) || string.Equals(latency, "n/a", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Duration.Parse(latency);
        }
    }
}
