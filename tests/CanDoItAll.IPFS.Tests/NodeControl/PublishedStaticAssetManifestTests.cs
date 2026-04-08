using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class PublishedStaticAssetManifestTests
{
    [TestMethod]
    public async Task TryWriteResponseAsync_Serves_Styles_Icons_Scripts_And_FrameworkAssets_From_Published_Manifest()
    {
        var publishRoot = CreatePublishRoot();

        try
        {
            WriteAsset(publishRoot, "wwwroot/app.css", "body{background:#123456;}");
            WriteAsset(publishRoot, "wwwroot/favicon.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            WriteAsset(publishRoot, "wwwroot/js/filesExplorer.js", "console.log('files');");
            WriteAsset(publishRoot, "wwwroot/vendor/material-symbols/material-symbols-rounded.css", ".material-symbols-rounded{font-family:'Material Symbols Rounded';}");
            WriteAsset(publishRoot, "wwwroot/vendor/material-symbols/material-symbols-rounded.woff2", "font-binary-placeholder");
            WriteAsset(publishRoot, "wwwroot/_content/CanDoItAll.Components.BaseLib/css/output.css", ".shell{display:grid;}");
            WriteAsset(publishRoot, "wwwroot/_framework/blazor.server.js", "console.log('framework');");
            WriteManifest(
                publishRoot,
                Endpoint("app.css", "app.css", ContentType("text/css")),
                Endpoint("favicon.svg", "favicon.svg", ContentType("image/svg+xml")),
                Endpoint("js/filesExplorer.js", "js/filesExplorer.js", ContentType("text/javascript")),
                Endpoint("vendor/material-symbols/material-symbols-rounded.css", "vendor/material-symbols/material-symbols-rounded.css", ContentType("text/css")),
                Endpoint("vendor/material-symbols/material-symbols-rounded.woff2", "vendor/material-symbols/material-symbols-rounded.woff2", ContentType("font/woff2")),
                Endpoint("_content/CanDoItAll.Components.BaseLib/css/output.css", "_content/CanDoItAll.Components.BaseLib/css/output.css", ContentType("text/css")),
                Endpoint("_framework/blazor.server.js", "_framework/blazor.server.js", ContentType("text/javascript")));

            var manifest = PublishedStaticAssetManifest.TryLoad(publishRoot);

            Assert.IsNotNull(manifest);
            await AssertServedAssetAsync(manifest, "/app.css", "text/css", "body{background:#123456;}").ConfigureAwait(false);
            await AssertServedAssetAsync(manifest, "/favicon.svg", "image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>").ConfigureAwait(false);
            await AssertServedAssetAsync(manifest, "/js/filesExplorer.js", "text/javascript", "console.log('files');").ConfigureAwait(false);
            await AssertServedAssetAsync(
                manifest,
                "/vendor/material-symbols/material-symbols-rounded.css",
                "text/css",
                ".material-symbols-rounded{font-family:'Material Symbols Rounded';}").ConfigureAwait(false);
            await AssertServedAssetAsync(
                manifest,
                "/vendor/material-symbols/material-symbols-rounded.woff2",
                "font/woff2",
                "font-binary-placeholder").ConfigureAwait(false);
            await AssertServedAssetAsync(
                manifest,
                "/_content/CanDoItAll.Components.BaseLib/css/output.css",
                "text/css",
                ".shell{display:grid;}").ConfigureAwait(false);
            await AssertServedAssetAsync(
                manifest,
                "/_framework/blazor.server.js",
                "text/javascript",
                "console.log('framework');").ConfigureAwait(false);
        }
        finally
        {
            TryDelete(publishRoot);
        }
    }

    [TestMethod]
    public async Task TryWriteResponseAsync_Prefers_Brotli_Encoded_Asset_When_Client_Accepts_It()
    {
        var publishRoot = CreatePublishRoot();

        try
        {
            WriteAsset(publishRoot, "wwwroot/app.css", "plain-css");
            WriteAsset(publishRoot, "wwwroot/app.css.br", "brotli-css");
            WriteManifest(
                publishRoot,
                Endpoint("app.css", "app.css", ContentType("text/css")),
                Endpoint("app.css", "app.css.br", ContentType("text/css"), ContentEncoding("br")));

            var manifest = PublishedStaticAssetManifest.TryLoad(publishRoot);
            Assert.IsNotNull(manifest);

            var context = CreateHttpContext("/app.css", "GET");
            context.Request.Headers.AcceptEncoding = "gzip, br";

            var handled = await manifest.TryWriteResponseAsync(context).ConfigureAwait(false);

            Assert.IsTrue(handled);
            Assert.AreEqual("br", context.Response.Headers.ContentEncoding.ToString());
            Assert.AreEqual("text/css", context.Response.ContentType);
            Assert.AreEqual("brotli-css", ReadBody(context));
        }
        finally
        {
            TryDelete(publishRoot);
        }
    }

    private static async Task AssertServedAssetAsync(
        PublishedStaticAssetManifest manifest,
        string requestPath,
        string expectedContentType,
        string expectedBody)
    {
        var context = CreateHttpContext(requestPath, "GET");

        var handled = await manifest.TryWriteResponseAsync(context).ConfigureAwait(false);

        Assert.IsTrue(handled);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual(expectedContentType, context.Response.ContentType);
        Assert.AreEqual(expectedBody, ReadBody(context));
    }

    private static DefaultHttpContext CreateHttpContext(string requestPath, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = requestPath;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static object Endpoint(string route, string assetFile, params object[] headers)
        => new
        {
            Route = route,
            AssetFile = assetFile,
            ResponseHeaders = headers
        };

    private static object ContentType(string value)
        => new
        {
            Name = "Content-Type",
            Value = value
        };

    private static object ContentEncoding(string value)
        => new
        {
            Name = "Content-Encoding",
            Value = value
        };

    private static void WriteManifest(string publishRoot, params object[] endpoints)
    {
        var manifestPath = Path.Combine(publishRoot, "IpfsNodeControl.staticwebassets.endpoints.json");
        var json = JsonSerializer.Serialize(new { Endpoints = endpoints });
        File.WriteAllText(manifestPath, json);
    }

    private static void WriteAsset(string publishRoot, string relativePath, string content)
    {
        var fullPath = Path.Combine(publishRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(fullPath, content);
    }

    private static string CreatePublishRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "published-static-assets-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "wwwroot"));
        return path;
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
            // Best-effort temp cleanup only.
        }
    }
}
