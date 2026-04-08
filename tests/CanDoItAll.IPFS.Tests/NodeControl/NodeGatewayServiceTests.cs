using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeGatewayServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_Redirects_Directories_And_Serves_Default_Index_Files()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var gatewayService = CreateGatewayService(apiHost.BaseAddress);
        var nodeOperatorService = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var websiteRoot = CreateTempRoot("node-gateway-service");

        Directory.CreateDirectory(Path.Combine(websiteRoot, "assets"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "index.html"),
                "<!doctype html><html><body><main>Gateway service proof</main></body></html>",
                Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(websiteRoot, "assets", "site.css"),
                "body { color: #0f172a; }",
                Encoding.UTF8).ConfigureAwait(false);

            var uploaded = await nodeOperatorService.UploadLocalDirectoryAsync(websiteRoot, pin: true, CancellationToken.None).ConfigureAwait(false);

            using var redirect = await gatewayService.ResolveAsync(
                "ipfs",
                uploaded.ResolvedId,
                $"/ipfs/{uploaded.ResolvedId}",
                string.Empty,
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(NodeGatewayResolutionKind.Redirect, redirect.Kind);
            Assert.AreEqual($"/ipfs/{uploaded.ResolvedId}/", redirect.RedirectLocation);

            using var index = await gatewayService.ResolveAsync(
                "ipfs",
                uploaded.ResolvedId,
                $"/ipfs/{uploaded.ResolvedId}/",
                string.Empty,
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(NodeGatewayResolutionKind.File, index.Kind);
            Assert.AreEqual("text/html", index.ContentType);
            StringAssert.Contains(await ReadAllTextAsync(index.Stream!).ConfigureAwait(false), "Gateway service proof");

            using var asset = await gatewayService.ResolveAsync(
                "ipfs",
                $"{uploaded.ResolvedId}/assets/site.css",
                $"/ipfs/{uploaded.ResolvedId}/assets/site.css",
                string.Empty,
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(NodeGatewayResolutionKind.File, asset.Kind);
            Assert.AreEqual("text/css", asset.ContentType);
            Assert.AreEqual("body { color: #0f172a; }", await ReadAllTextAsync(asset.Stream!).ConfigureAwait(false));
        }
        finally
        {
            TryDelete(websiteRoot);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_Returns_A_Directory_Listing_When_No_Default_Index_Exists()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var gatewayService = CreateGatewayService(apiHost.BaseAddress);
        var nodeOperatorService = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var folderRoot = CreateTempRoot("node-gateway-listing");

        Directory.CreateDirectory(Path.Combine(folderRoot, "notes"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(folderRoot, "readme.txt"), "directory listing proof", Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(folderRoot, "notes", "child.txt"), "nested", Encoding.UTF8).ConfigureAwait(false);

            var uploaded = await nodeOperatorService.UploadLocalDirectoryAsync(folderRoot, pin: true, CancellationToken.None).ConfigureAwait(false);

            using var listing = await gatewayService.ResolveAsync(
                "ipfs",
                uploaded.ResolvedId,
                $"/ipfs/{uploaded.ResolvedId}/",
                string.Empty,
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(NodeGatewayResolutionKind.Html, listing.Kind);
            Assert.AreEqual("text/html; charset=utf-8", listing.ContentType);
            StringAssert.Contains(listing.Html!, "Index of");
            StringAssert.Contains(listing.Html!, "notes/");
            StringAssert.Contains(listing.Html!, "readme.txt");
            StringAssert.Contains(listing.Html!, "..");
        }
        finally
        {
            TryDelete(folderRoot);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_Publish_Mode_Hides_Directory_Listings_When_No_Default_Index_Exists()
    {
        await using var apiHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var gatewayService = CreateGatewayService(apiHost.BaseAddress, new GatewayPublishingOptions
        {
            Mode = GatewayPublishingMode.Publish,
            EnableDirectoryListings = false
        });
        var nodeOperatorService = NodeOperatorTestHarness.CreateService(apiHost.BaseAddress);
        var folderRoot = CreateTempRoot("node-gateway-no-listing");

        Directory.CreateDirectory(Path.Combine(folderRoot, "notes"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(folderRoot, "readme.txt"), "directory listing proof", Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(folderRoot, "notes", "child.txt"), "nested", Encoding.UTF8).ConfigureAwait(false);

            var uploaded = await nodeOperatorService.UploadLocalDirectoryAsync(folderRoot, pin: true, CancellationToken.None).ConfigureAwait(false);

            using var listing = await gatewayService.ResolveAsync(
                "ipfs",
                uploaded.ResolvedId,
                $"/ipfs/{uploaded.ResolvedId}/",
                string.Empty,
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(NodeGatewayResolutionKind.NotFound, listing.Kind);
            Assert.AreEqual("publish", listing.ResponsePolicy.GatewayMode);
            Assert.AreEqual("no-store", listing.ResponsePolicy.CacheControl);
        }
        finally
        {
            TryDelete(folderRoot);
        }
    }

    private static NodeGatewayService CreateGatewayService(Uri apiBaseAddress, GatewayPublishingOptions? gatewayPublishingOptions = null)
    {
        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(new NodeConnectionSettings
        {
            Label = "Gateway node",
            BaseUrl = apiBaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 30
        }, isHydrated: true);

        var bootstrapService = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);
        var leaseFactory = new CurrentNodeLeaseFactory(targetRegistry, bootstrapService);
        return new NodeGatewayService(
            leaseFactory,
            Options.Create(gatewayPublishingOptions ?? new GatewayPublishingOptions
            {
                Mode = GatewayPublishingMode.Preview,
                EnableDirectoryListings = true
            }));
    }

    private static async Task<string> ReadAllTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string CreateTempRoot(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
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
