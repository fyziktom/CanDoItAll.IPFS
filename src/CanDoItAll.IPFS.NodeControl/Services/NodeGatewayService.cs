using System.Net;
using System.Diagnostics;
using Microsoft.Net.Http.Headers;
using System.Text;
using Ipfs;
using Ipfs.Engine.Client.Transport;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeGatewayService(
    INodeConnectionLeaseFactory leaseFactory,
    IOptions<GatewayPublishingOptions> gatewayPublishingOptionsAccessor)
{
    private static readonly string[] DefaultIndexFileNames = ["index.html", "index.htm"];
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = CreateContentTypeProvider();
    private readonly GatewayPublishingOptions gatewayPublishingOptions = gatewayPublishingOptionsAccessor.Value;

    public async Task<NodeGatewayResolution> ResolveAsync(
        string gatewayNamespace,
        string? gatewayPath,
        string requestPath,
        string requestQuery,
        CancellationToken cancellationToken)
    {
        var normalizedTargetPath = BuildGatewayTargetPath(gatewayNamespace, gatewayPath);
        var requestEndsWithSlash = requestPath.EndsWith("/", StringComparison.Ordinal);
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "gateway" },
            { NodeControlTelemetry.OperationTagName, "resolve" },
            { "gateway.namespace", gatewayNamespace },
            { "gateway.target_path", normalizedTargetPath }
        };
        using var activity = NodeControlTelemetry.StartActivity("gateway.resolve", ActivityKind.Internal, tags);
        var lease = await leaseFactory.CreateLeaseWithMinimumTimeoutSecondsAsync(
            60,
            NodeConnectionRequestCategory.Gateway,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var node = await lease.Client.FileSystem.ListFileAsync(normalizedTargetPath, cancellationToken).ConfigureAwait(false);
            if (node.IsDirectory)
            {
                if (!requestEndsWithSlash)
                {
                    lease.Dispose();
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    NodeControlTelemetry.RecordOperation("gateway", "resolve", "redirect", Stopwatch.GetElapsedTime(start), tags);
                    return NodeGatewayResolution.CreateRedirect(
                        $"{requestPath}/{requestQuery}",
                        BuildRedirectPolicy());
                }

                var indexLink = node.Links.FirstOrDefault(link =>
                    DefaultIndexFileNames.Contains(link.Name, StringComparer.OrdinalIgnoreCase));
                if (indexLink is not null)
                {
                    var indexPath = $"{normalizedTargetPath.TrimEnd('/')}/{indexLink.Name}";
                    var stream = await lease.Client.FileSystem.ReadFileAsync(indexPath, cancellationToken).ConfigureAwait(false);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    NodeControlTelemetry.RecordOperation("gateway", "resolve", "file", Stopwatch.GetElapsedTime(start), tags);
                    return NodeGatewayResolution.CreateFile(
                        stream,
                        lease,
                        ResolveContentType(indexLink.Name),
                        BuildFilePolicy(gatewayNamespace, indexLink.Id.ToString()));
                }

                lease.Dispose();
                activity?.SetStatus(ActivityStatusCode.Ok);
                if (gatewayPublishingOptions.EnableDirectoryListings != true)
                {
                    NodeControlTelemetry.RecordOperation("gateway", "resolve", "listing-disabled", Stopwatch.GetElapsedTime(start), tags);
                    return NodeGatewayResolution.CreateNotFound(BuildNotFoundPolicy());
                }

                NodeControlTelemetry.RecordOperation("gateway", "resolve", "listing", Stopwatch.GetElapsedTime(start), tags);
                return NodeGatewayResolution.CreateHtml(
                    BuildDirectoryListingHtml(requestPath, normalizedTargetPath, node.Links),
                    BuildDirectoryListingPolicy());
            }

            var fileName = GetTerminalSegment(normalizedTargetPath);
            var fileStream = await lease.Client.FileSystem.ReadFileAsync(normalizedTargetPath, cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation("gateway", "resolve", "file", Stopwatch.GetElapsedTime(start), tags);
            return NodeGatewayResolution.CreateFile(
                fileStream,
                lease,
                ResolveContentType(fileName),
                BuildFilePolicy(gatewayNamespace, node.Id.ToString()));
        }
        catch (Exception ex)
        {
            lease.Dispose();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation("gateway", "resolve", "failure", Stopwatch.GetElapsedTime(start), tags);
            throw;
        }
    }

    public static bool IsNotFound(Exception exception)
        => exception is FileNotFoundException
           || exception is DirectoryNotFoundException
           || exception is IpfsApiException ipfsApiException && IsNotFoundLikeApiError(ipfsApiException);

    private static bool IsNotFoundLikeApiError(IpfsApiException exception)
    {
        if (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        var message = $"{exception.Message} {exception.RawBody} {string.Join(" ", exception.Details)}";
        return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
               || message.Contains("invalid cid", StringComparison.OrdinalIgnoreCase)
               || message.Contains("cannot resolve", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no link named", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no such file", StringComparison.OrdinalIgnoreCase)
               || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildGatewayTargetPath(string gatewayNamespace, string? gatewayPath)
    {
        var normalizedNamespace = string.Equals(gatewayNamespace, "ipns", StringComparison.OrdinalIgnoreCase)
            ? "ipns"
            : "ipfs";
        var trimmedPath = gatewayPath?.Trim().Trim('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            throw new FileNotFoundException("An IPFS or IPNS path is required.");
        }

        return $"/{normalizedNamespace}/{trimmedPath}";
    }

    private static string ResolveContentType(string fileName)
        => ContentTypeProvider.TryGetContentType(fileName, out var contentType)
            ? contentType
            : "application/octet-stream";

    private static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".wasm"] = "application/wasm";
        provider.Mappings[".webcil"] = "application/octet-stream";
        provider.Mappings[".dll"] = "application/octet-stream";
        provider.Mappings[".dat"] = "application/octet-stream";
        provider.Mappings[".blat"] = "application/octet-stream";
        provider.Mappings[".pdb"] = "application/octet-stream";
        provider.Mappings[".webmanifest"] = "application/manifest+json";
        return provider;
    }

    private static string BuildDirectoryListingHtml(string requestPath, string normalizedTargetPath, IEnumerable<IFileSystemLink> links)
    {
        var orderedLinks = links
            .OrderByDescending(link => link.IsDirectory)
            .ThenBy(link => link.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.Id?.ToString(), StringComparer.Ordinal)
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.Append("  <title>Index of ");
        builder.Append(WebUtility.HtmlEncode(normalizedTargetPath));
        builder.AppendLine("</title>");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("  <style>");
        builder.AppendLine("    body { font-family: system-ui, sans-serif; margin: 2rem; color: #0f172a; background: #f8fafc; }");
        builder.AppendLine("    h1 { font-size: 1.5rem; margin-bottom: 0.5rem; }");
        builder.AppendLine("    p { color: #475569; }");
        builder.AppendLine("    ul { list-style: none; padding: 0; margin: 1.5rem 0 0; }");
        builder.AppendLine("    li + li { margin-top: 0.75rem; }");
        builder.AppendLine("    a { color: #0f766e; text-decoration: none; font-weight: 600; }");
        builder.AppendLine("    a:hover { text-decoration: underline; }");
        builder.AppendLine("    .meta { color: #64748b; font-size: 0.95rem; margin-left: 0.5rem; }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.Append("  <h1>Index of ");
        builder.Append(WebUtility.HtmlEncode(normalizedTargetPath));
        builder.AppendLine("</h1>");
        builder.AppendLine("  <p>No default index file was found in this directory.</p>");
        builder.AppendLine("  <ul>");

        var parentPath = TryGetParentRequestPath(requestPath);
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            builder.Append("    <li><a href=\"");
            builder.Append(WebUtility.HtmlEncode(parentPath));
            builder.AppendLine("\">..</a></li>");
        }

        foreach (var link in orderedLinks)
        {
            var childName = string.IsNullOrWhiteSpace(link.Name)
                ? link.Id?.ToString() ?? "(unnamed)"
                : link.Name;
            var encodedSegment = Uri.EscapeDataString(childName);
            var href = $"{requestPath}{encodedSegment}{(link.IsDirectory ? "/" : string.Empty)}";
            builder.Append("    <li><a href=\"");
            builder.Append(WebUtility.HtmlEncode(href));
            builder.Append("\">");
            builder.Append(WebUtility.HtmlEncode(childName));
            if (link.IsDirectory)
            {
                builder.Append('/');
            }

            builder.Append("</a><span class=\"meta\">");
            builder.Append(WebUtility.HtmlEncode(link.IsDirectory ? "directory" : $"{link.Size:n0} bytes"));
            builder.AppendLine("</span></li>");
        }

        builder.AppendLine("  </ul>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string? TryGetParentRequestPath(string requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return null;
        }

        var trimmed = requestPath.TrimEnd('/');
        var separatorIndex = trimmed.LastIndexOf('/');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var parent = trimmed[..separatorIndex];
        return parent.EndsWith("/", StringComparison.Ordinal)
            ? parent
            : $"{parent}/";
    }

    private static string GetTerminalSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var separatorIndex = trimmed.LastIndexOf('/');
        return separatorIndex >= 0 && separatorIndex < trimmed.Length - 1
            ? trimmed[(separatorIndex + 1)..]
            : trimmed;
    }

    private NodeGatewayResponsePolicy BuildDirectoryListingPolicy()
        => CreateBasePolicy(
            cacheControl: "no-store",
            contentSecurityPolicy: "default-src 'none'; style-src 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
            denyFraming: true,
            suppressIndexing: true);

    private NodeGatewayResponsePolicy BuildFilePolicy(string gatewayNamespace, string entityTagValue)
    {
        var isPreview = GetGatewayMode() == GatewayPublishingMode.Preview;
        var isMutableNamespace = string.Equals(gatewayNamespace, "ipns", StringComparison.OrdinalIgnoreCase);
        var cacheControl = isPreview
            ? "no-store"
            : isMutableNamespace
                ? "no-cache"
                : $"public, max-age={gatewayPublishingOptions.ImmutableFileMaxAgeSeconds}, immutable";

        return CreateBasePolicy(
            cacheControl,
            entityTagValue,
            suppressIndexing: isPreview);
    }

    private NodeGatewayResponsePolicy BuildNotFoundPolicy()
        => CreateBasePolicy(
            cacheControl: "no-store",
            suppressIndexing: GetGatewayMode() == GatewayPublishingMode.Preview);

    private NodeGatewayResponsePolicy BuildRedirectPolicy()
        => CreateBasePolicy(
            cacheControl: "no-store",
            suppressIndexing: GetGatewayMode() == GatewayPublishingMode.Preview);

    private NodeGatewayResponsePolicy CreateBasePolicy(
        string cacheControl,
        string? entityTagValue = null,
        string? contentSecurityPolicy = null,
        bool denyFraming = false,
        bool suppressIndexing = false)
        => new()
        {
            CacheControl = cacheControl,
            ContentSecurityPolicy = contentSecurityPolicy,
            DenyFraming = denyFraming,
            EntityTag = string.IsNullOrWhiteSpace(entityTagValue)
                ? null
                : new EntityTagHeaderValue($"\"{entityTagValue}\""),
            GatewayMode = GetGatewayMode() == GatewayPublishingMode.Publish ? "publish" : "preview",
            SuppressIndexing = suppressIndexing
        };

    private GatewayPublishingMode GetGatewayMode()
        => gatewayPublishingOptions.Mode ?? GatewayPublishingMode.Preview;
}

public sealed class NodeGatewayResolution : IDisposable
{
    private NodeGatewayResolution(NodeGatewayResolutionKind kind, NodeGatewayResponsePolicy responsePolicy)
    {
        Kind = kind;
        ResponsePolicy = responsePolicy;
    }

    public NodeGatewayResolutionKind Kind { get; }

    public NodeGatewayResponsePolicy ResponsePolicy { get; }

    public string? RedirectLocation { get; private init; }

    public string? ContentType { get; private init; }

    public string? Html { get; private init; }

    public Stream? Stream { get; private init; }

    public IpfsClientLease? Lease { get; private init; }

    public static NodeGatewayResolution CreateRedirect(string location, NodeGatewayResponsePolicy responsePolicy)
        => new(NodeGatewayResolutionKind.Redirect, responsePolicy)
        {
            RedirectLocation = location
        };

    public static NodeGatewayResolution CreateHtml(string html, NodeGatewayResponsePolicy responsePolicy)
        => new(NodeGatewayResolutionKind.Html, responsePolicy)
        {
            Html = html,
            ContentType = "text/html; charset=utf-8"
        };

    public static NodeGatewayResolution CreateFile(Stream stream, IpfsClientLease lease, string contentType, NodeGatewayResponsePolicy responsePolicy)
        => new(NodeGatewayResolutionKind.File, responsePolicy)
        {
            Stream = stream,
            Lease = lease,
            ContentType = contentType
        };

    public static NodeGatewayResolution CreateNotFound(NodeGatewayResponsePolicy responsePolicy)
        => new(NodeGatewayResolutionKind.NotFound, responsePolicy);

    public void Dispose()
    {
        Stream?.Dispose();
        Lease?.Dispose();
    }
}

public enum NodeGatewayResolutionKind
{
    Redirect,
    Html,
    File,
    NotFound
}

public sealed class NodeGatewayResponsePolicy
{
    public string? CacheControl { get; init; }

    public string? ContentSecurityPolicy { get; init; }

    public bool DenyFraming { get; init; }

    public EntityTagHeaderValue? EntityTag { get; init; }

    public required string GatewayMode { get; init; }

    public bool SuppressIndexing { get; init; }

    public void Apply(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!string.IsNullOrWhiteSpace(CacheControl))
        {
            httpContext.Response.Headers[HeaderNames.CacheControl] = CacheControl;
        }

        httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        httpContext.Response.Headers["X-Ipfs-NodeControl-Gateway-Mode"] = GatewayMode;

        if (SuppressIndexing)
        {
            httpContext.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        }

        if (!string.IsNullOrWhiteSpace(ContentSecurityPolicy))
        {
            httpContext.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }

        if (DenyFraming)
        {
            httpContext.Response.Headers["X-Frame-Options"] = "DENY";
        }
    }
}
