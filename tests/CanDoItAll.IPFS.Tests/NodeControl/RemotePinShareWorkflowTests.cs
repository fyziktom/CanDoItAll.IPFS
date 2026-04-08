#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Security;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class RemotePinShareWorkflowTests
{
    [TestMethod]
    public async Task CreateEnvelopeAsync_Uses_The_Configured_Node_Status_Snapshot()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = CreateShareService(host.BaseAddress, "http://127.0.0.1:5092/");
        var content = new RemotePinContentSnapshot(
            "/ipfs/bafy-envelope",
            "bafy-envelope",
            "shared-proof.txt",
            IsDirectory: false,
            Size: 128,
            ChildCount: 0);

        var envelope = await service.CreateEnvelopeAsync(content, "  Share this proof  ", CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("Share this proof", envelope.Note);
        Assert.AreEqual(content, envelope.Content);
        Assert.AreEqual(host.BaseAddress.ToString(), envelope.Sender.NodeBaseUrl);
        Assert.AreEqual("http://127.0.0.1:5092/", envelope.Sender.ControlAppUrl);
        Assert.AreEqual(1, envelope.Version);
        Assert.IsNull(envelope.Signature);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.Sender.PeerId));
        Assert.IsTrue(envelope.Sender.Addresses.Count > 0);
    }

    [TestMethod]
    public async Task CreateEnvelopeAsync_Signs_A_Version_Two_Request_When_A_Local_Shared_Secret_Is_Configured()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = CreateShareService(
            host.BaseAddress,
            "http://127.0.0.1:5092/",
            securityOptions: new RemotePinSecurityOptions
            {
                CompatibilityModeEnabled = false,
                LocalKeyId = "sender-key",
                LocalSharedSecret = "sender-secret",
                RequestExpiryMinutes = 10
            });
        var content = new RemotePinContentSnapshot(
            "/ipfs/bafy-signed",
            "bafy-signed",
            "signed-proof.txt",
            IsDirectory: false,
            Size: 256,
            ChildCount: 0);

        var envelope = await service.CreateEnvelopeAsync(content, null, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(RemotePinRequestSecurityService.SignedEnvelopeVersion, envelope.Version);
        Assert.AreEqual(envelope.Sender.PeerId, envelope.SenderId);
        Assert.AreEqual("sender-key", envelope.KeyId);
        Assert.IsTrue(envelope.ExpiresAtUtc > envelope.RequestedAtUtc);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.Nonce));
        Assert.AreEqual(RemotePinRequestSecurityService.HmacSha256Algorithm, envelope.SignatureAlgorithm);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.Signature));
    }

    [TestMethod]
    public async Task ProbeAsync_Returns_Receiver_Metadata_From_The_Remote_Control_App()
    {
        await using var receiver = await SimulatedRemoteControlAppHost.StartAsync().ConfigureAwait(false);
        var service = CreateShareService(new Uri("http://127.0.0.1:5001/"), "http://127.0.0.1:5092/");

        var snapshot = await service.ProbeAsync(receiver.BaseAddress.ToString(), CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(receiver.Probe.ControlAppUrl, snapshot.ControlAppUrl);
        Assert.AreEqual(receiver.Probe.NodeBaseUrl, snapshot.NodeBaseUrl);
        Assert.AreEqual(receiver.Probe.PeerId, snapshot.PeerId);
        Assert.IsTrue(snapshot.NodeHealthy);
    }

    [TestMethod]
    public async Task SendAsync_Posts_Requests_And_Returns_The_Stored_Response()
    {
        await using var receiver = await SimulatedRemoteControlAppHost.StartAsync().ConfigureAwait(false);
        var service = CreateShareService(new Uri("http://127.0.0.1:5001/"), "http://127.0.0.1:5092/");
        var request = CreateEnvelope("request-success", "bafy-success");

        var stored = await service.SendAsync(receiver.BaseAddress.ToString(), request, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(request.RequestId, stored.Request.RequestId);
        Assert.AreEqual(RemotePinRequestState.Pending, stored.State);
        Assert.AreEqual(request.RequestId, receiver.StoredRequests.Single().Request.RequestId);
    }

    [TestMethod]
    public async Task ProbeAsync_And_SendAsync_Include_The_Configured_Remote_Pin_Access_Key()
    {
        await using var receiver = await SimulatedRemoteControlAppHost.StartAsync(expectedRemotePinAccessKey: "remote-secret").ConfigureAwait(false);
        var service = CreateShareService(new Uri("http://127.0.0.1:5001/"), "http://127.0.0.1:5092/", remotePinAccessKey: "remote-secret");

        var probe = await service.ProbeAsync(receiver.BaseAddress.ToString(), CancellationToken.None).ConfigureAwait(false);
        var stored = await service.SendAsync(receiver.BaseAddress.ToString(), CreateEnvelope("request-auth", "bafy-auth"), CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(receiver.Probe.PeerId, probe.PeerId);
        Assert.AreEqual("request-auth", stored.Request.RequestId);
    }

    [TestMethod]
    public async Task SendAsync_Surfaces_Server_Detail_When_The_Remote_Control_App_Rejects_The_Request()
    {
        await using var receiver = await SimulatedRemoteControlAppHost.StartAsync(postFailureDetail: "Receiver policy rejected this request.").ConfigureAwait(false);
        var service = CreateShareService(new Uri("http://127.0.0.1:5001/"), "http://127.0.0.1:5092/");

        var exception = await ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(receiver.BaseAddress.ToString(), CreateEnvelope("request-failure", "bafy-failure"), CancellationToken.None)).ConfigureAwait(false);

        Assert.AreEqual("Receiver policy rejected this request.", exception.Message);
    }

    private static RemotePinShareService CreateShareService(
        Uri nodeBaseAddress,
        string controlAppUrl,
        string? remotePinAccessKey = null,
        RemotePinSecurityOptions? securityOptions = null)
    {
        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(new NodeConnectionSettings
        {
            Label = "Sender node",
            BaseUrl = nodeBaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        }, isHydrated: true);

        var hostedUrlRegistry = new HostedUrlRegistry();
        hostedUrlRegistry.Update([controlAppUrl]);
        var bootstrapService = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);
        var leaseFactory = new CurrentNodeLeaseFactory(targetRegistry, bootstrapService);
        var configuredNodeStatusService = new ConfiguredNodeStatusService(leaseFactory, targetRegistry, hostedUrlRegistry);
        var securityService = new RemotePinRequestSecurityService(Options.Create(securityOptions ?? new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = true
        }));
        return new RemotePinShareService(
            configuredNodeStatusService,
            NodeControlServiceCollectionExtensions.CreateCompatibilityHttpClientFactory(),
            securityService,
            Options.Create(new ControlAppSecurityOptions
            {
                RemotePinAccessKey = remotePinAccessKey
            }));
    }

    private static RemotePinRequestEnvelope CreateEnvelope(string requestId, string cid)
        => new()
        {
            RequestId = requestId,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = "Remote share proof",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092/",
                "http://127.0.0.1:5001/",
                "12D3KooWSender",
                ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
            Content = new RemotePinContentSnapshot(
                $"/ipfs/{cid}",
                cid,
                "shared-proof.txt",
                IsDirectory: false,
                Size: 256,
                ChildCount: 0)
        };

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

    private sealed class SimulatedRemoteControlAppHost : IAsyncDisposable
    {
        private readonly IHost host;

        private SimulatedRemoteControlAppHost(IHost host, Uri baseAddress, RemotePinReceiverProbeSnapshot probe)
        {
            this.host = host;
            BaseAddress = baseAddress;
            Probe = probe;
        }

        public Uri BaseAddress { get; }

        public RemotePinReceiverProbeSnapshot Probe { get; }

        public List<StoredRemotePinRequest> StoredRequests { get; } = [];

        public static async Task<SimulatedRemoteControlAppHost> StartAsync(
            string? postFailureDetail = null,
            string? expectedRemotePinAccessKey = null)
        {
            var port = GetUnusedPort();
            var baseAddress = new Uri($"http://127.0.0.1:{port}/");
            var probe = new RemotePinReceiverProbeSnapshot
            {
                ControlAppUrl = baseAddress.ToString(),
                NodeLabel = "Receiver node",
                NodeBaseUrl = "http://127.0.0.1:5002/",
                ApiPath = "api/v0",
                NodeHealthy = true,
                PeerId = "12D3KooWReceiver",
                AgentVersion = "simulated",
                Addresses = ["/ip4/127.0.0.1/tcp/4002/p2p/12D3KooWReceiver"]
            };

            SimulatedRemoteControlAppHost? hostWrapper = null;
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(baseAddress.ToString());
                    webBuilder.Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            var path = context.Request.Path.Value ?? string.Empty;
                            if (path.Equals("/api/remote-pin/probe", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!HasExpectedRemotePinAccessKey(context, expectedRemotePinAccessKey))
                                {
                                    return;
                                }

                                await context.Response.WriteAsJsonAsync(probe).ConfigureAwait(false);
                                return;
                            }

                            if (path.Equals("/api/remote-pin/requests", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!HasExpectedRemotePinAccessKey(context, expectedRemotePinAccessKey))
                                {
                                    return;
                                }

                                if (!string.IsNullOrWhiteSpace(postFailureDetail))
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    await context.Response.WriteAsync(postFailureDetail).ConfigureAwait(false);
                                    return;
                                }

                                var request = await context.Request.ReadFromJsonAsync<RemotePinRequestEnvelope>(cancellationToken: context.RequestAborted).ConfigureAwait(false)
                                    ?? throw new InvalidOperationException("The simulated remote control app did not receive a request body.");
                                var stored = new StoredRemotePinRequest
                                {
                                    Request = request,
                                    ReceivedAtUtc = DateTimeOffset.UtcNow,
                                    State = RemotePinRequestState.Pending
                                };
                                hostWrapper!.StoredRequests.Add(stored);
                                context.Response.StatusCode = StatusCodes.Status202Accepted;
                                await context.Response.WriteAsJsonAsync(stored).ConfigureAwait(false);
                                return;
                            }

                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        });
                    });
                })
                .Build();

            await host.StartAsync().ConfigureAwait(false);
            hostWrapper = new SimulatedRemoteControlAppHost(host, baseAddress, probe);
            return hostWrapper;
        }

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync().ConfigureAwait(false);
            host.Dispose();
        }

        private static bool HasExpectedRemotePinAccessKey(HttpContext context, string? expectedRemotePinAccessKey)
        {
            if (string.IsNullOrWhiteSpace(expectedRemotePinAccessKey))
            {
                return true;
            }

            var providedValue = context.Request.Headers[ControlAppSecurityHeaders.RemotePinAccessKey].ToString();
            if (string.Equals(providedValue, expectedRemotePinAccessKey, StringComparison.Ordinal))
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
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
