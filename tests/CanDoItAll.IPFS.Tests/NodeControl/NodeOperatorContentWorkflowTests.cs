using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeOperatorContentWorkflowTests
{
    [TestMethod]
    public async Task BlockObjectAndDag_Workflows_Roundtrip_Through_NodeOperatorService()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        var blockCid = await service.PutBlockTextAsync("content block proof", pin: false, CancellationToken.None).ConfigureAwait(false);
        var block = await service.GetBlockAsync(blockCid, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(blockCid, block.Cid);
        Assert.AreEqual("content block proof", block.Utf8Preview);

        var removedBlock = await service.RemoveBlockAsync(blockCid, ignoreMissing: true, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(blockCid, removedBlock);

        var directoryCid = await service.CreateEmptyDirectoryAsync(CancellationToken.None).ConfigureAwait(false);
        var directoryObject = await service.GetObjectAsync(directoryCid, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(directoryCid, directoryObject.Cid);
        Assert.AreEqual(0, directoryObject.LinkCount);
        Assert.AreEqual(0, directoryObject.Links.Count);

        var dagCid = await service.PutDagJsonAsync("{\"story\":\"content\",\"value\":42}", pin: true, CancellationToken.None).ConfigureAwait(false);
        var dag = await service.GetDagAsync(dagCid, CancellationToken.None).ConfigureAwait(false);
        StringAssert.Contains(dag.Json, "\"story\": \"content\"");
        StringAssert.Contains(dag.Json, "\"value\": 42");
    }

    [TestMethod]
    public async Task KeyLifecycle_And_IpnsPublishResolve_Work_Through_NodeOperatorService()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var keyName = $"bundle-key-{Guid.NewGuid():N}";
        var renamedKey = $"{keyName}-renamed";

        try
        {
            var createdKey = await service.CreateKeyAsync(keyName, "rsa", 512, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(keyName, createdKey.Name);

            var renamed = await service.RenameKeyAsync(keyName, renamedKey, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(renamedKey, renamed.Name);

            CollectionAssert.Contains(
                (await service.ListKeysAsync(CancellationToken.None).ConfigureAwait(false)).Select(key => key.Name).ToArray(),
                renamedKey);

            var file = await service.UploadTextAsync("ipns-note.txt", "ipns publish proof", pin: true, wrap: false, CancellationToken.None).ConfigureAwait(false);
            var published = await service.PublishNameAsync($"/ipfs/{file.ResolvedId}", renamedKey, TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);
            StringAssert.StartsWith(published.NamePath, "/ipns/");
            Assert.AreEqual($"/ipfs/{file.ResolvedId}", published.ContentPath);

            var resolved = await service.ResolveNameAsync(published.NamePath, recursive: true, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual($"/ipfs/{file.ResolvedId}", resolved);
        }
        finally
        {
            await service.RemoveKeyAsync(renamedKey, CancellationToken.None).ConfigureAwait(false);
            await service.RemoveKeyAsync(keyName, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
