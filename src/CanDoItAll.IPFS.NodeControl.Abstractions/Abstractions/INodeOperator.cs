using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface INodeOperator
{
    IReadOnlyList<NodeCapabilityNote> GetCapabilityNotes();

    Task<NodeFileSnapshot> InspectFileSystemAsync(string path, CancellationToken cancellationToken);

    Task<NodeFileSnapshot> UploadLocalFileAsync(
        string filePath,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken);

    Task<NodeFileSnapshot> UploadLocalDirectoryAsync(
        string directoryPath,
        bool pin,
        CancellationToken cancellationToken);

    Task<NodeFileSnapshot> UploadTextAsync(
        string name,
        string content,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken);

    Task<string> ReadFilePreviewAsync(string path, int maxBytes, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListPinsAsync(CancellationToken cancellationToken);

    IReadOnlyList<NodeExplorerItemSnapshot> GetCachedPinnedExplorerItems();

    Task<bool> HasTrustedCachedPinnedExplorerItemsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NodeExplorerItemSnapshot>> ListPinnedExplorerItemsAsync(CancellationToken cancellationToken);

    Task<NodeExplorerSnapshot> GetExplorerSnapshotAsync(string path, CancellationToken cancellationToken);

    Task<NodePreviewSnapshot> GetPreviewSnapshotAsync(string path, string? displayName, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> PinAsync(string path, bool recursive, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> UnpinAsync(string cidText, bool recursive, CancellationToken cancellationToken);

    Task<NodeBlockSnapshot> GetBlockAsync(string cidText, CancellationToken cancellationToken);

    Task<string> PutBlockTextAsync(string text, bool pin, CancellationToken cancellationToken);

    Task<string?> RemoveBlockAsync(string cidText, bool ignoreMissing, CancellationToken cancellationToken);

    Task<NodeObjectSnapshot> GetObjectAsync(string cidText, CancellationToken cancellationToken);

    Task<string> CreateEmptyDirectoryAsync(CancellationToken cancellationToken);

    Task<NodeDagSnapshot> GetDagAsync(string request, CancellationToken cancellationToken);

    Task<string> PutDagJsonAsync(string json, bool pin, CancellationToken cancellationToken);

    Task<string> ResolveNameAsync(string name, bool recursive, CancellationToken cancellationToken);

    Task<NodeNamePublishSnapshot> PublishNameAsync(
        string path,
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NodeKeySnapshot>> ListKeysAsync(CancellationToken cancellationToken);

    Task<NodeKeySnapshot> CreateKeyAsync(string name, string keyType, int size, CancellationToken cancellationToken);

    Task<NodeKeySnapshot> RenameKeyAsync(string oldName, string newName, CancellationToken cancellationToken);

    Task<string?> RemoveKeyAsync(string name, CancellationToken cancellationToken);

    Task<NodeNetworkSnapshot> GetNetworkSnapshotAsync(CancellationToken cancellationToken);

    Task ConnectAsync(string address, CancellationToken cancellationToken);

    Task<ResolvedPeerConnectionTarget> ConnectByKnownNodeApiAsync(
        string hostOrApiUrl,
        int apiPort,
        int swarmPort,
        CancellationToken cancellationToken);

    Task DisconnectAsync(string address, CancellationToken cancellationToken);

    Task<string?> AddBootstrapAsync(string address, CancellationToken cancellationToken);

    Task<string?> RemoveBootstrapAsync(string address, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> RestoreDefaultBootstrapAsync(CancellationToken cancellationToken);

    Task RemoveAllBootstrapAsync(CancellationToken cancellationToken);

    Task<string?> AddAddressFilterAsync(string address, CancellationToken cancellationToken);

    Task<string?> RemoveAddressFilterAsync(string address, CancellationToken cancellationToken);

    Task<NodePeerSnapshot> FindPeerAsync(string peerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NodePeerSnapshot>> FindProvidersAsync(string cidText, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<NodePeerSnapshot>> ListPubSubPeersAsync(string? topic, CancellationToken cancellationToken);

    Task PublishPubSubAsync(string topic, string message, CancellationToken cancellationToken);

    Task<string> GetFullConfigAsync(CancellationToken cancellationToken);

    Task<string> GetConfigValueAsync(string key, CancellationToken cancellationToken);

    Task SetConfigValueAsync(string key, string value, bool treatAsJson, CancellationToken cancellationToken);

    Task<string> GetRepositoryVersionAsync(CancellationToken cancellationToken);

    Task RunRepositoryGcAsync(CancellationToken cancellationToken);

    Task VerifyRepositoryAsync(CancellationToken cancellationToken);

    Task ShutdownNodeAsync();
}
