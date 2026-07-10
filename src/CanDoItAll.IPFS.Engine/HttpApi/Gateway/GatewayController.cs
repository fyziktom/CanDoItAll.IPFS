using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Ipfs.CoreApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Ipfs.Server.HttpApi.Gateway
{
    /// <summary>
    ///   Serves immutable IPFS content through the conventional path gateway.
    /// </summary>
    [AllowAnonymous]
    [Route("ipfs")]
    public sealed class GatewayController : Controller
    {
        private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
        private static readonly string[] DefaultIndexFileNames = ["index.html", "index.htm"];
        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
        private static readonly byte[] LittleEndianTiffSignature = [0x49, 0x49, 0x2A, 0x00];
        private static readonly byte[] BigEndianTiffSignature = [0x4D, 0x4D, 0x00, 0x2A];
        private static readonly byte[] IconSignature = [0x00, 0x00, 0x01, 0x00];
        private readonly ICoreApi ipfs;

        public GatewayController(ICoreApi ipfs)
        {
            this.ipfs = ipfs;
        }

        /// <summary>
        ///   Gets a file or directory below an immutable IPFS path.
        /// </summary>
        [HttpGet("{**gatewayPath}")]
        [HttpHead("{**gatewayPath}")]
        public async Task<IActionResult> GetAsync(string? gatewayPath)
        {
            var normalizedPath = gatewayPath?.Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return NotFound();
            }

            var ipfsPath = $"/ipfs/{normalizedPath}";
            IFileSystemNode node;
            try
            {
                node = await ipfs.FileSystem.ListFileAsync(ipfsPath, HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsMissingOrInvalidPath(exception))
            {
                return NotFound();
            }

            if (node.IsDirectory)
            {
                return await ServeDirectoryAsync(ipfsPath, node).ConfigureAwait(false);
            }

            return await ServeFileAsync(ipfsPath, node.Id.ToString(), GetTerminalSegment(normalizedPath)).ConfigureAwait(false);
        }

        private async Task<IActionResult> ServeDirectoryAsync(string ipfsPath, IFileSystemNode node)
        {
            var requestPath = HttpContext.Request.Path.Value ?? string.Empty;
            if (!requestPath.EndsWith("/", StringComparison.Ordinal))
            {
                return Redirect($"{requestPath}/{HttpContext.Request.QueryString}");
            }

            var index = node.Links.FirstOrDefault(link =>
                DefaultIndexFileNames.Contains(link.Name, StringComparer.OrdinalIgnoreCase));
            if (index is not null)
            {
                return await ServeFileAsync(
                    $"{ipfsPath.TrimEnd('/')}/{index.Name}",
                    index.Id.ToString(),
                    index.Name).ConfigureAwait(false);
            }

            ApplyImmutableHeaders(node.Id.ToString());
            Response.Headers[HeaderNames.ContentSecurityPolicy] =
                "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            Response.Headers[HeaderNames.XFrameOptions] = "DENY";
            return Content(BuildDirectoryListing(requestPath, ipfsPath, node.Links), "text/html; charset=utf-8");
        }

        private async Task<IActionResult> ServeFileAsync(string ipfsPath, string entityTag, string fileName)
        {
            Stream stream;
            try
            {
                stream = await ipfs.FileSystem.ReadFileAsync(ipfsPath, HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsMissingOrInvalidPath(exception))
            {
                return NotFound();
            }

            var contentType = await ResolveContentTypeAsync(stream, fileName).ConfigureAwait(false);
            ApplyImmutableHeaders(entityTag);
            return File(
                stream,
                contentType,
                fileDownloadName: null,
                lastModified: null,
                entityTag: new EntityTagHeaderValue($"\"{entityTag}\""),
                enableRangeProcessing: stream.CanSeek);
        }

        private void ApplyImmutableHeaders(string entityTag)
        {
            Response.Headers[HeaderNames.CacheControl] = ImmutableCacheControl;
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            Response.Headers[HeaderNames.ETag] = $"\"{entityTag}\"";
        }

        private async Task<string> ResolveContentTypeAsync(Stream stream, string fileName)
        {
            if (ContentTypeProvider.TryGetContentType(fileName, out var contentType))
            {
                return contentType;
            }

            if (!stream.CanSeek)
            {
                return "application/octet-stream";
            }

            var originalPosition = stream.Position;
            var prefix = new byte[512];
            var bytesRead = 0;
            try
            {
                while (bytesRead < prefix.Length)
                {
                    var read = await stream
                        .ReadAsync(prefix.AsMemory(bytesRead, prefix.Length - bytesRead), HttpContext.RequestAborted)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    bytesRead += read;
                }
            }
            finally
            {
                stream.Position = originalPosition;
            }

            return DetectContentType(prefix.AsSpan(0, bytesRead));
        }

        private static string DetectContentType(ReadOnlySpan<byte> prefix)
        {
            if (prefix.StartsWith(PngSignature))
            {
                return "image/png";
            }

            if (prefix.StartsWith(JpegSignature))
            {
                return "image/jpeg";
            }

            if (prefix.StartsWith("GIF87a"u8) || prefix.StartsWith("GIF89a"u8))
            {
                return "image/gif";
            }

            if (prefix.Length >= 12 && prefix[..4].SequenceEqual("RIFF"u8) && prefix.Slice(8, 4).SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            if (prefix.StartsWith("BM"u8))
            {
                return "image/bmp";
            }

            if (prefix.StartsWith(LittleEndianTiffSignature) || prefix.StartsWith(BigEndianTiffSignature))
            {
                return "image/tiff";
            }

            if (prefix.StartsWith(IconSignature))
            {
                return "image/x-icon";
            }

            if (prefix.StartsWith("%PDF-"u8))
            {
                return "application/pdf";
            }

            if (prefix.Length >= 12 && prefix[..4].SequenceEqual("RIFF"u8) && prefix.Slice(8, 4).SequenceEqual("WAVE"u8))
            {
                return "audio/wav";
            }

            if (prefix.StartsWith("OggS"u8))
            {
                return "application/ogg";
            }

            if (prefix.StartsWith("ID3"u8))
            {
                return "audio/mpeg";
            }

            if (prefix.Length >= 12 && prefix.Slice(4, 4).SequenceEqual("ftyp"u8))
            {
                var brand = prefix.Slice(8, 4);
                if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8))
                {
                    return "image/avif";
                }

                return "video/mp4";
            }

            return "application/octet-stream";
        }

        private static bool IsMissingOrInvalidPath(Exception exception)
            => exception is ArgumentException
               || exception is FormatException
               || exception is KeyNotFoundException
               || exception is FileNotFoundException
               || exception is DirectoryNotFoundException;

        private static string GetTerminalSegment(string path)
        {
            var separatorIndex = path.LastIndexOf('/');
            return separatorIndex >= 0 && separatorIndex < path.Length - 1
                ? path[(separatorIndex + 1)..]
                : path;
        }

        private static string BuildDirectoryListing(
            string requestPath,
            string ipfsPath,
            IEnumerable<IFileSystemLink> links)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
            builder.Append("<title>Index of ").Append(WebUtility.HtmlEncode(ipfsPath)).AppendLine("</title>");
            builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            builder.AppendLine("<style>body{font-family:system-ui,sans-serif;margin:2rem}li{margin:.5rem 0}</style></head><body>");
            builder.Append("<h1>Index of ").Append(WebUtility.HtmlEncode(ipfsPath)).AppendLine("</h1><ul>");

            foreach (var link in links
                .OrderByDescending(link => link.IsDirectory)
                .ThenBy(link => link.Name, StringComparer.OrdinalIgnoreCase))
            {
                var name = string.IsNullOrWhiteSpace(link.Name) ? link.Id.ToString() : link.Name;
                var suffix = link.IsDirectory ? "/" : string.Empty;
                builder.Append("<li><a href=\"")
                    .Append(WebUtility.HtmlEncode($"{requestPath}{Uri.EscapeDataString(name)}{suffix}"))
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(name))
                    .Append(suffix)
                    .AppendLine("</a></li>");
            }

            builder.AppendLine("</ul></body></html>");
            return builder.ToString();
        }
    }
}
