using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ipfs.Engine.Client.Models
{
    internal sealed class IpfsApiError
    {
        public string? Message { get; set; }
        public string[]? Details { get; set; }
        public string? Code { get; set; }
    }

    internal sealed class PeerInfoDto
    {
        public string? ID;
        public string? PublicKey;
        public string[]? Addresses;
        public string? AgentVersion;
        public string? ProtocolVersion;
    }

    internal sealed class PathDto
    {
        public string? Path;
    }

    internal sealed class BitswapLinkDto
    {
        [JsonProperty(PropertyName = "/")]
        public string? Link;
    }

    internal sealed class BitswapWantsDto
    {
        public List<BitswapLinkDto>? Keys;
    }

    internal sealed class BitswapLedgerDto
    {
        public string? Peer;
        public double Value;
        public ulong Sent;
        public ulong Recv;
        public ulong Exchanged;
    }

    internal sealed class BlockStatsDto
    {
        public string? Key;
        public long Size;
    }

    internal sealed class KeyDto
    {
        public string? Key;
    }

    internal sealed class HashDto
    {
        public string? Hash;
        public string? Error;
    }

    internal sealed class BootstrapPeersDto
    {
        public IEnumerable<string>? Peers;
    }

    internal sealed class ConfigDetailDto
    {
        public string? Key;
        public JToken? Value;
    }

    internal sealed class LinkedDataDto
    {
        [JsonProperty(PropertyName = "/")]
        public string? Link;
    }

    internal sealed class LinkedDataCidDto
    {
        public LinkedDataDto? Cid;
    }

    internal sealed class FileSystemNodeDto
    {
        public string? Name;
        public string? Hash;
        public string? Size;
    }

    internal sealed class FileSystemLinkDto
    {
        public string? Name;
        public string? Hash;
        public long Size;
        public long ContentSize;
        public string? Type;
        public int ChildCount;
    }

    internal sealed class FileSystemDetailDto
    {
        public string? Hash;
        public long Size;
        public string? Type;
        public FileSystemLinkDto[]? Links;
    }

    internal sealed class FileSystemDetailsDto
    {
        public Dictionary<string, string>? Arguments;
        public Dictionary<string, FileSystemDetailDto>? Objects;
    }

    internal sealed class DhtPeerResponseDto
    {
        public string? ID;
        public IEnumerable<string>? Addrs;
    }

    internal sealed class DhtPeerDto
    {
        public string? ID;
        public int Type;
        public IEnumerable<DhtPeerResponseDto>? Responses;
        public string? Extra;
    }

    internal sealed class CryptoKeyDto
    {
        public string? Name;
        public string? Id;
    }

    internal sealed class CryptoKeysDto
    {
        public IEnumerable<CryptoKeyDto>? Keys;
    }

    internal sealed class CryptoKeyRenameDto
    {
        public string? Was;
        public string? Now;
        public string? Id;
        public bool Overwrite;
    }

    internal sealed class NamedContentDto
    {
        public string? Name;
        public string? Value;
    }

    internal sealed class ObjectLinkDto
    {
        public string? Name;
        public string? Hash;
        public long Size;
    }

    internal sealed class ObjectLinkDetailDto
    {
        public string? Hash;
        public IEnumerable<ObjectLinkDto>? Links;
    }

    internal sealed class ObjectDataDetailDto
    {
        public string? Hash;
        public IEnumerable<ObjectLinkDto>? Links;
        public string? Data;
    }

    internal sealed class ObjectStatDto
    {
        public string? Hash;
        public int NumLinks { get; set; }
        public long LinksSize { get; set; }
        public long BlockSize { get; set; }
        public long DataSize { get; set; }
        public long CumulativeSize { get; set; }
    }

    internal sealed class PinDetailsDto
    {
        public Dictionary<string, object>? Keys;
    }

    internal sealed class PinsDto
    {
        public IEnumerable<string>? Pins;
    }

    internal sealed class PubsubTopicsDto
    {
        public IEnumerable<string>? Strings;
    }

    internal sealed class PubsubPeersDto
    {
        public IEnumerable<string>? Strings;
    }

    internal sealed class MessageDto
    {
        public string? from;
        public string? seqno;
        public string? data;
        public string[]? topicIDs;
    }

    internal sealed class StatsBitswapDto
    {
        public int ProvideBufLen;
        public IEnumerable<BitswapLinkDto>? Wantlist;
        public IEnumerable<string>? Peers;
        public ulong BlocksReceived;
        public ulong DataReceived;
        public ulong BlocksSent;
        public ulong DataSent;
        public ulong DupBlksReceived;
        public ulong DupDataReceived;
    }

    internal sealed class VersionBlockRepositoryDto
    {
        public string? Version;
    }

    internal sealed class ConnectedPeerDto
    {
        public string? Peer;
        public string? Addr;
        public string? Latency;
    }

    internal sealed class ConnectedPeersDto
    {
        public IEnumerable<ConnectedPeerDto>? Peers;
    }

    internal sealed class FiltersDto
    {
        public string[]? Strings;
    }

    internal sealed class AddrsDto
    {
        public Dictionary<string, List<string>>? Addrs;
    }
}
