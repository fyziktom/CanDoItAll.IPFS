using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Ipfs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Ipfs.Engine
{
    [TestClass]
    public class LocalhostFileValidationTest
    {
        private static string TestFilesDirectory => Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "inputs",
                "testfiles"));

        [TestMethod]
        public async Task SampleFiles_RoundTrip_OnSingleLocalNode()
        {
            using var node = new TempNode();
            await ConfigureLocalOnlyNodeAsync(node).ConfigureAwait(false);

            try
            {
                var files = Directory
                    .EnumerateFiles(TestFilesDirectory)
                    .OrderBy(Path.GetFileName)
                    .ToArray();

                Assert.AreEqual(3, files.Length, "Expected the repository sample file set to contain exactly three files.");

                foreach (var file in files)
                {
                    var expectedBytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
                    var added = await node.FileSystem.AddFileAsync(file).ConfigureAwait(false);
                    var listed = await node.FileSystem.ListFileAsync(added.Id).ConfigureAwait(false);

                    await using var stream = await node.FileSystem.ReadFileAsync(added.Id).ConfigureAwait(false);
                    await using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer).ConfigureAwait(false);
                    var actualBytes = buffer.ToArray();

                    Assert.AreEqual(expectedBytes.Length, listed.Size, $"Stored size mismatch for '{Path.GetFileName(file)}'.");
                    Assert.AreEqual(
                        Convert.ToHexString(SHA256.HashData(expectedBytes)),
                        Convert.ToHexString(SHA256.HashData(actualBytes)),
                        $"Content hash mismatch for '{Path.GetFileName(file)}'.");
                }
            }
            finally
            {
                await node.StopAsync().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task DuplicateLargeFileUpload_DoesNotCreateAdditionalBlocks()
        {
            using var node = new TempNode();
            await ConfigureLocalOnlyNodeAsync(node).ConfigureAwait(false);

            try
            {
                var file = Path.Combine(TestFilesDirectory, "video.mkv");
                Assert.IsTrue(File.Exists(file), "The duplicate-upload validation file is missing.");

                var first = await node.FileSystem.AddFileAsync(file).ConfigureAwait(false);
                var statsAfterFirst = await node.BlockRepository.StatisticsAsync().ConfigureAwait(false);
                var blockFilesAfterFirst = BlockFileCount(node);

                var second = await node.FileSystem.AddFileAsync(file).ConfigureAwait(false);
                var statsAfterSecond = await node.BlockRepository.StatisticsAsync().ConfigureAwait(false);
                var blockFilesAfterSecond = BlockFileCount(node);

                var third = await node.FileSystem.AddFileAsync(file).ConfigureAwait(false);
                var statsAfterThird = await node.BlockRepository.StatisticsAsync().ConfigureAwait(false);
                var blockFilesAfterThird = BlockFileCount(node);

                Assert.AreEqual(first.Id, second.Id, "Uploading identical content twice should produce the same CID.");
                Assert.AreEqual(first.Id, third.Id, "Uploading identical content three times should produce the same CID.");
                Assert.AreEqual(statsAfterFirst.NumObjects, statsAfterSecond.NumObjects, "Second upload should not increase stored object count.");
                Assert.AreEqual(statsAfterFirst.NumObjects, statsAfterThird.NumObjects, "Third upload should not increase stored object count.");
                Assert.AreEqual(statsAfterFirst.RepoSize, statsAfterSecond.RepoSize, "Second upload should not increase repository block size.");
                Assert.AreEqual(statsAfterFirst.RepoSize, statsAfterThird.RepoSize, "Third upload should not increase repository block size.");
                Assert.AreEqual(blockFilesAfterFirst, blockFilesAfterSecond, "Second upload should not create additional block files.");
                Assert.AreEqual(blockFilesAfterFirst, blockFilesAfterThird, "Third upload should not create additional block files.");
            }
            finally
            {
                await node.StopAsync().ConfigureAwait(false);
            }
        }

        private static async Task ConfigureLocalOnlyNodeAsync(TempNode node)
        {
            Assert.IsTrue(Directory.Exists(TestFilesDirectory), $"Missing test files directory '{TestFilesDirectory}'.");

            node.Options.Discovery.DisableMdns = true;
            node.Options.Discovery.DisableRandomWalk = true;
            node.Options.Discovery.BootstrapPeers = Array.Empty<MultiAddress>();
            node.Options.Swarm.MinConnections = 0;
            await node.Config.SetAsync(
                "Addresses.Swarm",
                JToken.FromObject(new[] { "/ip4/127.0.0.1/tcp/0" }))
                .ConfigureAwait(false);

            await node.StartAsync().ConfigureAwait(false);

            var localPeer = await node.LocalPeer.ConfigureAwait(false);
            Assert.IsTrue(localPeer.Addresses.Any(), "Node did not publish any listening address.");
            Assert.IsTrue(
                localPeer.Addresses.All(a => a.ToString().Contains("/ip4/127.0.0.1/")),
                "Validation node must stay bound to localhost only.");
        }

        private static int BlockFileCount(TempNode node)
        {
            var blocksFolder = Path.Combine(node.Options.Repository.Folder, "blocks");
            return Directory.Exists(blocksFolder)
                ? Directory.EnumerateFiles(blocksFolder, "*", SearchOption.AllDirectories).Count()
                : 0;
        }
    }
}
