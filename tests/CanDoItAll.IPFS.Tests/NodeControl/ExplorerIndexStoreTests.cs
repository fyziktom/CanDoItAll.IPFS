using System;
using System.IO;
using System.Globalization;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ExplorerIndexStoreTests
{
    [TestMethod]
    public void UpsertRoot_Initializes_The_Schema_And_RoundTrips_Roots_On_Reload()
    {
        var tempRoot = CreateTempRoot();
        var filePath = Path.Combine(tempRoot, "explorer.db");
        try
        {
            var store = CreateStore(filePath);
            var now = DateTimeOffset.UtcNow;
            store.UpsertRoot(new ExplorerIndexedRootRecord("bafy-file", "note.txt", false, 42, 0, now, now, now, true));
            store.UpsertRoot(new ExplorerIndexedRootRecord("bafy-folder", "wrapped.txt", true, 99, 1, now, now, now, true));

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsTrue(store.HasPinnedRoots());

            var reloaded = CreateStore(filePath);
            var roots = reloaded.ListPinnedRoots();
            Assert.AreEqual(2, roots.Count);
            Assert.IsTrue(roots[0].IsDirectory);
            Assert.AreEqual("wrapped.txt", roots[0].DisplayName);
            Assert.AreEqual("note.txt", roots[1].DisplayName);
            Assert.AreEqual(1, reloaded.GetRoot("bafy-folder")?.ChildCount);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void MarkPinnedRootsSeen_And_MarkMissingPinnedRootsAsUnpinned_Update_Visibility()
    {
        var tempRoot = CreateTempRoot();
        var filePath = Path.Combine(tempRoot, "explorer.db");
        try
        {
            var store = CreateStore(filePath);
            var now = DateTimeOffset.UtcNow;
            store.UpsertRoot(new ExplorerIndexedRootRecord("bafy-one", "alpha.txt", false, 10, 0, now, now, now, true));
            store.UpsertRoot(new ExplorerIndexedRootRecord("bafy-two", "beta.txt", false, 20, 0, now, now, now, true));

            var seenAt = now.AddMinutes(5);
            store.MarkPinnedRootsSeen(["bafy-one"], seenAt);
            store.MarkMissingPinnedRootsAsUnpinned(["bafy-one"]);

            Assert.AreEqual(1, store.ListPinnedRoots().Count);
            Assert.AreEqual(seenAt, store.GetRoot("bafy-one")?.LastSeenPinnedAtUtc);
            Assert.IsFalse(store.GetRoot("bafy-two")?.IsPinned);

            store.MarkUnpinned("bafy-one");
            Assert.IsFalse(store.HasPinnedRoots());
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void Legacy_Database_Is_Upgraded_To_The_Versioned_Schema()
    {
        var tempRoot = CreateTempRoot();
        var filePath = Path.Combine(tempRoot, "explorer.db");
        try
        {
            CreateLegacyDatabase(filePath);

            var store = CreateStore(filePath);
            var roots = store.ListPinnedRoots();

            Assert.AreEqual(1, roots.Count);
            Assert.AreEqual("legacy.txt", roots[0].DisplayName);
            Assert.AreEqual(ExplorerIndexStore.CurrentSchemaVersion, store.GetSchemaVersionForTests());

            var reloaded = CreateStore(filePath);
            Assert.AreEqual(ExplorerIndexStore.CurrentSchemaVersion, reloaded.GetSchemaVersionForTests());
            Assert.AreEqual(1, reloaded.ListPinnedRoots().Count);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static ExplorerIndexStore CreateStore(string filePath)
        => new(Options.Create(new ExplorerIndexStoreOptions
        {
            FilePath = filePath
        }));

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ipfs-engine-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CreateLegacyDatabase(string filePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE ExplorerPinnedRootIndex (
                Target TEXT NOT NULL PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                IsDirectory INTEGER NOT NULL,
                Size INTEGER NOT NULL,
                ChildCount INTEGER NOT NULL,
                FirstPinnedAtUtc TEXT NOT NULL,
                LastSeenPinnedAtUtc TEXT NOT NULL,
                LastMetadataRefreshAtUtc TEXT NOT NULL,
                IsPinned INTEGER NOT NULL
            );
            INSERT INTO ExplorerPinnedRootIndex (
                Target,
                DisplayName,
                IsDirectory,
                Size,
                ChildCount,
                FirstPinnedAtUtc,
                LastSeenPinnedAtUtc,
                LastMetadataRefreshAtUtc,
                IsPinned)
            VALUES (
                'bafy-legacy',
                'legacy.txt',
                0,
                12,
                0,
                '2026-04-02T10:00:00.0000000+00:00',
                '2026-04-02T10:05:00.0000000+00:00',
                '2026-04-02T10:06:00.0000000+00:00',
                1);
            """;
        command.ExecuteNonQuery();
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
            // Best-effort cleanup only.
        }
    }
}
