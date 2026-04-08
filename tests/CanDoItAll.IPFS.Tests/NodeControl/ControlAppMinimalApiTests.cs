using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Security;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ControlAppMinimalApiTests
{
    [TestMethod]
    public async Task FilesContentEndpoint_Returns_File_Content_And_Download_Metadata()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress);
        using var client = app.CreateConfiguredClient();
        var service = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var uploaded = await service.UploadTextAsync("note.txt", "control app file proof", pin: true, wrap: false, default).ConfigureAwait(false);

        using var response = await client.GetAsync(
            $"/api/files/content?path={Uri.EscapeDataString(uploaded.ResolvedId)}&name={Uri.EscapeDataString("proof.txt")}&download=true").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType);
        StringAssert.Contains(response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, uploaded.ResolvedId);
        StringAssert.Contains(response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, ".txt");
        Assert.AreEqual("control app file proof", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task UploadBrowserEndpoint_Accepts_Multipart_Form_Data_And_Stores_The_File()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress);
        using var client = app.CreateConfiguredClient();
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("browser upload body"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "files", "browser-note.txt");

        using var response = await client.PostAsync("/api/files/upload-browser?pin=true&wrap=false", content).ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<NodeFileSnapshot>().ConfigureAwait(false);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual("browser-note.txt", snapshot.RequestedPath);
        Assert.IsFalse(snapshot.IsDirectory);
        Assert.AreEqual("browser upload body", await apiHost.Client.FileSystem.ReadAllTextAsync(snapshot.ResolvedId).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task LogsEndpoints_Return_A_Recent_Json_Slice_And_A_Downloadable_Text_File()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress);
        using var client = app.CreateConfiguredClient();
        app.LogStore.Write(new ApplicationLogEntry(
            DateTimeOffset.UtcNow.AddMinutes(-3),
            "Warning",
            "Tests.ControlApp",
            "control app warning",
            10,
            null));
        app.LogStore.Write(new ApplicationLogEntry(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            "Error",
            "Tests.ControlApp",
            "control app error",
            11,
            "System.InvalidOperationException: boom"));

        var slice = await client.GetFromJsonAsync<ApplicationLogSlice>("/api/logs?window=10m&limit=10").ConfigureAwait(false);
        using var download = await client.GetAsync("/api/logs/download?window=10m&limit=10").ConfigureAwait(false);

        Assert.IsNotNull(slice);
        Assert.AreEqual("10m", slice.WindowKey);
        Assert.IsTrue(slice.Entries.Count >= 2);
        Assert.IsTrue(slice.Entries.Any(entry => entry.Message == "control app warning"));
        Assert.IsTrue(slice.Entries.Any(entry => entry.Message == "control app error"));
        Assert.AreEqual(HttpStatusCode.OK, download.StatusCode);
        Assert.AreEqual("text/plain", download.Content.Headers.ContentType?.MediaType);
        StringAssert.Contains(download.Content.Headers.ContentDisposition?.FileName ?? string.Empty, "ipfs-node-control-10m-");
        StringAssert.Contains(await download.Content.ReadAsStringAsync().ConfigureAwait(false), "control app warning");
    }

    [TestMethod]
    public async Task RemotePinEndpoints_Return_Probe_Metadata_And_Enqueue_Requests()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress);
        using var client = app.CreateConfiguredClient();

        var probe = await client.GetFromJsonAsync<RemotePinReceiverProbeSnapshot>("/api/remote-pin/probe").ConfigureAwait(false);
        using var response = await client.PostAsJsonAsync("/api/remote-pin/requests", CreateEnvelope("request-001", "bafy-api-proof")).ConfigureAwait(false);
        var stored = await response.Content.ReadFromJsonAsync<StoredRemotePinRequest>().ConfigureAwait(false);

        Assert.IsNotNull(probe);
        Assert.IsTrue(probe.NodeHealthy);
        Assert.AreEqual(apiHost.BaseAddress.ToString(), probe.NodeBaseUrl);
        Assert.IsFalse(string.IsNullOrWhiteSpace(probe.ControlAppUrl));
        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.IsNotNull(stored);
        Assert.AreEqual(RemotePinRequestState.Pending, stored.State);
        Assert.AreEqual(RemotePinSecurityDisposition.Compatibility, stored.SecurityDisposition);
        Assert.AreEqual(1, app.RequestStore.List().Count);
        Assert.AreEqual("request-001", app.RequestStore.List()[0].Request.RequestId);
    }

    [TestMethod]
    public async Task RemotePinEndpoints_Reject_Unsigned_Requests_In_Pro_Mode_When_Compatibility_Is_Disabled()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(
            apiHost.BaseAddress,
            new Dictionary<string, string?>
            {
                ["OperatingProfile:Mode"] = "Pro",
                ["ControlAppSecurity:RemotePinAccessKey"] = "remote-secret",
                ["RemotePinSecurity:CompatibilityModeEnabled"] = "false"
            });
        using var client = app.CreateConfiguredClient((ControlAppSecurityHeaders.RemotePinAccessKey, "remote-secret"));

        using var response = await client.PostAsJsonAsync("/api/remote-pin/requests", CreateEnvelope("request-pro-reject", "bafy-pro-reject")).ConfigureAwait(false);
        var detail = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(detail, "Unsigned");
        var stored = app.RequestStore.List().Single();
        Assert.AreEqual(RemotePinRequestState.Rejected, stored.State);
        Assert.AreEqual(RemotePinSecurityDisposition.Rejected, stored.SecurityDisposition);
    }

    [TestMethod]
    public async Task RemotePinEndpoints_Accept_Signed_Requests_And_Reject_Replays_In_Pro_Mode()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(
            apiHost.BaseAddress,
            new Dictionary<string, string?>
            {
                ["OperatingProfile:Mode"] = "Pro",
                ["ControlAppSecurity:RemotePinAccessKey"] = "remote-secret",
                ["RemotePinSecurity:CompatibilityModeEnabled"] = "false",
                ["RemotePinSecurity:TrustedSenders:0:SenderId"] = "12D3KooWSender",
                ["RemotePinSecurity:TrustedSenders:0:Label"] = "Sender node",
                ["RemotePinSecurity:TrustedSenders:0:KeyId"] = "sender-key",
                ["RemotePinSecurity:TrustedSenders:0:SharedSecret"] = "sender-secret"
            });
        using var client = app.CreateConfiguredClient((ControlAppSecurityHeaders.RemotePinAccessKey, "remote-secret"));
        var signedEnvelope = CreateSignedEnvelope("request-pro-accept", "bafy-pro-accept", "sender-key", "sender-secret");

        using var acceptedResponse = await client.PostAsJsonAsync("/api/remote-pin/requests", signedEnvelope).ConfigureAwait(false);
        var stored = await acceptedResponse.Content.ReadFromJsonAsync<StoredRemotePinRequest>().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.Accepted, acceptedResponse.StatusCode);
        Assert.IsNotNull(stored);
        Assert.AreEqual(RemotePinSecurityDisposition.Verified, stored.SecurityDisposition);
        Assert.AreEqual(1, app.RequestStore.List().Count);

        using var replayResponse = await client.PostAsJsonAsync("/api/remote-pin/requests", signedEnvelope).ConfigureAwait(false);
        var replayDetail = await replayResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.BadRequest, replayResponse.StatusCode);
        StringAssert.Contains(replayDetail, "already exists");
        Assert.AreEqual(1, app.RequestStore.List().Count);
    }

    [TestMethod]
    public async Task HealthEndpoints_Distinguish_Liveness_And_Readiness()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(apiHost.BaseAddress);
        using var client = app.CreateConfiguredClient();

        using var liveResponse = await client.GetAsync("/health/live").ConfigureAwait(false);
        using var readyResponse = await client.GetAsync("/health/ready").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, readyResponse.StatusCode);

        var liveDocument = await ReadJsonAsync(liveResponse).ConfigureAwait(false);
        var readyDocument = await ReadJsonAsync(readyResponse).ConfigureAwait(false);

        Assert.AreEqual("Healthy", liveDocument.RootElement.GetProperty("status").GetString());
        Assert.AreEqual(0, liveDocument.RootElement.GetProperty("entries").EnumerateObject().Count());
        Assert.AreEqual("Healthy", readyDocument.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("Healthy", readyDocument.RootElement.GetProperty("entries").GetProperty("current-node").GetProperty("status").GetString());
        Assert.AreEqual("Healthy", readyDocument.RootElement.GetProperty("entries").GetProperty("persistence").GetProperty("status").GetString());
        Assert.IsTrue(readyResponse.Headers.TryGetValues(NodeControlTelemetry.CorrelationHeaderName, out var correlationHeaderValues));
        Assert.IsFalse(string.IsNullOrWhiteSpace(correlationHeaderValues.Single()));
    }

    [TestMethod]
    public async Task ReadinessEndpoint_Fails_When_Current_Node_Is_Unreachable()
    {
        using var app = CreateAppHost(new Uri("http://127.0.0.1:65531/"));
        using var client = app.CreateConfiguredClient();

        using var response = await client.GetAsync("/health/ready").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var document = await ReadJsonAsync(response).ConfigureAwait(false);
        Assert.AreEqual("Unhealthy", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("Unhealthy", document.RootElement.GetProperty("entries").GetProperty("current-node").GetProperty("status").GetString());
        Assert.AreEqual("Healthy", document.RootElement.GetProperty("entries").GetProperty("persistence").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task ReadinessEndpoint_Requires_Admin_Access_When_Profile_Requires_Authentication()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        using var app = CreateAppHost(
            apiHost.BaseAddress,
            new Dictionary<string, string?>
            {
                ["OperatingProfile:Mode"] = "Pro",
                ["ControlAppSecurity:AdminAccessKey"] = "admin-secret"
            });
        using var anonymousClient = app.CreateConfiguredClient();
        using var adminClient = app.CreateConfiguredClient((ControlAppSecurityHeaders.AdminAccessKey, "admin-secret"));

        using var liveResponse = await anonymousClient.GetAsync("/health/live").ConfigureAwait(false);
        using var anonymousReadyResponse = await anonymousClient.GetAsync("/health/ready").ConfigureAwait(false);
        using var authorizedReadyResponse = await adminClient.GetAsync("/health/ready").ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousReadyResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, authorizedReadyResponse.StatusCode);
    }

    private static ControlAppTestHost CreateAppHost(Uri apiBaseAddress, IReadOnlyDictionary<string, string?>? configurationOverrides = null)
        => new(
            new NodeConnectionSettings
        {
            Label = "Control app test node",
            BaseUrl = apiBaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        },
            configurationOverrides);

    private static RemotePinRequestEnvelope CreateSignedEnvelope(string requestId, string cid, string keyId, string sharedSecret)
    {
        var signer = new RemotePinRequestSecurityService(Options.Create(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            LocalKeyId = keyId,
            LocalSharedSecret = sharedSecret,
            RequestExpiryMinutes = 10
        }));

        return signer.PrepareOutgoingEnvelope(CreateEnvelope(requestId, cid));
    }

    private static RemotePinRequestEnvelope CreateEnvelope(string requestId, string cid)
        => new()
        {
            RequestId = requestId,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = "API enqueue proof",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092/",
                "http://127.0.0.1:5001/",
                "12D3KooWSender",
                ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
            Content = new RemotePinContentSnapshot(
                $"/ipfs/{cid}",
                cid,
                "api-proof.txt",
                IsDirectory: false,
                Size: 128,
                ChildCount: 0)
        };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    private sealed class ControlAppTestHost : WebApplicationFactory<global::Program>
    {
        private readonly string tempRoot;
        private readonly IReadOnlyDictionary<string, string?>? configurationOverrides;

        public ControlAppTestHost(NodeConnectionSettings settings, IReadOnlyDictionary<string, string?>? configurationOverrides)
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "control-app-api-tests", Guid.NewGuid().ToString("N"));
            this.configurationOverrides = configurationOverrides;
            Directory.CreateDirectory(tempRoot);

            var settingsStore = new ServerNodeSettingsStore(Options.Create(new ServerNodeSettingsStoreOptions
            {
                FilePath = Path.Combine(tempRoot, "current-node-settings.json")
            }));
            settingsStore.Save(settings);
        }

        public ApplicationLogStore LogStore => Services.GetRequiredService<ApplicationLogStore>();

        public RemotePinRequestStore RequestStore => Services.GetRequiredService<RemotePinRequestStore>();

        public HttpClient CreateConfiguredClient(params (string HeaderName, string Value)[] headers)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            Services.GetRequiredService<HostedUrlRegistry>().Update([client.BaseAddress!.ToString()]);
            foreach (var (headerName, value) in headers)
            {
                client.DefaultRequestHeaders.Remove(headerName);
                client.DefaultRequestHeaders.Add(headerName, value);
            }

            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                if (configurationOverrides is not null)
                {
                    configurationBuilder.AddInMemoryCollection(configurationOverrides);
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
