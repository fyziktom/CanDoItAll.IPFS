using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ipfs.Engine;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class RemotePinRequestStoreTests
{
    [TestMethod]
    public void Add_Deduplicates_By_RequestId_And_Persists_Across_Restarts()
    {
        var tempRoot = CreateTempRoot();
        var filePath = Path.Combine(tempRoot, "remote-pin-requests.json");
        try
        {
            var store = CreateStore(filePath);
            var request = CreateEnvelope("request-001", "bafy-alpha");

            store.Add(request);
            store.Add(request);
            store.Update(request.RequestId, stored =>
            {
                stored.State = RemotePinRequestState.Accepted;
                stored.ResponseMessage = "Pinned";
                stored.RespondedAtUtc = DateTimeOffset.UtcNow;
            });

            Assert.AreEqual(1, store.List().Count);

            var reloaded = CreateStore(filePath);
            var persisted = reloaded.List().Single();
            Assert.AreEqual(request.RequestId, persisted.Request.RequestId);
            Assert.AreEqual(RemotePinRequestState.Accepted, persisted.State);
            Assert.AreEqual("Pinned", persisted.ResponseMessage);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void Update_Throws_For_Unknown_Request()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var store = CreateStore(Path.Combine(tempRoot, "remote-pin-requests.json"));

            ExceptionAssert.Throws<KeyNotFoundException>(() =>
                store.Update("missing-request", _ => { }));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void List_Migrates_A_Legacy_Array_And_Creates_A_Backup()
    {
        var tempRoot = CreateTempRoot();
        var filePath = Path.Combine(tempRoot, "remote-pin-requests.json");
        try
        {
            var legacyItems = new List<StoredRemotePinRequest>
            {
                new()
                {
                    Request = CreateEnvelope("request-legacy", "bafy-legacy"),
                    ReceivedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                    State = RemotePinRequestState.Pending
                }
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(legacyItems));

            var store = CreateStore(filePath);
            var items = store.List();

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("request-legacy", items[0].Request.RequestId);
            StringAssert.Contains(File.ReadAllText(filePath), "\"schemaVersion\": 1");
            Assert.IsTrue(File.Exists($"{filePath}.bak"));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void List_Quarantines_A_Corrupt_Document()
    {
        var tempRoot = CreateTempRoot();
        var filePath = Path.Combine(tempRoot, "remote-pin-requests.json");
        try
        {
            File.WriteAllText(filePath, "{ not valid json");
            var store = CreateStore(filePath);

            var items = store.List();

            Assert.AreEqual(0, items.Count);
            Assert.IsFalse(File.Exists(filePath));
            var quarantineDirectory = PersistentFileUtilities.GetQuarantineDirectory(filePath);
            Assert.IsTrue(Directory.Exists(quarantineDirectory));
            Assert.AreEqual(1, Directory.GetFiles(quarantineDirectory, "*.json").Length);
            Assert.AreEqual(1, Directory.GetFiles(quarantineDirectory, "*.error.txt").Length);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static RemotePinRequestStore CreateStore(string filePath)
        => new(Options.Create(new RemotePinRequestStoreOptions
        {
            FilePath = filePath
        }));

    private static RemotePinRequestEnvelope CreateEnvelope(string requestId, string cid)
        => new()
        {
            RequestId = requestId,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = "Round-trip proof",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092",
                "http://127.0.0.1:5001/",
                "12D3KooW-request-store",
                ["127.0.0.1"]),
            Content = new RemotePinContentSnapshot(
                $"/ipfs/{cid}",
                cid,
                "request-store.txt",
                false,
                128,
                0)
        };

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ipfs-engine-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
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
