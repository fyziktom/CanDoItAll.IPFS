using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.Client.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Ipfs.Engine.ClientTests
{
    [TestClass]
    public class IpfsEngineClientContractTests
    {
        [TestMethod]
        public async Task Basic_Roundtrip_Operations_Work_Over_Http()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);

            var version = await host.Client.Generic.VersionAsync().ConfigureAwait(false);
            Assert.IsTrue(version.Count > 0);

            var peer = await host.Client.Generic.IdAsync().ConfigureAwait(false);
            Assert.IsNotNull(peer.Id);
            Assert.IsTrue(peer.Addresses.Any());

            await host.Client.Config.SetAsync("ClientTest.Value", "alpha").ConfigureAwait(false);
            var configValue = await host.Client.Config.GetAsync("ClientTest.Value").ConfigureAwait(false);
            Assert.AreEqual("alpha", configValue.ToString());

            var config = await host.Client.Config.GetAsync().ConfigureAwait(false);
            config["ClientTestReplace"] = "beta";
            await host.Client.Config.ReplaceAsync(config).ConfigureAwait(false);
            var replaced = await host.Client.Config.GetAsync("ClientTestReplace").ConfigureAwait(false);
            Assert.AreEqual("beta", replaced.ToString());

            var fileNode = await host.Client.FileSystem.AddTextAsync("hello ipfs client").ConfigureAwait(false);
            Assert.IsNotNull(fileNode.Id);
            Assert.AreEqual("hello ipfs client", await host.Client.FileSystem.ReadAllTextAsync(fileNode.Id.ToString()).ConfigureAwait(false));

            using (var range = await host.Client.FileSystem.ReadFileAsync(fileNode.Id.ToString(), 6, 4).ConfigureAwait(false))
            using (var reader = new StreamReader(range, Encoding.UTF8))
            {
                Assert.AreEqual("ipfs", await reader.ReadToEndAsync().ConfigureAwait(false));
            }

            var listed = await host.Client.FileSystem.ListFileAsync(fileNode.Id.ToString()).ConfigureAwait(false);
            Assert.AreEqual(fileNode.Id, listed.Id);

            using (var tar = await host.Client.FileSystem.GetAsync(fileNode.Id.ToString()).ConfigureAwait(false))
            {
                Assert.IsTrue(tar.CanRead);
            }

            var blockId = await host.Client.Block.PutAsync(Encoding.UTF8.GetBytes("raw-block"), contentType: "raw").ConfigureAwait(false);
            var blockStat = await host.Client.Block.StatAsync(blockId).ConfigureAwait(false);
            Assert.AreEqual(9L, blockStat.Size);
            var block = await host.Client.Block.GetAsync(blockId).ConfigureAwait(false);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("raw-block"), block.DataBytes);

            var removedBlock = await host.Client.Block.RemoveAsync(blockId).ConfigureAwait(false);
            Assert.AreEqual(blockId, removedBlock);

            var dagId = await host.Client.Dag.PutAsync(JObject.Parse("{\"alpha\":1,\"beta\":\"two\"}"), pin: true).ConfigureAwait(false);
            var dag = await host.Client.Dag.GetAsync(dagId).ConfigureAwait(false);
            Assert.AreEqual(1, dag["alpha"]!.Value<int>());
            CollectionAssert.Contains((await host.Client.Pin.ListAsync().ConfigureAwait(false)).ToArray(), dagId);

            var alpha = new DagNode(Encoding.UTF8.GetBytes("alpha"));
            var beta = new DagNode(Encoding.UTF8.GetBytes("beta"), new[] { alpha.ToLink("child") });
            var storedObject = await host.Client.Object.PutAsync(beta).ConfigureAwait(false);
            var fetchedObject = await host.Client.Object.GetAsync(storedObject.Id).ConfigureAwait(false);
            CollectionAssert.AreEqual(beta.DataBytes, fetchedObject.DataBytes);
            Assert.AreEqual(1, fetchedObject.Links.Count());
            Assert.AreEqual("child", fetchedObject.Links.First().Name);

            using (var objectData = await host.Client.Object.DataAsync(storedObject.Id).ConfigureAwait(false))
            using (var objectReader = new StreamReader(objectData, Encoding.UTF8))
            {
                Assert.AreEqual("beta", await objectReader.ReadToEndAsync().ConfigureAwait(false));
            }

            var objectStat = await host.Client.Object.StatAsync(storedObject.Id).ConfigureAwait(false);
            Assert.AreEqual(1, objectStat.LinkCount);
            Assert.AreEqual(4L, objectStat.DataSize);

            var emptyObject = await host.Client.Object.NewAsync().ConfigureAwait(false);
            Assert.IsNotNull(emptyObject.Id);
            var emptyDirectory = await host.Client.Object.NewDirectoryAsync().ConfigureAwait(false);
            Assert.IsNotNull(emptyDirectory.Id);

            var pinned = await host.Client.Pin.AddAsync(fileNode.Id.ToString()).ConfigureAwait(false);
            CollectionAssert.Contains(pinned.ToArray(), fileNode.Id);
            var pinList = await host.Client.Pin.ListAsync().ConfigureAwait(false);
            CollectionAssert.Contains(pinList.ToArray(), fileNode.Id);
            var unpinned = await host.Client.Pin.RemoveAsync(fileNode.Id).ConfigureAwait(false);
            CollectionAssert.Contains(unpinned.ToArray(), fileNode.Id);

            var repoStats = await host.Client.BlockRepository.StatisticsAsync().ConfigureAwait(false);
            Assert.IsTrue(repoStats.RepoSize > 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(await host.Client.BlockRepository.VersionAsync().ConfigureAwait(false)));
            await host.Client.BlockRepository.RemoveGarbageAsync().ConfigureAwait(false);

            await host.Client.Key.CreateAsync("client-test-key", "ed25519", 0).ConfigureAwait(false);
            var keys = await host.Client.Key.ListAsync().ConfigureAwait(false);
            Assert.IsTrue(keys.Any(k => k.Name == "client-test-key"));
            var renamedKey = await host.Client.Key.RenameAsync("client-test-key", "client-test-key-2").ConfigureAwait(false);
            Assert.AreEqual("client-test-key-2", renamedKey.Name);
            var removedKey = await host.Client.Key.RemoveAsync("client-test-key-2").ConfigureAwait(false);
            Assert.IsNotNull(removedKey);

            var filter = (MultiAddress)"/ip4/127.0.0.1";
            Assert.AreEqual(filter, await host.Client.Swarm.AddAddressFilterAsync(filter).ConfigureAwait(false));
            var filters = await host.Client.Swarm.ListAddressFiltersAsync().ConfigureAwait(false);
            Assert.IsTrue(filters.Any(x => x == filter));
            Assert.AreEqual(filter, await host.Client.Swarm.RemoveAddressFilterAsync(filter).ConfigureAwait(false));
            var addresses = await host.Client.Swarm.AddressesAsync().ConfigureAwait(false);
            Assert.IsTrue(addresses.All(p => p.Id != null));

            var bandwidth = await host.Client.Stats.BandwidthAsync().ConfigureAwait(false);
            Assert.IsNotNull(bandwidth);
            var bitswap = await host.Client.Stats.BitswapAsync().ConfigureAwait(false);
            Assert.IsNotNull(bitswap);
            var repo = await host.Client.Stats.RepositoryAsync().ConfigureAwait(false);
            Assert.IsTrue(repo.RepoSize > 0);

            var bootstrapDefaults = await host.Client.Bootstrap.AddDefaultsAsync().ConfigureAwait(false);
            Assert.IsTrue(bootstrapDefaults.Any());
            var bootstrapList = await host.Client.Bootstrap.ListAsync().ConfigureAwait(false);
            Assert.IsTrue(bootstrapList.Any());
            await host.Client.Bootstrap.RemoveAllAsync().ConfigureAwait(false);
            Assert.AreEqual(0, (await host.Client.Bootstrap.ListAsync().ConfigureAwait(false)).Count());
        }

        [TestMethod]
        public async Task FileSystem_AddDirectoryAsync_Uploads_Nested_Files_And_Empty_Folders_Over_Http()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);

            var rootPath = Path.Combine(Path.GetTempPath(), $"ipfs-http-dir-{Guid.NewGuid():N}");
            Directory.CreateDirectory(rootPath);
            Directory.CreateDirectory(Path.Combine(rootPath, "nested"));
            Directory.CreateDirectory(Path.Combine(rootPath, "empty"));
            await File.WriteAllTextAsync(Path.Combine(rootPath, "root.txt"), "root upload", Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(rootPath, "nested", "child.txt"), "nested upload", Encoding.UTF8).ConfigureAwait(false);

            try
            {
                var uploaded = await host.Client.FileSystem.AddDirectoryAsync(rootPath, recursive: true).ConfigureAwait(false);

                Assert.IsTrue(uploaded.IsDirectory);
                Assert.AreEqual("root upload", await host.Client.FileSystem.ReadAllTextAsync($"{uploaded.Id}/root.txt").ConfigureAwait(false));
                Assert.AreEqual("nested upload", await host.Client.FileSystem.ReadAllTextAsync($"{uploaded.Id}/nested/child.txt").ConfigureAwait(false));

                var emptyFolder = await host.Client.FileSystem.ListFileAsync($"{uploaded.Id}/empty").ConfigureAwait(false);
                Assert.IsTrue(emptyFolder.IsDirectory);
            }
            finally
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }

        [TestMethod]
        public async Task Real_Startup_Serves_Core_Endpoints_Without_Runtime_Xml_Doc_File()
        {
            await using var host = await RealStartupIpfsHttpHost.StartAsync().ConfigureAwait(false);

            var version = await host.Client.Generic.VersionAsync().ConfigureAwait(false);
            Assert.IsTrue(version.Count > 0);

            var peer = await host.Client.Generic.IdAsync().ConfigureAwait(false);
            Assert.IsNotNull(peer.Id);
            Assert.IsTrue(peer.Addresses.Any());
        }

        [TestMethod]
        public async Task PubSub_String_Subscribe_Works_Over_Http()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
            var topic = Guid.NewGuid().ToString("N");
            var received = new TaskCompletionSource<IPublishedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var subscription = host.Client.PubSub.SubscribeAsync(topic, message => received.TrySetResult(message), cts.Token);
            await Task.Delay(200).ConfigureAwait(false);

            var topics = await host.Client.PubSub.SubscribedTopicsAsync().ConfigureAwait(false);
            CollectionAssert.Contains(topics.ToArray(), topic);

            await host.Client.PubSub.PublishAsync(topic, "hello world").ConfigureAwait(false);
            var published = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hello world"), published.DataBytes);
            CollectionAssert.Contains(published.Topics.ToArray(), topic);

            cts.Cancel();
            await subscription.ConfigureAwait(false);
        }

        [TestMethod]
        public async Task Swarm_And_Bitswap_Peer_Operations_Work_Over_Http()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
            using var remote = new TempNode();
            remote.Options.Discovery.DisableMdns = true;
            remote.Options.Discovery.DisableRandomWalk = true;
            await remote.Bootstrap.RemoveAllAsync().ConfigureAwait(false);
            await remote.StartAsync().ConfigureAwait(false);

            var remotePeer = await remote.Generic.IdAsync().ConfigureAwait(false);
            var remoteAddress = await TestIpfsHttpHost.GetDialAddressAsync(remote).ConfigureAwait(false);

            await host.Client.Swarm.ConnectAsync(remoteAddress).ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);

            var peers = await host.Client.Swarm.PeersAsync().ConfigureAwait(false);
            Assert.IsTrue(peers.Any(p => p.Id == remotePeer.Id));

            var foundPeer = await host.Client.Dht.FindPeerAsync(remotePeer.Id).ConfigureAwait(false);
            Assert.AreEqual(remotePeer.Id, foundPeer.Id);
            Assert.IsTrue(foundPeer.Addresses.Any());

            var wants = await host.Client.Bitswap.WantsAsync().ConfigureAwait(false);
            Assert.IsNotNull(wants);
            var ledger = await host.Client.Bitswap.LedgerAsync(new Peer { Id = remotePeer.Id }).ConfigureAwait(false);
            Assert.AreEqual(remotePeer.Id, ledger.Peer.Id);
            await host.Client.Bitswap.UnwantAsync(Cid.Decode("QmYwAPJzv5CZsnAzt8auV2J9m9wM4BvP64QekJy5oQ1gnS")).ConfigureAwait(false);

            await host.Client.Swarm.DisconnectAsync(remoteAddress).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task Unsupported_And_Server_Gap_Functions_Are_Explicit()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);

            await ThrowsAsync<NotSupportedException>(() => host.Client.Generic.PingAsync((MultiHash)"QmYwAPJzv5CZsnAzt8auV2J9m9wM4BvP64QekJy5oQ1gnS")).ConfigureAwait(false);
            await ThrowsAsync<NotSupportedException>(() => host.Client.Bitswap.GetAsync(Cid.Decode("QmYwAPJzv5CZsnAzt8auV2J9m9wM4BvP64QekJy5oQ1gnS"))).ConfigureAwait(false);
            await ThrowsAsync<NotSupportedException>(() => host.Client.Dht.ProvideAsync(Cid.Decode("QmYwAPJzv5CZsnAzt8auV2J9m9wM4BvP64QekJy5oQ1gnS"))).ConfigureAwait(false);
            await ThrowsAsync<NotSupportedException>(() => host.Client.Key.ExportAsync("self", "pw".ToCharArray())).ConfigureAwait(false);
            await ThrowsAsync<NotSupportedException>(() => host.Client.PubSub.PublishAsync("topic", new byte[] { 1, 2, 3 })).ConfigureAwait(false);
            await ThrowsAsync<NotSupportedException>(() => host.Client.Swarm.ListAddressFiltersAsync(persist: true)).ConfigureAwait(false);

            var verifyException = await ThrowsAsync<IpfsApiException>(() => host.Client.BlockRepository.VerifyAsync()).ConfigureAwait(false);
            Assert.AreEqual(System.Net.HttpStatusCode.NotImplemented, verifyException.StatusCode);
        }

        [TestMethod]
        public async Task Public_Dns_And_Name_Resolution_Work_Over_Http()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);

            var dnsPath = await host.Client.Dns.ResolveAsync("ipfs.io", recursive: true).ConfigureAwait(false);
            StringAssert.StartsWith(dnsPath, "/ipfs/");

            var namePath = await host.Client.Name.ResolveAsync("ipfs.io", recursive: true).ConfigureAwait(false);
            StringAssert.StartsWith(namePath, "/ipfs/");
        }

        [TestMethod]
        public async Task Name_Publish_And_Resolve_Work_Over_Http()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);

            var file = await host.Client.FileSystem.AddTextAsync("http ipns proof").ConfigureAwait(false);
            var published = await host.Client.Name.PublishAsync($"/ipfs/{file.Id}").ConfigureAwait(false);

            StringAssert.StartsWith(published.NamePath, "/ipns/");
            Assert.AreEqual($"/ipfs/{file.Id}", published.ContentPath);

            var resolved = await host.Client.Name.ResolveAsync(published.NamePath, recursive: true).ConfigureAwait(false);
            Assert.AreEqual($"/ipfs/{file.Id}", resolved);
        }

        private static async Task<T> ThrowsAsync<T>(Func<Task> action)
            where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T ex)
            {
                return ex;
            }

            Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
            return null!;
        }
    }
}
