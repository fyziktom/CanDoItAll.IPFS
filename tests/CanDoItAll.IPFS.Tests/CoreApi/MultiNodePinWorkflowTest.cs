using Microsoft.VisualStudio.TestTools.UnitTesting;
using PeerTalk;
using PeerTalk.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Engine
{
    [TestClass]
    public class MultiNodePinWorkflowTest
    {
        [TestMethod]
        public async Task MultiNode_Pin_Share_Read_Remove_Workflow()
        {
            var notes = new List<string>();
            var content = $"multi-node pin workflow {Guid.NewGuid():N}";
            var filePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);

            try
            {
                using var bootstrap = new TempNode();
                using var origin = new TempNode();
                using var pinner = new TempNode();
                using var reader = new TempNode();

                var psk = new PreSharedKey().Generate();

                await ConfigureNodeAsync(bootstrap, Array.Empty<MultiAddress>(), psk).ConfigureAwait(false);
                var bootstrapPeers = new[]
                {
                    (await bootstrap.LocalPeer.ConfigureAwait(false)).Addresses.First()
                };

                await ConfigureNodeAsync(origin, bootstrapPeers, psk).ConfigureAwait(false);
                await ConfigureNodeAsync(pinner, bootstrapPeers, psk).ConfigureAwait(false);
                await ConfigureNodeAsync(reader, bootstrapPeers, psk).ConfigureAwait(false);

                var added = await origin.FileSystem.AddFileAsync(filePath).ConfigureAwait(false);
                var cid = added.Id;
                notes.Add($"origin add: {cid}");
                notes.Add(
                    await bootstrap.Block.StatAsync(cid).ConfigureAwait(false) is null
                        ? "bootstrap after origin add: root block absent locally"
                        : "bootstrap after origin add: root block present locally");

                Assert.IsTrue((await origin.Pin.ListAsync().ConfigureAwait(false)).Any(pin => pin == cid), "Origin should pin the added file.");
                Assert.IsNull(await pinner.Block.StatAsync(cid).ConfigureAwait(false), "Second node should not have the file before pinning by CID.");

                var pinnedOnSecondNode = (await pinner.Pin.AddAsync(cid).ConfigureAwait(false)).ToArray();
                Assert.IsTrue(pinnedOnSecondNode.Any(pin => pin == cid), "Second node should pin the CID fetched from the swarm.");
                Assert.IsTrue((await pinner.Pin.ListAsync().ConfigureAwait(false)).Any(pin => pin == cid), "Second node should list the remote-pinned CID.");
                Assert.IsNotNull(await pinner.Block.StatAsync(cid).ConfigureAwait(false), "Second node should have the file after pinning.");
                notes.Add("second node pin by CID: fetched content from swarm without local re-add");

                Assert.IsFalse((await reader.Pin.ListAsync().ConfigureAwait(false)).Any(pin => pin == cid), "Reader node should start unpinned.");
                var readerBlockBeforeRead = await reader.Block.StatAsync(cid).ConfigureAwait(false);
                notes.Add(
                    readerBlockBeforeRead is null
                        ? "third node before explicit read: root block absent locally"
                        : "third node before explicit read: root block already present locally while still unpinned");

                var firstRead = await ReadAllTextAsync(reader, cid, 30).ConfigureAwait(false);
                Assert.AreEqual(content, firstRead, "Reader node should fetch content without pinning it.");
                Assert.IsFalse((await reader.Pin.ListAsync().ConfigureAwait(false)).Any(pin => pin == cid), "Reader node fetch should not pin the file.");
                Assert.IsNotNull(await reader.Block.StatAsync(cid).ConfigureAwait(false), "Reader node should cache the fetched file.");
                notes.Add("third node read without pin: success and CID remained unpinned");

                await reader.BlockRepository.RemoveGarbageAsync().ConfigureAwait(false);
                Assert.IsNull(await reader.Block.StatAsync(cid).ConfigureAwait(false), "Reader cache should be removed by garbage collection when unpinned.");

                var removedFromOrigin = (await origin.Pin.RemoveAsync(cid).ConfigureAwait(false)).ToArray();
                Assert.IsTrue(removedFromOrigin.Any(pin => pin == cid), "Origin should unpin the file.");
                await origin.BlockRepository.RemoveGarbageAsync().ConfigureAwait(false);
                Assert.IsNull(await origin.Block.StatAsync(cid).ConfigureAwait(false), "Origin should remove the file after unpin plus garbage collection.");
                await origin.StopAsync().ConfigureAwait(false);
                notes.Add("origin unpin + GC: local copy removed");

                var secondRead = await ReadAllTextAsync(reader, cid, 30).ConfigureAwait(false);
                Assert.AreEqual(content, secondRead, "Reader should still fetch the file from the second node after origin removal.");
                notes.Add("read after origin removal: served from second node pinned copy");

                await reader.BlockRepository.RemoveGarbageAsync().ConfigureAwait(false);
                Assert.IsNull(await reader.Block.StatAsync(cid).ConfigureAwait(false), "Reader cache should be empty again before final removal.");

                var removedFromSecondNode = (await pinner.Pin.RemoveAsync(cid).ConfigureAwait(false)).ToArray();
                Assert.IsTrue(removedFromSecondNode.Any(pin => pin == cid), "Second node should unpin the file.");
                await pinner.BlockRepository.RemoveGarbageAsync().ConfigureAwait(false);
                Assert.IsNull(await pinner.Block.StatAsync(cid).ConfigureAwait(false), "Second node should remove the file after unpin plus garbage collection.");
                notes.Add("second node unpin + GC: pinned copy removed");

                var bootstrapBlockAfterCleanup = await bootstrap.Block.StatAsync(cid).ConfigureAwait(false);
                var readerBlockAfterCleanup = await reader.Block.StatAsync(cid).ConfigureAwait(false);
                notes.Add(
                    $"after explicit cleanup: bootstrapHasRoot={(bootstrapBlockAfterCleanup is not null)}, readerHasRoot={(readerBlockAfterCleanup is not null)}");

                await bootstrap.StopAsync().ConfigureAwait(false);
                notes.Add("bootstrap stopped before final fetch probe");

                Exception finalFetchFailure = null;
                var finalFetchSucceeded = false;
                try
                {
                    var finalRead = await ReadAllTextAsync(reader, cid, 10).ConfigureAwait(false);
                    Assert.AreEqual(content, finalRead, "Final fetch should still return the original content if it succeeds.");
                    finalFetchSucceeded = true;
                }
                catch (Exception ex)
                {
                    finalFetchFailure = ex;
                }
                notes.Add(
                    finalFetchSucceeded
                        ? "final read after all explicit removals: still succeeded"
                        : $"final read after all explicit removals: failed with {finalFetchFailure.GetType().Name}");

                foreach (var note in notes)
                {
                    Console.WriteLine(note);
                }
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        private static async Task ConfigureNodeAsync(TempNode node, MultiAddress[] bootstrapPeers, PreSharedKey privateNetworkKey)
        {
            node.Options.Discovery.DisableMdns = true;
            node.Options.Discovery.BootstrapPeers = bootstrapPeers;
            node.Options.Swarm.MinConnections = 0;
            node.Options.Swarm.PrivateNetworkKey = privateNetworkKey;

            await node.StartAsync().ConfigureAwait(false);
            if (bootstrapPeers.Length != 0)
            {
                await node.Swarm.ConnectAsync(bootstrapPeers[0]).ConfigureAwait(false);
            }
        }

        private static async Task<string> ReadAllTextAsync(TempNode node, Cid cid, int timeoutSeconds)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            return await node.FileSystem.ReadAllTextAsync(cid, cts.Token).ConfigureAwait(false);
        }
    }
}
