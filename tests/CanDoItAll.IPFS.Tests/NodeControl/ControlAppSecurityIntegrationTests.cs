#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Security;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ControlAppSecurityIntegrationTests
{
    [TestMethod]
    public async Task Security_Policy_Metadata_Is_Discoverable_On_Protected_Minimal_Apis()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress, CreateProOverrides());

        var dataSource = app.Services.GetRequiredService<EndpointDataSource>();
        var logsEndpoint = FindRoute(dataSource, "/api/logs");
        var remotePinRequestEndpoint = FindRoute(dataSource, "/api/remote-pin/requests");

        Assert.AreEqual(
            ControlAppAuthorizationPolicyNames.AdminApi,
            logsEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single().Policy);
        Assert.AreEqual(
            ControlAppAuthorizationPolicyNames.RemotePinIngress,
            remotePinRequestEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single().Policy);
        Assert.AreEqual(
            ControlAppRateLimitPolicyNames.AdminApi,
            logsEndpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>().Single().PolicyName);
        Assert.AreEqual(
            ControlAppRateLimitPolicyNames.RemotePinIngress,
            remotePinRequestEndpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>().Single().PolicyName);
    }

    [TestMethod]
    public async Task Pro_Mode_Rejects_Anonymous_Admin_And_Remote_Pin_Requests()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress, CreateProOverrides());
        using var client = app.CreateConfiguredClient();

        await AssertProtectedAsync(await client.GetAsync("/api/logs?window=10m&limit=10").ConfigureAwait(false)).ConfigureAwait(false);
        await AssertProtectedAsync(await client.GetAsync("/api/files/content?path=bafy-anonymous").ConfigureAwait(false)).ConfigureAwait(false);
        await AssertProtectedAsync(await client.GetAsync("/api/remote-pin/probe").ConfigureAwait(false)).ConfigureAwait(false);

        using var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("security proof")), "files", "proof.txt");
        await AssertProtectedAsync(await client.PostAsync("/api/files/upload-browser?pin=true&wrap=false", uploadContent).ConfigureAwait(false)).ConfigureAwait(false);
        await AssertProtectedAsync(await client.PostAsJsonAsync("/api/remote-pin/requests", CreateEnvelope("request-protected", "bafy-secure")).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Light_Mode_Explicitly_Allows_Local_Anonymous_Admin_And_Remote_Pin_Flows()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress);
        using var client = app.CreateConfiguredClient();

        using var logs = await client.GetAsync("/api/logs?window=10m&limit=10").ConfigureAwait(false);
        using var probe = await client.GetAsync("/api/remote-pin/probe").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, logs.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, probe.StatusCode);
    }

    [TestMethod]
    public async Task Pro_Mode_Admin_And_Remote_Pin_Keys_Unlock_Their_Protected_Endpoints()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress, CreateProOverrides());

        app.LogStore.Write(new ApplicationLogEntry(
            DateTimeOffset.UtcNow,
            "Information",
            "Tests.Security",
            "secured admin endpoint",
            1,
            null));

        using var adminClient = app.CreateConfiguredClient((ControlAppSecurityHeaders.AdminAccessKey, "admin-secret"));
        using var remotePinClient = app.CreateConfiguredClient((ControlAppSecurityHeaders.RemotePinAccessKey, "remote-secret"));

        using var logs = await adminClient.GetAsync("/api/logs?window=10m&limit=10").ConfigureAwait(false);
        using var probe = await remotePinClient.GetAsync("/api/remote-pin/probe").ConfigureAwait(false);
        using var request = await remotePinClient.PostAsJsonAsync("/api/remote-pin/requests", CreateEnvelope("request-authorized", "bafy-authorized")).ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, logs.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, probe.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, request.StatusCode);
    }

    [TestMethod]
    public async Task Pro_Mode_Admin_And_Remote_Pin_Policies_Reject_Over_Limit_Requests()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress, new Dictionary<string, string?>(CreateProOverrides())
        {
            ["ControlAppSecurity:AdminPermitLimit"] = "1",
            ["ControlAppSecurity:RemotePinPermitLimit"] = "1"
        });

        using var adminClient = app.CreateConfiguredClient((ControlAppSecurityHeaders.AdminAccessKey, "admin-secret"));
        using var remotePinClient = app.CreateConfiguredClient((ControlAppSecurityHeaders.RemotePinAccessKey, "remote-secret"));

        using var firstAdmin = await adminClient.GetAsync("/api/logs?window=10m&limit=10").ConfigureAwait(false);
        using var secondAdmin = await adminClient.GetAsync("/api/logs?window=10m&limit=10").ConfigureAwait(false);
        using var firstRemotePin = await remotePinClient.GetAsync("/api/remote-pin/probe").ConfigureAwait(false);
        using var secondRemotePin = await remotePinClient.GetAsync("/api/remote-pin/probe").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, firstAdmin.StatusCode);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, secondAdmin.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, firstRemotePin.StatusCode);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, secondRemotePin.StatusCode);
    }

    [TestMethod]
    public async Task Gateway_Endpoints_Remain_Public_In_Pro_Mode()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress, CreateProOverrides());
        using var client = app.CreateConfiguredClient();

        using var ipfs = await client.GetAsync("/ipfs/bafy-missing").ConfigureAwait(false);
        using var ipns = await client.GetAsync("/ipns/example.invalid").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.NotFound, ipfs.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, ipns.StatusCode);
    }

    private static IReadOnlyDictionary<string, string?> CreateProOverrides()
        => new Dictionary<string, string?>
        {
            ["OperatingProfile:Mode"] = "Pro",
            ["ControlAppSecurity:AdminAccessKey"] = "admin-secret",
            ["ControlAppSecurity:RemotePinAccessKey"] = "remote-secret"
        };

    private static async Task AssertProtectedAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.IsTrue(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"Expected a protected endpoint response, but got {(int)response.StatusCode} {response.StatusCode}.");
            await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
        }
    }

    private static RouteEndpoint FindRoute(EndpointDataSource dataSource, string rawText)
        => dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint => string.Equals(endpoint.RoutePattern.RawText, rawText, StringComparison.Ordinal));

    private static SecurityControlAppTestHost CreateAppHost(
        Uri apiBaseAddress,
        IReadOnlyDictionary<string, string?>? overrides = null)
        => new(
            new NodeConnectionSettings
            {
                Label = "Control app security test node",
                BaseUrl = apiBaseAddress.ToString(),
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            },
            overrides);

    private static RemotePinRequestEnvelope CreateEnvelope(string requestId, string cid)
        => new()
        {
            RequestId = requestId,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = "Security enqueue proof",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092/",
                "http://127.0.0.1:5001/",
                "12D3KooWSender",
                ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
            Content = new RemotePinContentSnapshot(
                $"/ipfs/{cid}",
                cid,
                "security-proof.txt",
                IsDirectory: false,
                Size: 128,
                ChildCount: 0)
        };

    private sealed class SecurityControlAppTestHost : WebApplicationFactory<global::Program>
    {
        private readonly string tempRoot;
        private readonly IReadOnlyDictionary<string, string?> overrides;

        public SecurityControlAppTestHost(
            NodeConnectionSettings settings,
            IReadOnlyDictionary<string, string?>? overrides = null)
        {
            this.overrides = overrides ?? new Dictionary<string, string?>();
            tempRoot = Path.Combine(Path.GetTempPath(), "control-app-security-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var settingsStore = new ServerNodeSettingsStore(Options.Create(new ServerNodeSettingsStoreOptions
            {
                FilePath = Path.Combine(tempRoot, "current-node-settings.json")
            }));
            settingsStore.Save(settings);
        }

        public ApplicationLogStore LogStore => Services.GetRequiredService<ApplicationLogStore>();

        public HttpClient CreateConfiguredClient(params (string Name, string Value)[] headers)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            foreach (var (name, value) in headers)
            {
                client.DefaultRequestHeaders.Remove(name);
                client.DefaultRequestHeaders.Add(name, value);
            }

            Services.GetRequiredService<HostedUrlRegistry>().Update([client.BaseAddress!.ToString()]);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                if (overrides.Count > 0)
                {
                    config.AddInMemoryCollection(overrides);
                }
            });
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<ServerNodeSettingsStoreOptions>(options =>
                    options.FilePath = Path.Combine(tempRoot, "current-node-settings.json"));
                services.PostConfigure<RemotePinRequestStoreOptions>(options =>
                    options.FilePath = Path.Combine(tempRoot, "remote-pin-requests.json"));
                services.PostConfigure<ApplicationLogStoreOptions>(options =>
                {
                    options.FilePath = Path.Combine(tempRoot, "application.log");
                    options.MaxEntriesPerWindow = 100;
                });
                services.PostConfigure<ExplorerIndexStoreOptions>(options =>
                    options.FilePath = Path.Combine(tempRoot, "explorer.db"));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            TryDelete(tempRoot);
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
}
