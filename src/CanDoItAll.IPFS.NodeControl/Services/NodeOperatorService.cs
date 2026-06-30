using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeOperatorService(
    NodeFileWorkflowService fileWorkflow,
    INodeExplorerWorkflow explorerWorkflow,
    INodeContentWorkflow contentWorkflow,
    INodeNetworkWorkflow networkWorkflow,
    INodeMaintenanceWorkflow maintenanceWorkflow) : INodeOperator
{
    public IReadOnlyList<NodeCapabilityNote> GetCapabilityNotes()
        => maintenanceWorkflow.GetCapabilityNotes();

    public Task<NodeFileSnapshot> InspectFileSystemAsync(string path, CancellationToken cancellationToken)
        => fileWorkflow.InspectFileSystemAsync(path, cancellationToken);

    public Task<NodeFileSnapshot> UploadBrowserFileAsync(
        IBrowserFile browserFile,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken)
        => fileWorkflow.UploadBrowserFileAsync(browserFile, pin, wrap, cancellationToken);

    public Task<NodeFileSnapshot> UploadLocalFileAsync(
        string filePath,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken)
        => fileWorkflow.UploadLocalFileAsync(filePath, pin, wrap, cancellationToken);

    public Task<NodeFileSnapshot> UploadLocalDirectoryAsync(
        string directoryPath,
        bool pin,
        CancellationToken cancellationToken)
        => fileWorkflow.UploadLocalDirectoryAsync(directoryPath, pin, cancellationToken);

    public Task<NodeFileSnapshot> UploadTextAsync(
        string name,
        string content,
        bool pin,
        bool wrap,
        CancellationToken cancellationToken)
        => fileWorkflow.UploadTextAsync(name, content, pin, wrap, cancellationToken);

    public Task<string> ReadFilePreviewAsync(string path, int maxBytes, CancellationToken cancellationToken)
        => fileWorkflow.ReadFilePreviewAsync(path, maxBytes, cancellationToken);

    public Task<IReadOnlyList<string>> ListPinsAsync(CancellationToken cancellationToken)
        => fileWorkflow.ListPinsAsync(cancellationToken);

    public IReadOnlyList<NodeExplorerItemSnapshot> GetCachedPinnedExplorerItems()
        => explorerWorkflow.GetCachedPinnedExplorerItems();

    public Task<bool> HasTrustedCachedPinnedExplorerItemsAsync(CancellationToken cancellationToken)
        => explorerWorkflow.HasTrustedCachedPinnedExplorerItemsAsync(cancellationToken);

    public Task<IReadOnlyList<NodeExplorerItemSnapshot>> ListPinnedExplorerItemsAsync(CancellationToken cancellationToken)
        => explorerWorkflow.ListPinnedExplorerItemsAsync(cancellationToken);

    public Task<NodeExplorerSnapshot> GetExplorerSnapshotAsync(string path, CancellationToken cancellationToken)
        => explorerWorkflow.GetExplorerSnapshotAsync(path, cancellationToken);

    public Task<NodePreviewSnapshot> GetPreviewSnapshotAsync(string path, string? displayName, CancellationToken cancellationToken)
        => explorerWorkflow.GetPreviewSnapshotAsync(path, displayName, cancellationToken);

    public Task<IReadOnlyList<string>> PinAsync(string path, bool recursive, CancellationToken cancellationToken)
        => fileWorkflow.PinAsync(path, recursive, cancellationToken);

    public Task<IReadOnlyList<string>> UnpinAsync(string cidText, bool recursive, CancellationToken cancellationToken)
        => fileWorkflow.UnpinAsync(cidText, recursive, cancellationToken);

    public Task<NodeBlockSnapshot> GetBlockAsync(string cidText, CancellationToken cancellationToken)
        => contentWorkflow.GetBlockAsync(cidText, cancellationToken);

    public Task<string> PutBlockTextAsync(string text, bool pin, CancellationToken cancellationToken)
        => contentWorkflow.PutBlockTextAsync(text, pin, cancellationToken);

    public Task<string?> RemoveBlockAsync(string cidText, bool ignoreMissing, CancellationToken cancellationToken)
        => contentWorkflow.RemoveBlockAsync(cidText, ignoreMissing, cancellationToken);

    public Task<NodeObjectSnapshot> GetObjectAsync(string cidText, CancellationToken cancellationToken)
        => contentWorkflow.GetObjectAsync(cidText, cancellationToken);

    public Task<string> CreateEmptyDirectoryAsync(CancellationToken cancellationToken)
        => contentWorkflow.CreateEmptyDirectoryAsync(cancellationToken);

    public Task<NodeDagSnapshot> GetDagAsync(string request, CancellationToken cancellationToken)
        => contentWorkflow.GetDagAsync(request, cancellationToken);

    public Task<string> PutDagJsonAsync(string json, bool pin, CancellationToken cancellationToken)
        => contentWorkflow.PutDagJsonAsync(json, pin, cancellationToken);

    public Task<string> ResolveNameAsync(string name, bool recursive, CancellationToken cancellationToken)
        => contentWorkflow.ResolveNameAsync(name, recursive, cancellationToken);

    public Task<NodeNamePublishSnapshot> PublishNameAsync(
        string path,
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
        => contentWorkflow.PublishNameAsync(path, key, lifetime, cancellationToken);

    public Task<IReadOnlyList<NodeKeySnapshot>> ListKeysAsync(CancellationToken cancellationToken)
        => contentWorkflow.ListKeysAsync(cancellationToken);

    public Task<NodeKeySnapshot> CreateKeyAsync(string name, string keyType, int size, CancellationToken cancellationToken)
        => contentWorkflow.CreateKeyAsync(name, keyType, size, cancellationToken);

    public Task<NodeKeySnapshot> RenameKeyAsync(string oldName, string newName, CancellationToken cancellationToken)
        => contentWorkflow.RenameKeyAsync(oldName, newName, cancellationToken);

    public Task<string?> RemoveKeyAsync(string name, CancellationToken cancellationToken)
        => contentWorkflow.RemoveKeyAsync(name, cancellationToken);

    public Task<NodeNetworkSnapshot> GetNetworkSnapshotAsync(CancellationToken cancellationToken)
        => networkWorkflow.GetNetworkSnapshotAsync(cancellationToken);

    public Task ConnectAsync(string address, CancellationToken cancellationToken)
        => networkWorkflow.ConnectAsync(address, cancellationToken);

    public Task<ResolvedPeerConnectionTarget> ConnectByKnownNodeApiAsync(
        string hostOrApiUrl,
        int apiPort,
        int swarmPort,
        CancellationToken cancellationToken)
        => networkWorkflow.ConnectByKnownNodeApiAsync(hostOrApiUrl, apiPort, swarmPort, cancellationToken);

    public Task DisconnectAsync(string address, CancellationToken cancellationToken)
        => networkWorkflow.DisconnectAsync(address, cancellationToken);

    public Task<string?> AddBootstrapAsync(string address, CancellationToken cancellationToken)
        => networkWorkflow.AddBootstrapAsync(address, cancellationToken);

    public Task<string?> RemoveBootstrapAsync(string address, CancellationToken cancellationToken)
        => networkWorkflow.RemoveBootstrapAsync(address, cancellationToken);

    public Task<IReadOnlyList<string>> RestoreDefaultBootstrapAsync(CancellationToken cancellationToken)
        => networkWorkflow.RestoreDefaultBootstrapAsync(cancellationToken);

    public Task RemoveAllBootstrapAsync(CancellationToken cancellationToken)
        => networkWorkflow.RemoveAllBootstrapAsync(cancellationToken);

    public Task<string?> AddAddressFilterAsync(string address, CancellationToken cancellationToken)
        => networkWorkflow.AddAddressFilterAsync(address, cancellationToken);

    public Task<string?> RemoveAddressFilterAsync(string address, CancellationToken cancellationToken)
        => networkWorkflow.RemoveAddressFilterAsync(address, cancellationToken);

    public Task<NodePeerSnapshot> FindPeerAsync(string peerId, CancellationToken cancellationToken)
        => networkWorkflow.FindPeerAsync(peerId, cancellationToken);

    public Task<IReadOnlyList<NodePeerSnapshot>> FindProvidersAsync(string cidText, int limit, CancellationToken cancellationToken)
        => networkWorkflow.FindProvidersAsync(cidText, limit, cancellationToken);

    public Task<IReadOnlyList<NodePeerSnapshot>> ListPubSubPeersAsync(string? topic, CancellationToken cancellationToken)
        => networkWorkflow.ListPubSubPeersAsync(topic, cancellationToken);

    public Task PublishPubSubAsync(string topic, string message, CancellationToken cancellationToken)
        => networkWorkflow.PublishPubSubAsync(topic, message, cancellationToken);

    public Task<string> GetFullConfigAsync(CancellationToken cancellationToken)
        => maintenanceWorkflow.GetFullConfigAsync(cancellationToken);

    public Task<string> GetConfigValueAsync(string key, CancellationToken cancellationToken)
        => maintenanceWorkflow.GetConfigValueAsync(key, cancellationToken);

    public Task SetConfigValueAsync(string key, string value, bool treatAsJson, CancellationToken cancellationToken)
        => maintenanceWorkflow.SetConfigValueAsync(key, value, treatAsJson, cancellationToken);

    public Task<string> GetRepositoryVersionAsync(CancellationToken cancellationToken)
        => maintenanceWorkflow.GetRepositoryVersionAsync(cancellationToken);

    public Task RunRepositoryGcAsync(CancellationToken cancellationToken)
        => maintenanceWorkflow.RunRepositoryGcAsync(cancellationToken);

    public Task VerifyRepositoryAsync(CancellationToken cancellationToken)
        => maintenanceWorkflow.VerifyRepositoryAsync(cancellationToken);

    public Task ShutdownNodeAsync()
        => maintenanceWorkflow.ShutdownNodeAsync();
}
