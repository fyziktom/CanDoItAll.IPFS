using System.Text;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Ipfs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeContentWorkflowService(IpfsClientFactory clientFactory) : INodeContentWorkflow
{
    public async Task<NodeBlockSnapshot> GetBlockAsync(string cidText, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var block = await lease.Client.Block.GetAsync(Cid.Decode(cidText), cancellationToken).ConfigureAwait(false);
        var bytes = block.DataBytes;

        return new NodeBlockSnapshot
        {
            Cid = block.Id.ToString(),
            Size = block.Size,
            Utf8Preview = NodePreviewTextReader.GetUtf8Preview(bytes, 4096),
            Base64Preview = Convert.ToBase64String(bytes.Length > 768 ? bytes[..768] : bytes)
        };
    }

    public async Task<string> PutBlockTextAsync(string text, bool pin, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var cid = await lease.Client.Block.PutAsync(Encoding.UTF8.GetBytes(text ?? string.Empty), pin: pin, cancel: cancellationToken).ConfigureAwait(false);
        return cid.ToString();
    }

    public async Task<string?> RemoveBlockAsync(string cidText, bool ignoreMissing, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var removed = await lease.Client.Block.RemoveAsync(Cid.Decode(cidText), ignoreMissing, cancellationToken).ConfigureAwait(false);
        return removed?.ToString();
    }

    public async Task<NodeObjectSnapshot> GetObjectAsync(string cidText, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var cid = Cid.Decode(cidText);
        var node = await lease.Client.Object.GetAsync(cid, cancellationToken).ConfigureAwait(false);
        var stat = await lease.Client.Object.StatAsync(cid, cancellationToken).ConfigureAwait(false);
        using var dataStream = await lease.Client.Object.DataAsync(cid, cancellationToken).ConfigureAwait(false);

        return new NodeObjectSnapshot
        {
            Cid = cid.ToString(),
            LinkCount = stat.LinkCount,
            LinkSize = stat.LinkSize,
            BlockSize = stat.BlockSize,
            DataSize = stat.DataSize,
            CumulativeSize = stat.CumulativeSize,
            DataPreview = await NodePreviewTextReader.ReadUtf8PreviewAsync(dataStream, 4096, cancellationToken).ConfigureAwait(false),
            Links = node.Links
                .Select(link => new NodeLinkSnapshot(link.Name, link.Id.ToString(), link.Size, link.Size, false, 0))
                .ToList()
        };
    }

    public async Task<string> CreateEmptyDirectoryAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var node = await lease.Client.Object.NewDirectoryAsync(cancellationToken).ConfigureAwait(false);
        return node.Id.ToString();
    }

    public async Task<NodeDagSnapshot> GetDagAsync(string request, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = await lease.Client.Dag.GetAsync(request, cancellationToken).ConfigureAwait(false);
        return new NodeDagSnapshot
        {
            Request = request,
            Json = token.ToString(Formatting.Indented)
        };
    }

    public async Task<string> PutDagJsonAsync(string json, bool pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("A DAG payload is required.");
        }

        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var token = JToken.Parse(json);
        var objectValue = token as JObject ?? new JObject { ["value"] = token };
        var cid = await lease.Client.Dag.PutAsync(objectValue, pin: pin, cancel: cancellationToken).ConfigureAwait(false);
        return cid.ToString();
    }

    public async Task<string> ResolveNameAsync(string name, bool recursive, CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Client.Name.ResolveAsync(name, recursive, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NodeNamePublishSnapshot> PublishNameAsync(
        string path,
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var result = await lease.Client.Name.PublishAsync(path, resolve: true, key: key, lifetime: lifetime, cancel: cancellationToken).ConfigureAwait(false);
        return new NodeNamePublishSnapshot
        {
            NamePath = result.NamePath,
            ContentPath = result.ContentPath
        };
    }

    public async Task<IReadOnlyList<NodeKeySnapshot>> ListKeysAsync(CancellationToken cancellationToken)
    {
        using var lease = await CreateReadLeaseAsync(cancellationToken).ConfigureAwait(false);
        return (await lease.Client.Key.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(key => new NodeKeySnapshot(key.Name, key.Id.ToString()))
            .OrderBy(key => key.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<NodeKeySnapshot> CreateKeyAsync(string name, string keyType, int size, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var key = await lease.Client.Key.CreateAsync(name, keyType, size, cancellationToken).ConfigureAwait(false);
        return new NodeKeySnapshot(key.Name, key.Id.ToString());
    }

    public async Task<NodeKeySnapshot> RenameKeyAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var key = await lease.Client.Key.RenameAsync(oldName, newName, cancellationToken).ConfigureAwait(false);
        return new NodeKeySnapshot(key.Name, key.Id.ToString());
    }

    public async Task<string?> RemoveKeyAsync(string name, CancellationToken cancellationToken)
    {
        using var lease = await CreateMutationLeaseAsync(cancellationToken).ConfigureAwait(false);
        var removed = await lease.Client.Key.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
        return removed?.Name;
    }

    private Task<IpfsClientLease> CreateReadLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, cancellationToken);

    private Task<IpfsClientLease> CreateMutationLeaseAsync(CancellationToken cancellationToken)
        => clientFactory.CreateLeaseAsync(NodeConnectionRequestCategory.Mutation, cancellationToken);
}
