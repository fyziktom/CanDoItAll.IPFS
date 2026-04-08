using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeOperatorExplorerWorkflowTests
{
    [TestMethod]
    public async Task Explorer_ListPinnedExplorerItemsAsync_Classifies_Pinned_Files_And_Folders()
    {
        await using var host = await StartHostWithRetryAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        var pinnedFile = await service.UploadTextAsync("plain.txt", "plain file", pin: true, wrap: false, CancellationToken.None).ConfigureAwait(false);
        var pinnedFolder = await service.UploadTextAsync("wrapped.txt", "wrapped file", pin: true, wrap: true, CancellationToken.None).ConfigureAwait(false);

        var items = await service.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false);

        var fileItem = items.Single(item => item.Target == pinnedFile.ResolvedId);
        var folderItem = items.Single(item => item.Target == pinnedFolder.ResolvedId);

        Assert.IsFalse(fileItem.IsDirectory);
        Assert.AreEqual("File", fileItem.TypeLabel);
        Assert.AreEqual($"/ipfs/{pinnedFile.ResolvedId}", fileItem.Path);
        Assert.AreEqual(Encoding.UTF8.GetByteCount("plain file"), fileItem.Size);

        Assert.IsTrue(folderItem.IsDirectory);
        Assert.AreEqual("File folder", folderItem.TypeLabel);
        Assert.AreEqual(1, folderItem.ChildCount);
        Assert.AreEqual($"/ipfs/{pinnedFolder.ResolvedId}", folderItem.Path);
        Assert.AreEqual(Encoding.UTF8.GetByteCount("wrapped file"), folderItem.Size);
    }

    [TestMethod]
    public async Task Explorer_GetCachedPinnedExplorerItems_Returns_Indexed_Roots_After_Live_Load()
    {
        await using var host = await StartHostWithRetryAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        var pinnedFile = await service.UploadTextAsync("cached.txt", "cached body", pin: true, wrap: false, CancellationToken.None).ConfigureAwait(false);

        await service.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false);
        var cachedItems = service.GetCachedPinnedExplorerItems();

        var cached = cachedItems.Single(item => item.Target == pinnedFile.ResolvedId);
        Assert.AreEqual("cached.txt", cached.DisplayName);
        Assert.AreEqual(Encoding.UTF8.GetByteCount("cached body"), cached.Size);
    }

    [TestMethod]
    public async Task Explorer_GetExplorerSnapshotAsync_Builds_Breadcrumbs_And_Child_Browse_Paths()
    {
        await using var host = await StartHostWithRetryAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        var wrapped = await service.UploadTextAsync("nested.txt", "nested body", pin: true, wrap: true, CancellationToken.None).ConfigureAwait(false);

        var rootSnapshot = await service.GetExplorerSnapshotAsync(wrapped.ResolvedId, CancellationToken.None).ConfigureAwait(false);
        var childEntry = rootSnapshot.Entries.Single();

        Assert.AreEqual($"/ipfs/{wrapped.ResolvedId}", rootSnapshot.NormalizedPath);
        Assert.IsNull(rootSnapshot.ParentPath);
        Assert.AreEqual(1, rootSnapshot.Breadcrumbs.Count);
        Assert.AreEqual(wrapped.ResolvedId, rootSnapshot.Breadcrumbs[0].Label);
        Assert.AreEqual($"/ipfs/{wrapped.ResolvedId}/nested.txt", childEntry.Path);
        Assert.IsFalse(childEntry.IsDirectory);
        Assert.AreEqual(Encoding.UTF8.GetByteCount("nested body"), childEntry.Size);

        var childSnapshot = await service.GetExplorerSnapshotAsync($"{wrapped.ResolvedId}/nested.txt", CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual($"/ipfs/{wrapped.ResolvedId}/nested.txt", childSnapshot.NormalizedPath);
        Assert.AreEqual($"/ipfs/{wrapped.ResolvedId}", childSnapshot.ParentPath);
        CollectionAssert.AreEqual(
            new[] { wrapped.ResolvedId, "nested.txt" },
            childSnapshot.Breadcrumbs.Select(item => item.Label).ToArray());
    }

    [TestMethod]
    public async Task Explorer_GetExplorerSnapshotAsync_Uses_Link_Metadata_For_Child_Folders()
    {
        await using var host = await StartHostWithRetryAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var rootPath = Path.Combine(Path.GetTempPath(), $"node-operator-explorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(Path.Combine(rootPath, "notes"));
        await File.WriteAllTextAsync(Path.Combine(rootPath, "root.txt"), "root body", utf8).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(rootPath, "notes", "child.txt"), "child body", utf8).ConfigureAwait(false);

        try
        {
            var uploaded = await service.UploadLocalDirectoryAsync(rootPath, pin: true, CancellationToken.None).ConfigureAwait(false);

            var snapshot = await service.GetExplorerSnapshotAsync(uploaded.ResolvedId, CancellationToken.None).ConfigureAwait(false);
            var rootFile = snapshot.Entries.Single(item => item.DisplayName == "root.txt");
            var folder = snapshot.Entries.Single(item => item.DisplayName == "notes");

            Assert.IsFalse(rootFile.IsDirectory);
            Assert.AreEqual(Encoding.UTF8.GetByteCount("root body"), rootFile.Size);
            Assert.IsTrue(folder.IsDirectory);
            Assert.AreEqual(1, folder.ChildCount);
            Assert.AreEqual(Encoding.UTF8.GetByteCount("child body"), folder.Size);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task Explorer_GetExplorerSnapshotAsync_Builds_Virtual_Unsorted_Year_And_Month_Folders_For_Files_Only()
    {
        await using var host = await StartHostWithRetryAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var now = DateTimeOffset.UtcNow;

        var pinnedFile = await service.UploadTextAsync("note.txt", "note body", pin: true, wrap: false, CancellationToken.None).ConfigureAwait(false);
        var pinnedFolder = await service.UploadTextAsync("wrapped.txt", "wrapped body", pin: true, wrap: true, CancellationToken.None).ConfigureAwait(false);

        await service.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false);

        var rootSnapshot = await service.GetExplorerSnapshotAsync("/virtual/unsorted", CancellationToken.None).ConfigureAwait(false);
        var yearEntry = rootSnapshot.Entries.Single();
        Assert.AreEqual("UNSORTED", rootSnapshot.Breadcrumbs.Single().Label);
        Assert.AreEqual(now.Year.ToString(), yearEntry.DisplayName);
        Assert.AreEqual(1, yearEntry.ChildCount);

        var yearSnapshot = await service.GetExplorerSnapshotAsync($"/virtual/unsorted/{now.Year}", CancellationToken.None).ConfigureAwait(false);
        var monthEntry = yearSnapshot.Entries.Single();
        Assert.AreEqual(now.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture), monthEntry.DisplayName);
        Assert.AreEqual(1, monthEntry.ChildCount);

        var monthSnapshot = await service.GetExplorerSnapshotAsync($"/virtual/unsorted/{now.Year}/{now.Month:00}", CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(1, monthSnapshot.Entries.Count);
        CollectionAssert.Contains(monthSnapshot.Entries.Select(item => item.Target).ToArray(), pinnedFile.ResolvedId);
        CollectionAssert.DoesNotContain(monthSnapshot.Entries.Select(item => item.Target).ToArray(), pinnedFolder.ResolvedId);
    }

    [TestMethod]
    public async Task Explorer_ListPinnedExplorerItemsAsync_Invalidates_Stale_Cached_Roots_Before_Directory_Traversal()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"node-operator-explorer-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var indexPath = Path.Combine(tempRoot, "explorer.db");

        try
        {
            await using var staleHost = await StartHostWithRetryAsync().ConfigureAwait(false);
            var staleService = NodeOperatorTestHarness.CreateService(staleHost.BaseAddress, indexPath);
            var staleFolder = await staleService.UploadTextAsync("ghost.txt", "ghost body", pin: true, wrap: true, CancellationToken.None).ConfigureAwait(false);

            var initialItems = await staleService.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(initialItems.Any(item => item.Target == staleFolder.ResolvedId));

            await using var freshHost = await StartHostWithRetryAsync().ConfigureAwait(false);
            var freshService = NodeOperatorTestHarness.CreateService(freshHost.BaseAddress, indexPath);

            var refreshedItems = await freshService.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(0, refreshedItems.Count);
            Assert.AreEqual(0, freshService.GetCachedPinnedExplorerItems().Count);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static async Task<TestIpfsHttpHost> StartHostWithRetryAsync()
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
            }
            catch (KeyNotFoundException ex)
            {
                lastError = ex;
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        throw lastError ?? new InvalidOperationException("The test IPFS host could not be started.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore transient cleanup failures from test temp roots.
        }
    }
}
