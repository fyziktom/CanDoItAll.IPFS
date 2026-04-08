using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ServerNodeSettingsStoreTests
{
    [TestMethod]
    public void SaveLoadAndClear_Roundtrip_Node_Settings()
    {
        var filePath = GetTempFilePath();
        var store = CreateStore(filePath);
        var settings = new NodeConnectionSettings
        {
            Label = "Receiver node",
            BaseUrl = "http://127.0.0.1:7001/",
            ApiPath = "api/v0/custom",
            TimeoutSeconds = 42
        };

        try
        {
            store.Save(settings);

            var loaded = store.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Receiver node", loaded.Label);
            Assert.AreEqual("http://127.0.0.1:7001/", loaded.BaseUrl);
            Assert.AreEqual("api/v0/custom", loaded.ApiPath);
            Assert.AreEqual(42, loaded.TimeoutSeconds);

            store.Clear();

            Assert.IsNull(store.Load());
            Assert.IsFalse(File.Exists(filePath));
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    [TestMethod]
    public void Load_Migrates_A_Legacy_Document_And_Creates_A_Backup()
    {
        var filePath = GetTempFilePath();
        var settings = new NodeConnectionSettings
        {
            Label = "Legacy node",
            BaseUrl = "http://127.0.0.1:6001/",
            ApiPath = "api/v0",
            TimeoutSeconds = 30
        };

        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings));
            var store = CreateStore(filePath);

            var loaded = store.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Legacy node", loaded.Label);
            StringAssert.Contains(File.ReadAllText(filePath), "\"schemaVersion\": 1");
            Assert.IsTrue(File.Exists($"{filePath}.bak"));

            var reloaded = store.Load();
            Assert.IsNotNull(reloaded);
            Assert.AreEqual("http://127.0.0.1:6001/", reloaded.BaseUrl);
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    [TestMethod]
    public void Load_Quarantines_A_Corrupt_Document()
    {
        var filePath = GetTempFilePath();

        try
        {
            File.WriteAllText(filePath, "{ not valid json");
            var store = CreateStore(filePath);

            var loaded = store.Load();

            Assert.IsNull(loaded);
            Assert.IsFalse(File.Exists(filePath));
            var quarantineDirectory = PersistentFileUtilities.GetQuarantineDirectory(filePath);
            Assert.IsTrue(Directory.Exists(quarantineDirectory));
            Assert.AreEqual(1, Directory.GetFiles(quarantineDirectory, "*.json").Length);
            Assert.AreEqual(1, Directory.GetFiles(quarantineDirectory, "*.error.txt").Length);
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    [TestMethod]
    public async Task ConfiguredNodeStatusService_Uses_Persisted_Target_For_Probe_Metadata()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var filePath = GetTempFilePath();
        var settings = new NodeConnectionSettings
        {
            Label = "Receiver node",
            BaseUrl = host.BaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };

        try
        {
            var store = CreateStore(filePath);
            store.Save(settings);
            var targetRegistry = new CurrentNodeTargetRegistry(Options.Create(new NodeConnectionSettings()), store);
            var bootstrapService = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);
            var leaseFactory = new CurrentNodeLeaseFactory(targetRegistry, bootstrapService);
            var hostedUrlRegistry = new HostedUrlRegistry();
            hostedUrlRegistry.Update(["http://127.0.0.1:5099/"]);
            var service = new ConfiguredNodeStatusService(leaseFactory, targetRegistry, hostedUrlRegistry);

            var probe = await service.GetReceiverProbeAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("http://127.0.0.1:5099/", probe.ControlAppUrl);
            Assert.AreEqual("Receiver node", probe.NodeLabel);
            Assert.AreEqual(host.BaseAddress.ToString(), probe.NodeBaseUrl);
            Assert.AreEqual("api/v0", probe.ApiPath);
            Assert.IsTrue(probe.NodeHealthy);
            Assert.IsFalse(string.IsNullOrWhiteSpace(probe.PeerId));
            Assert.IsTrue(probe.Addresses.Count > 0);
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    [TestMethod]
    public void BuildAdvertisedAddresses_Appends_Configured_Host_When_Reported_Addresses_Are_Local_Only()
    {
        var addresses = ConfiguredNodeStatusService.BuildAdvertisedAddresses(
            [
                "/ip4/127.0.0.1/tcp/4001",
                "/ip6/::1/tcp/4001",
                "/ip4/0.0.0.0/tcp/4001"
            ],
            new Uri("http://192.168.0.12:5001/"),
            4101);

        Assert.AreEqual(4, addresses.Count);
        Assert.IsTrue(addresses.Contains("/ip4/192.168.0.12/tcp/4101", StringComparer.Ordinal));
    }

    [TestMethod]
    public void BuildAdvertisedAddresses_Does_Not_Add_Fallback_When_Remote_Address_Already_Exists()
    {
        var addresses = ConfiguredNodeStatusService.BuildAdvertisedAddresses(
            ["/ip4/192.168.0.12/tcp/4001"],
            new Uri("http://192.168.0.12:5001/"),
            4001);

        CollectionAssert.AreEqual(new[] { "/ip4/192.168.0.12/tcp/4001" }, addresses.ToArray());
    }

    [TestMethod]
    public void RemotePinRequestEnvelope_Serializes_And_Roundtrips_Metadata()
    {
        var envelope = new RemotePinRequestEnvelope
        {
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 30, 0, TimeSpan.Zero),
            Note = "backup this folder on the receiver node",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092",
                "http://127.0.0.1:5001/",
                "12D3KooW-test-peer",
                ["addr-1", "addr-2"]),
            Content = new RemotePinContentSnapshot(
                "/ipfs/bafy-root",
                "bafy-root",
                "project-backup",
                true,
                4096,
                3)
        };

        var json = JsonSerializer.Serialize(envelope);
        var roundtrip = JsonSerializer.Deserialize<RemotePinRequestEnvelope>(json);

        Assert.IsNotNull(roundtrip);
        Assert.AreEqual(envelope.RequestId, roundtrip.RequestId);
        Assert.AreEqual(envelope.Note, roundtrip.Note);
        Assert.AreEqual("Sender node", roundtrip.Sender.Label);
        Assert.AreEqual("bafy-root", roundtrip.Content.Cid);
        Assert.IsTrue(roundtrip.Content.IsDirectory);
        CollectionAssert.AreEqual(envelope.Sender.Addresses.ToArray(), roundtrip.Sender.Addresses.ToArray());
    }

    private static ServerNodeSettingsStore CreateStore(string filePath)
        => new(Options.Create(new ServerNodeSettingsStoreOptions { FilePath = filePath }));

    private static string GetTempFilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ipfs-node-control-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "current-node-settings.json");
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
