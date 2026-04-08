using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using Ipfs;
using Ipfs.Engine.ClientTests;
using Ipfs.Engine.Client.Transport;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeOperatorFileWorkflowTests
{
    [TestMethod]
    public async Task UploadBrowserFileAsync_Inspects_And_Previews_Text_Content()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var browserFile = new InMemoryBrowserFile("hello.txt", "text/plain", "hello ipfs files");

        var uploaded = await service.UploadBrowserFileAsync(browserFile, pin: true, wrap: false, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("hello.txt", uploaded.RequestedPath);
        Assert.IsFalse(uploaded.IsDirectory);
        Assert.AreEqual(0, uploaded.Links.Count);

        var inspected = await service.InspectFileSystemAsync(uploaded.ResolvedId, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(uploaded.ResolvedId, inspected.ResolvedId);
        Assert.IsFalse(inspected.IsDirectory);

        var preview = await service.ReadFilePreviewAsync(uploaded.ResolvedId, 4096, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual("hello ipfs files", preview);

        CollectionAssert.Contains((await service.ListPinsAsync(CancellationToken.None).ConfigureAwait(false)).ToArray(), uploaded.ResolvedId);
    }

    [TestMethod]
    public async Task UploadTextAsync_WithWrap_Creates_A_Browsable_Directory_Root()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        var created = await service.UploadTextAsync("note.txt", "wrapped note", pin: true, wrap: true, CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(created.IsDirectory);
        Assert.AreEqual(Encoding.UTF8.GetByteCount("wrapped note"), created.Size);
        Assert.AreEqual(1, created.Links.Count);
        Assert.AreEqual("note.txt", created.Links[0].Name);

        var preview = await service.ReadFilePreviewAsync(created.Links[0].Target, 4096, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual("wrapped note", preview);
    }

    [TestMethod]
    public async Task UploadLocalDirectoryAsync_Preserves_Nested_Files_And_Empty_Subfolders()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var rootPath = Path.Combine(Path.GetTempPath(), $"node-operator-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(Path.Combine(rootPath, "notes"));
        Directory.CreateDirectory(Path.Combine(rootPath, "empty"));
        await File.WriteAllTextAsync(Path.Combine(rootPath, "root.txt"), "root body", utf8NoBom).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(rootPath, "notes", "child.txt"), "child body", utf8NoBom).ConfigureAwait(false);

        try
        {
            var uploaded = await service.UploadLocalDirectoryAsync(rootPath, pin: true, CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(uploaded.IsDirectory);
            Assert.AreEqual(
                Encoding.UTF8.GetByteCount("root body") + Encoding.UTF8.GetByteCount("child body"),
                uploaded.Size);
            Assert.AreEqual("root body", await service.ReadFilePreviewAsync($"{uploaded.ResolvedId}/root.txt", 4096, CancellationToken.None).ConfigureAwait(false));
            Assert.AreEqual("child body", await service.ReadFilePreviewAsync($"{uploaded.ResolvedId}/notes/child.txt", 4096, CancellationToken.None).ConfigureAwait(false));

            var emptyFolder = await service.InspectFileSystemAsync($"{uploaded.ResolvedId}/empty", CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(emptyFolder.IsDirectory);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task PinAndUnpinAsync_WithRepositoryGc_Removes_The_Current_Content()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var context = NodeOperatorTestHarness.CreateContext(host.BaseAddress);
        var service = context.Service;

        var created = await service.UploadTextAsync("gc-note.txt", "remove me after gc", pin: false, wrap: false, CancellationToken.None).ConfigureAwait(false);
        var cid = Cid.Decode(created.ResolvedId);

        CollectionAssert.DoesNotContain((await service.ListPinsAsync(CancellationToken.None).ConfigureAwait(false)).ToArray(), created.ResolvedId);

        CollectionAssert.Contains((await service.PinAsync(created.ResolvedId, recursive: true, CancellationToken.None).ConfigureAwait(false)).ToArray(), created.ResolvedId);
        CollectionAssert.Contains((await service.ListPinsAsync(CancellationToken.None).ConfigureAwait(false)).ToArray(), created.ResolvedId);
        Assert.IsNotNull(await host.Client.Block.StatAsync(cid).ConfigureAwait(false));
        CollectionAssert.Contains(
            (await service.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false)).Select(item => item.Target).ToArray(),
            created.ResolvedId);

        CollectionAssert.Contains((await service.UnpinAsync(created.ResolvedId, recursive: true, CancellationToken.None).ConfigureAwait(false)).ToArray(), created.ResolvedId);
        CollectionAssert.DoesNotContain((await service.ListPinsAsync(CancellationToken.None).ConfigureAwait(false)).ToArray(), created.ResolvedId);
        CollectionAssert.DoesNotContain(
            service.GetCachedPinnedExplorerItems().Select(item => item.Target).ToArray(),
            created.ResolvedId);

        await host.Client.BlockRepository.RemoveGarbageAsync().ConfigureAwait(false);
        try
        {
            var stat = await host.Client.Block.StatAsync(cid).ConfigureAwait(false);
            Assert.IsNull(stat, "Unpinned content should be removed by repository GC.");
        }
        catch (IpfsApiException)
        {
            // A missing block can surface as a 500-style API exception on this client path.
        }
    }

    [TestMethod]
    public async Task WrappedUpload_Can_Be_Exported_As_A_Tar_Stream()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        var created = await service.UploadTextAsync("tar-note.txt", "tar export proof", pin: true, wrap: true, CancellationToken.None).ConfigureAwait(false);

        using var tarStream = await host.Client.FileSystem.GetAsync(Cid.Decode(created.ResolvedId)).ConfigureAwait(false);
        var entries = new System.Collections.Generic.List<string>();
        using var archive = TarArchive.CreateInputTarArchive(tarStream, Encoding.UTF8);

        archive.ProgressMessageEvent += (_, entry, _) =>
        {
            entries.Add(entry.Name);
        };
        archive.ListContents();

        CollectionAssert.Contains(entries, $"{created.ResolvedId}/tar-note.txt");
        Assert.AreEqual("tar export proof", await service.ReadFilePreviewAsync($"{created.ResolvedId}/tar-note.txt", 4096, CancellationToken.None).ConfigureAwait(false));
    }

    private sealed class InMemoryBrowserFile(string name, string contentType, string content) : IBrowserFile
    {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(content);

        public string Name { get; } = name;

        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;

        public long Size => bytes.LongLength;

        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
            {
                throw new IOException($"The file size {Size} exceeds the allowed limit {maxAllowedSize}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new MemoryStream(bytes, writable: false);
        }
    }
}
