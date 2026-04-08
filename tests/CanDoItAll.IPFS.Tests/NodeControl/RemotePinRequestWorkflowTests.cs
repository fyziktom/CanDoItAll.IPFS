using Ipfs.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class RemotePinRequestWorkflowTests
{
    [TestMethod]
    public async Task Reject_Persists_Request_State_Without_Pinning()
    {
        await using var receiver = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            var workflow = CreateWorkflow(receiver.BaseAddress, tempRoot);
            var request = CreateRequestEnvelope(
                requestId: Guid.NewGuid().ToString("N"),
                cid: "bafy-reject",
                senderPeerId: "12D3KooW-reject",
                senderAddresses: ["127.0.0.1"]);

            var stored = workflow.Enqueue(request);
            var rejected = workflow.Reject(stored.Request.RequestId, "Rejected by receiver policy.");

            Assert.AreEqual(RemotePinRequestState.Rejected, rejected.State);
            Assert.AreEqual("Rejected by receiver policy.", rejected.ResponseMessage);
            Assert.AreEqual(RemotePinRequestState.Rejected, workflow.List().Single().State);
            CollectionAssert.DoesNotContain(
                (await receiver.Client.Pin.ListAsync().ConfigureAwait(false)).Select(cid => cid.ToString()).ToArray(),
                request.Content.Cid);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task AcceptAsync_Connects_To_Sender_And_Pins_Content()
    {
        await using var receiver = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var sender = await StartIsolatedNodeAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            var senderPeer = await sender.Generic.IdAsync().ConfigureAwait(false);
            var senderDialAddress = (await TestIpfsHttpHost.GetDialAddressAsync(sender).ConfigureAwait(false)).ToString();
            var senderFile = await sender.FileSystem.AddTextAsync($"remote pin proof {Guid.NewGuid():N}").ConfigureAwait(false);
            var workflow = CreateWorkflow(receiver.BaseAddress, tempRoot);
            var request = CreateRequestEnvelope(
                requestId: Guid.NewGuid().ToString("N"),
                cid: senderFile.Id.ToString(),
                senderPeerId: senderPeer.Id.ToString(),
                senderAddresses: [senderDialAddress],
                displayName: "shared-note.txt");

            workflow.Enqueue(request);
            var accepted = await workflow.AcceptAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(RemotePinRequestState.Accepted, accepted.State);
            StringAssert.Contains(accepted.ResponseMessage ?? string.Empty, "Pinned");
            Assert.IsNotNull(await receiver.Node.Block.StatAsync(senderFile.Id).ConfigureAwait(false));
            var pinVisibleOnNode = await WaitForAsync(
                async () => (await receiver.Node.Pin.ListAsync().ConfigureAwait(false))
                    .Any(cid => string.Equals(cid.ToString(), request.Content.Cid, StringComparison.Ordinal)),
                TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            var pinVisibleOnApi = await WaitForAsync(
                async () => (await receiver.Client.Pin.ListAsync().ConfigureAwait(false))
                    .Any(cid => string.Equals(cid.ToString(), request.Content.Cid, StringComparison.Ordinal)),
                TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Assert.IsTrue(pinVisibleOnNode, "The receiver node should persist the sender CID after accepting the request.");
            Assert.IsTrue(pinVisibleOnApi, "The receiver HTTP API should list the sender CID after accepting the request.");
        }
        finally
        {
            TryDelete(tempRoot);
            await sender.StopAsync().ConfigureAwait(false);
            sender.Dispose();
        }
    }

    [TestMethod]
    public async Task AcceptAsync_Marks_Request_As_Accepted_When_Pin_Is_Persisted_Before_Api_Returns_Failure()
    {
        var cid = "bafybeie5nqv6kd3qnfjuprw2scvubkzyj2q6p6fow6s7kvig5z4m4fo3qa";
        await using var receiver = await SimulatedReceiverHost.StartWithPinAddFailureAsync(cid).ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            var workflow = CreateWorkflow(receiver.BaseAddress, tempRoot);
            var request = CreateRequestEnvelope(
                requestId: Guid.NewGuid().ToString("N"),
                cid: cid,
                senderPeerId: "12D3KooW-recovery",
                senderAddresses: ["/ip4/127.0.0.1/tcp/4101/p2p/12D3KooW-recovery"],
                displayName: "recovered-folder");

            workflow.Enqueue(request);
            var accepted = await workflow.AcceptAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(RemotePinRequestState.Accepted, accepted.State);
            StringAssert.Contains(accepted.ResponseMessage ?? string.Empty, "after verifying the final state");
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task AcceptAsync_Marks_Request_As_Failed_When_Content_Cannot_Be_Reached()
    {
        await using var receiver = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            var workflow = CreateWorkflow(receiver.BaseAddress, tempRoot);
            var request = CreateRequestEnvelope(
                requestId: Guid.NewGuid().ToString("N"),
                cid: "bafy-unreachable",
                senderPeerId: "12D3KooW-unreachable",
                senderAddresses: ["/ip4/127.0.0.1/tcp/49999/p2p/12D3KooW-unreachable"]);

            workflow.Enqueue(request);
            var failed = await workflow.AcceptAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(RemotePinRequestState.Failed, failed.State);
            Assert.IsFalse(string.IsNullOrWhiteSpace(failed.ResponseMessage));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void Enqueue_Persists_A_Rejected_Request_When_Security_Validation_Fails()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var workflow = CreateWorkflow(
                new Uri("http://127.0.0.1:5001/"),
                tempRoot,
                new RemotePinSecurityOptions
                {
                    CompatibilityModeEnabled = false
                });
            var request = CreateRequestEnvelope(
                requestId: Guid.NewGuid().ToString("N"),
                cid: "bafy-rejected",
                senderPeerId: "12D3KooW-rejected",
                senderAddresses: ["/ip4/127.0.0.1/tcp/4101/p2p/12D3KooW-rejected"]);

            var error = Throws<ArgumentException>(() => workflow.Enqueue(request));

            StringAssert.Contains(error.Message, "Unsigned");
            var stored = workflow.List().Single();
            Assert.AreEqual(RemotePinRequestState.Rejected, stored.State);
            Assert.AreEqual(RemotePinSecurityDisposition.Rejected, stored.SecurityDisposition);
            StringAssert.Contains(stored.ResponseMessage ?? string.Empty, "Unsigned");
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void ResolvePinTimeoutSeconds_Extends_Accept_Window_For_Folders_And_Large_Content()
    {
        var folder = new RemotePinContentSnapshot(
            "/ipfs/bafy-folder",
            "bafy-folder",
            "backup-folder",
            IsDirectory: true,
            Size: 64 * 1024,
            ChildCount: 12);
        var file = new RemotePinContentSnapshot(
            "/ipfs/bafy-file",
            "bafy-file",
            "note.txt",
            IsDirectory: false,
            Size: 1024,
            ChildCount: 0);
        var largeFile = new RemotePinContentSnapshot(
            "/ipfs/bafy-large",
            "bafy-large",
            "archive.bin",
            IsDirectory: false,
            Size: 512L * 1024L * 1024L,
            ChildCount: 0);

        Assert.AreEqual(900, RemotePinRequestWorkflowService.ResolvePinTimeoutSeconds(folder, 120));
        Assert.AreEqual(300, RemotePinRequestWorkflowService.ResolvePinTimeoutSeconds(file, 120));
        Assert.AreEqual(900, RemotePinRequestWorkflowService.ResolvePinTimeoutSeconds(largeFile, 120));
        Assert.AreEqual(900, RemotePinRequestWorkflowService.ResolvePinTimeoutSeconds(folder, 600));
    }

    private static RemotePinRequestWorkflowService CreateWorkflow(
        Uri receiverBaseAddress,
        string tempRoot,
        RemotePinSecurityOptions? securityOptions = null)
    {
        var settingsStore = new ServerNodeSettingsStore(Options.Create(new ServerNodeSettingsStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "current-node-settings.json")
        }));
        settingsStore.Save(new NodeConnectionSettings
        {
            Label = "Receiver node",
            BaseUrl = receiverBaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        });

        var targetRegistry = new CurrentNodeTargetRegistry(Options.Create(new NodeConnectionSettings()), settingsStore);
        var bootstrapService = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);
        var leaseFactory = new CurrentNodeLeaseFactory(targetRegistry, bootstrapService);
        var requestStore = new RemotePinRequestStore(Options.Create(new RemotePinRequestStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "remote-pin-requests.json")
        }));
        var indexStore = new ExplorerIndexStore(Options.Create(new ExplorerIndexStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "explorer-index.db")
        }));
        var securityService = new RemotePinRequestSecurityService(Options.Create(securityOptions ?? new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = true
        }));

        return new RemotePinRequestWorkflowService(
            requestStore,
            leaseFactory,
            indexStore,
            securityService,
            NullLogger<RemotePinRequestWorkflowService>.Instance);
    }

    private static RemotePinRequestEnvelope CreateRequestEnvelope(
        string requestId,
        string cid,
        string senderPeerId,
        IReadOnlyList<string> senderAddresses,
        string displayName = "shared-item")
        => new()
        {
            RequestId = requestId,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = "Please pin this content on the receiver node.",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092",
                "http://127.0.0.1:5001/",
                senderPeerId,
                senderAddresses),
            Content = new RemotePinContentSnapshot(
                $"/ipfs/{cid}",
                cid,
                displayName,
                false,
                128,
                0)
        };

    private static async Task<TempNode> StartIsolatedNodeAsync()
    {
        var node = new TempNode();
        node.Options.Discovery.DisableMdns = true;
        node.Options.Discovery.DisableRandomWalk = true;
        await node.Bootstrap.RemoveAllAsync().ConfigureAwait(false);
        await node.StartAsync().ConfigureAwait(false);
        return node;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ipfs-node-control-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDelete(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static T Throws<T>(Func<object?> action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T ex)
        {
            return ex;
        }

        Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
        return null!;
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        return await predicate().ConfigureAwait(false);
    }

    private sealed class SimulatedReceiverHost : IAsyncDisposable
    {
        private readonly IHost host;

        private SimulatedReceiverHost(IHost host, Uri baseAddress)
        {
            this.host = host;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public static async Task<SimulatedReceiverHost> StartWithPinAddFailureAsync(string cid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cid);

            var localBlockAvailable = false;
            var pinPersisted = false;
            var port = GetUnusedPort();
            var baseAddress = new Uri($"http://127.0.0.1:{port}/");

            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(baseAddress.ToString());
                    webBuilder.Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            var path = context.Request.Path.Value ?? string.Empty;
                            if (path.Equals("/api/v0/swarm/connect", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Response.StatusCode = StatusCodes.Status200OK;
                                await context.Response.WriteAsync(string.Empty).ConfigureAwait(false);
                                return;
                            }

                            if (path.Equals("/api/v0/version", StringComparison.OrdinalIgnoreCase))
                            {
                                await context.Response.WriteAsJsonAsync(new Dictionary<string, string>
                                {
                                    ["Version"] = "simulated"
                                }).ConfigureAwait(false);
                                return;
                            }

                            if (path.Equals("/api/v0/block/stat", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!localBlockAvailable)
                                {
                                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                    await context.Response.WriteAsync("block not found").ConfigureAwait(false);
                                    return;
                                }

                                await context.Response.WriteAsJsonAsync(new
                                {
                                    Key = cid,
                                    Size = 128L
                                }).ConfigureAwait(false);
                                return;
                            }

                            if (path.Equals("/api/v0/block/get", StringComparison.OrdinalIgnoreCase))
                            {
                                localBlockAvailable = true;
                                context.Response.StatusCode = StatusCodes.Status200OK;
                                context.Response.ContentType = "application/octet-stream";
                                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("simulated-block")).ConfigureAwait(false);
                                return;
                            }

                            if (path.Equals("/api/v0/pin/add", StringComparison.OrdinalIgnoreCase))
                            {
                                pinPersisted = true;
                                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                await context.Response.WriteAsync("pin persisted before response failure").ConfigureAwait(false);
                                return;
                            }

                            if (path.Equals("/api/v0/pin/ls", StringComparison.OrdinalIgnoreCase))
                            {
                                await context.Response.WriteAsJsonAsync(new
                                {
                                    Keys = pinPersisted
                                        ? new Dictionary<string, object>
                                        {
                                            [cid] = new { Type = "recursive" }
                                        }
                                        : new Dictionary<string, object>()
                                }).ConfigureAwait(false);
                                return;
                            }

                            context.Response.StatusCode = StatusCodes.Status404NotFound;
                        });
                    });
                })
                .Build();

            await host.StartAsync().ConfigureAwait(false);
            return new SimulatedReceiverHost(host, baseAddress);
        }

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync().ConfigureAwait(false);
            host.Dispose();
        }

        private static int GetUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
