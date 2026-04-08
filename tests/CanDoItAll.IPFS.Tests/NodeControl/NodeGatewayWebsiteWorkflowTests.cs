using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeGatewayWebsiteWorkflowTests
{
    [TestMethod]
    public async Task Gateway_Serves_Pinned_Website_Index_And_Relative_Assets_In_Preview_Mode()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        await using var gatewayHost = await GatewayHost.StartAsync(apiHost.BaseAddress).ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var websiteRoot = Path.Combine(Path.GetTempPath(), $"node-gateway-site-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path.Combine(websiteRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(websiteRoot, "_framework"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "index.html"),
                """
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <title>Gateway proof</title>
                  <link rel="stylesheet" href="assets/site.css">
                </head>
                <body>
                  <main id="app">Gateway proof</main>
                  <script src="assets/app.js"></script>
                </body>
                </html>
                """,
                Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "assets", "site.css"),
                "body { background: #123456; color: #f8fafc; }",
                Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "assets", "app.js"),
                "document.getElementById('app').setAttribute('data-loaded', 'true');",
                Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllBytesAsync(
                Path.Combine(websiteRoot, "_framework", "dotnet.wasm"),
                [0x00, 0x61, 0x73, 0x6D]).ConfigureAwait(false);

            var uploaded = await service.UploadLocalDirectoryAsync(websiteRoot, pin: true, CancellationToken.None).ConfigureAwait(false);

            using var redirectResponse = await gatewayHost.Client.GetAsync(
                $"/ipfs/{uploaded.ResolvedId}",
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.Found, redirectResponse.StatusCode);
            Assert.AreEqual($"/ipfs/{uploaded.ResolvedId}/", redirectResponse.Headers.Location?.ToString());

            using var indexResponse = await gatewayHost.Client.GetAsync($"/ipfs/{uploaded.ResolvedId}/").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.OK, indexResponse.StatusCode);
            Assert.AreEqual("text/html", indexResponse.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual("preview", GetSingleHeader(indexResponse, "X-Ipfs-NodeControl-Gateway-Mode"));
            Assert.AreEqual("no-store", indexResponse.Headers.CacheControl?.ToString());
            Assert.AreEqual("no-referrer", GetSingleHeader(indexResponse, "Referrer-Policy"));
            Assert.AreEqual("nosniff", GetSingleHeader(indexResponse, "X-Content-Type-Options"));
            Assert.AreEqual("noindex, nofollow, noarchive", GetSingleHeader(indexResponse, "X-Robots-Tag"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(indexResponse.Headers.ETag?.Tag));
            var html = await indexResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            StringAssert.Contains(html, "<link rel=\"stylesheet\" href=\"assets/site.css\">");
            StringAssert.Contains(html, "<script src=\"assets/app.js\"></script>");

            using var cssResponse = await gatewayHost.Client.GetAsync($"/ipfs/{uploaded.ResolvedId}/assets/site.css").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.OK, cssResponse.StatusCode);
            Assert.AreEqual("text/css", cssResponse.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual("preview", GetSingleHeader(cssResponse, "X-Ipfs-NodeControl-Gateway-Mode"));
            Assert.AreEqual("no-store", cssResponse.Headers.CacheControl?.ToString());
            Assert.AreEqual("body { background: #123456; color: #f8fafc; }", await cssResponse.Content.ReadAsStringAsync().ConfigureAwait(false));

            using var scriptResponse = await gatewayHost.Client.GetAsync($"/ipfs/{uploaded.ResolvedId}/assets/app.js").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.OK, scriptResponse.StatusCode);
            StringAssert.Contains(scriptResponse.Content.Headers.ContentType?.MediaType ?? string.Empty, "javascript");
            Assert.AreEqual("no-store", scriptResponse.Headers.CacheControl?.ToString());
            StringAssert.Contains(await scriptResponse.Content.ReadAsStringAsync().ConfigureAwait(false), "data-loaded");

            using var wasmResponse = await gatewayHost.Client.GetAsync($"/ipfs/{uploaded.ResolvedId}/_framework/dotnet.wasm").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.OK, wasmResponse.StatusCode);
            Assert.AreEqual("application/wasm", wasmResponse.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            TryDelete(websiteRoot);
        }
    }

    [TestMethod]
    public async Task Gateway_Uses_Publish_Cache_Posture_And_Disables_Directory_Listings_When_Hardened()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        await using var gatewayHost = await GatewayHost.StartAsync(
            apiHost.BaseAddress,
            new Dictionary<string, string?>
            {
                ["OperatingProfile:Mode"] = "Pro"
            }).ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var websiteRoot = Path.Combine(Path.GetTempPath(), $"node-gateway-publish-site-{Guid.NewGuid():N}");
        var folderRoot = Path.Combine(Path.GetTempPath(), $"node-gateway-publish-folder-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path.Combine(websiteRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(folderRoot, "docs"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "index.html"),
                "<!doctype html><html><body><main>Published gateway proof</main></body></html>",
                Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "assets", "site.css"),
                "body { color: #0f172a; }",
                Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(folderRoot, "readme.txt"),
                "listing should stay hidden",
                Encoding.UTF8).ConfigureAwait(false);

            var publishedSite = await service.UploadLocalDirectoryAsync(websiteRoot, pin: true, CancellationToken.None).ConfigureAwait(false);
            var publishedFolder = await service.UploadLocalDirectoryAsync(folderRoot, pin: true, CancellationToken.None).ConfigureAwait(false);

            using var indexResponse = await gatewayHost.Client.GetAsync($"/ipfs/{publishedSite.ResolvedId}/").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.OK, indexResponse.StatusCode);
            Assert.AreEqual("publish", GetSingleHeader(indexResponse, "X-Ipfs-NodeControl-Gateway-Mode"));
            StringAssert.Contains(indexResponse.Headers.CacheControl?.ToString() ?? string.Empty, "public");
            StringAssert.Contains(indexResponse.Headers.CacheControl?.ToString() ?? string.Empty, "immutable");
            StringAssert.Contains(indexResponse.Headers.CacheControl?.ToString() ?? string.Empty, "max-age=31536000");
            Assert.AreEqual("no-referrer", GetSingleHeader(indexResponse, "Referrer-Policy"));
            Assert.AreEqual("nosniff", GetSingleHeader(indexResponse, "X-Content-Type-Options"));
            Assert.IsFalse(indexResponse.Headers.Contains("X-Robots-Tag"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(indexResponse.Headers.ETag?.Tag));

            using var listingResponse = await gatewayHost.Client.GetAsync($"/ipfs/{publishedFolder.ResolvedId}/").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.NotFound, listingResponse.StatusCode);
        }
        finally
        {
            TryDelete(websiteRoot);
            TryDelete(folderRoot);
        }
    }

    [TestMethod]
    public async Task Gateway_Uses_NoCache_For_Ipns_Content_In_Publish_Mode()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        await using var gatewayHost = await GatewayHost.StartAsync(
            apiHost.BaseAddress,
            new Dictionary<string, string?>
            {
                ["OperatingProfile:Mode"] = "Pro"
            }).ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var websiteRoot = Path.Combine(Path.GetTempPath(), $"node-gateway-ipns-site-{Guid.NewGuid():N}");

        Directory.CreateDirectory(websiteRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "index.html"),
                "<!doctype html><html><body><main>IPNS gateway proof</main></body></html>",
                Encoding.UTF8).ConfigureAwait(false);

            var uploaded = await service.UploadLocalDirectoryAsync(websiteRoot, pin: true, CancellationToken.None).ConfigureAwait(false);
            var published = await service.PublishNameAsync($"/ipfs/{uploaded.ResolvedId}", "self", TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);
            var ipnsPath = published.NamePath.TrimStart('/');

            using var response = await gatewayHost.Client.GetAsync($"/{ipnsPath}/").ConfigureAwait(false);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("publish", GetSingleHeader(response, "X-Ipfs-NodeControl-Gateway-Mode"));
            Assert.AreEqual("no-cache", response.Headers.CacheControl?.ToString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Headers.ETag?.Tag));
        }
        finally
        {
            TryDelete(websiteRoot);
        }
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

    private static string GetSingleHeader(HttpResponseMessage response, string name)
    {
        Assert.IsTrue(response.Headers.TryGetValues(name, out var values), $"Expected header '{name}' to be present.");
        return values.Single();
    }

    private sealed class GatewayHost(IHost host, HttpClient client, Uri baseAddress) : IAsyncDisposable
    {
        public IHost Host { get; } = host;

        public HttpClient Client { get; } = client;

        public Uri BaseAddress { get; } = baseAddress;

        public static async Task<GatewayHost> StartAsync(
            Uri apiBaseAddress,
            IReadOnlyDictionary<string, string?>? configurationOverrides = null)
        {
            var gatewayPort = GetUnusedPort();
            var gatewayBaseAddress = new Uri($"http://127.0.0.1:{gatewayPort}/");
            var targetRegistry = new CurrentNodeTargetRegistry();
            targetRegistry.Update(new NodeConnectionSettings
            {
                BaseUrl = apiBaseAddress.ToString(),
                ApiPath = "api/v0",
                TimeoutSeconds = 30
            }, isHydrated: true);

            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(gatewayBaseAddress.ToString());
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddSingleton(targetRegistry);
                        services.AddSingleton(new LocalNodeBootstrapService(
                            targetRegistry,
                            NullLogger<LocalNodeBootstrapService>.Instance));
                        services.AddOptions<OperatingProfileOptions>()
                            .Configure(options =>
                            {
                                if (configurationOverrides is null)
                                {
                                    return;
                                }

                                if (configurationOverrides.TryGetValue("OperatingProfile:Mode", out var rawMode)
                                    && Enum.TryParse<OperatingProfileMode>(rawMode, ignoreCase: true, out var mode))
                                {
                                    options.Mode = mode;
                                }
                            });
                        services.AddSingleton<IPostConfigureOptions<OperatingProfileOptions>, OperatingProfileOptionsSetup>();
                        services.AddOptions<GatewayPublishingOptions>()
                            .Configure(options =>
                            {
                                if (configurationOverrides is null)
                                {
                                    return;
                                }

                                if (configurationOverrides.TryGetValue("GatewayPublishing:Mode", out var rawMode)
                                    && Enum.TryParse<GatewayPublishingMode>(rawMode, ignoreCase: true, out var gatewayMode))
                                {
                                    options.Mode = gatewayMode;
                                }

                                if (configurationOverrides.TryGetValue("GatewayPublishing:EnableDirectoryListings", out var rawDirectoryListings)
                                    && bool.TryParse(rawDirectoryListings, out var enableDirectoryListings))
                                {
                                    options.EnableDirectoryListings = enableDirectoryListings;
                                }
                            });
                        services.AddSingleton<IPostConfigureOptions<GatewayPublishingOptions>, GatewayPublishingOptionsSetup>();
                        services.AddSingleton<CurrentNodeLeaseFactory>();
                        services.AddSingleton<INodeConnectionLeaseFactory>(serviceProvider => serviceProvider.GetRequiredService<CurrentNodeLeaseFactory>());
                        services.AddSingleton<NodeGatewayService>();
                    });
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapNodeGatewayEndpoints());
                    });
                })
                .Build();

            await host.StartAsync().ConfigureAwait(false);
            var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false
            })
            {
                BaseAddress = gatewayBaseAddress
            };

            return new GatewayHost(host, client, gatewayBaseAddress);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Host.StopAsync().ConfigureAwait(false);
            Host.Dispose();
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
