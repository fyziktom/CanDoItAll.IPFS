using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeMaintenanceWorkflowService(IpfsClientFactory clientFactory) : INodeMaintenanceWorkflow
{
    private static readonly IReadOnlyList<NodeCapabilityNote> CapabilityNotes =
    [
        new("Single file upload", "Available", "success", "Uploads go through the existing add route and can be pinned or wrapped."),
        new("Directory upload", "Available", "success", "Folder uploads now preserve nested files and subfolders through the add route."),
        new("CID and DAG browsing", "Available", "success", "file/ls, cat, object, and dag routes are wired into the UI."),
        new("Pin and unpin", "Available", "success", "pin/add, pin/ls, and pin/rm are exposed."),
        new("Block and object management", "Available", "success", "block get/put/rm/stat and object get/data/stat are exposed."),
        new("IPNS and keys", "Partial", "warning", "Key create/list/remove/rename and name publish/resolve work. Key import/export stays intentionally unavailable until the engine host exposes secure support."),
        new("Swarm and bootstrap", "Available", "success", "Peers, known addresses, connect, disconnect, filters, and bootstrap management are supported."),
        new("DHT tools", "Partial", "warning", "findpeer and findprovs work, but provide and DHT value storage routes are not exposed."),
        new("PubSub", "Available", "success", "Topic listing, peer lookup, publish, and live subscribe can run from the UI."),
        new("Node maintenance", "Partial", "warning", "Config read/write, repo gc, repo version, and shutdown are supported. Repo verify remains unavailable because the current HTTP surface returns 501.")
    ];

    public IReadOnlyList<NodeCapabilityNote> GetCapabilityNotes() => CapabilityNotes;

    public async Task<string> GetFullConfigAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = await lease.Client.Config.GetAsync(cancellationToken).ConfigureAwait(false);
        return token.ToString(Formatting.Indented);
    }

    public async Task<string> GetConfigValueAsync(string key, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = await lease.Client.Config.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return token.ToString(Formatting.Indented);
    }

    public async Task SetConfigValueAsync(string key, string value, bool treatAsJson, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        if (treatAsJson)
        {
            await lease.Client.Config.SetAsync(key, JToken.Parse(value), cancellationToken).ConfigureAwait(false);
            return;
        }

        await lease.Client.Config.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetRepositoryVersionAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Client.BlockRepository.VersionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RunRepositoryGcAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateAdminLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.BlockRepository.RemoveGarbageAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task VerifyRepositoryAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateAdminLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Client.BlockRepository.VerifyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ShutdownNodeAsync()
    {
        using var lease = await CreateAdminLeaseAsync(CancellationToken.None).ConfigureAwait(false);
        await lease.Client.Generic.ShutdownAsync().ConfigureAwait(false);
    }

    private Task<IpfsClientLease> CreateReadLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, cancellationToken);

    private Task<IpfsClientLease> CreateMutationLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Mutation, cancellationToken);

    private Task<IpfsClientLease> CreateAdminLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Admin, cancellationToken);
}
