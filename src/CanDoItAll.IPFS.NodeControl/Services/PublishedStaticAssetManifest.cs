using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class PublishedStaticAssetManifest
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<StaticAssetCandidate>> routes;
    private readonly FileExtensionContentTypeProvider contentTypeProvider = new();

    private PublishedStaticAssetManifest(Dictionary<string, IReadOnlyList<StaticAssetCandidate>> routes)
    {
        this.routes = routes;
    }

    public static PublishedStaticAssetManifest? TryLoad(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var manifestPath = Path.Combine(baseDirectory, "IpfsNodeControl.staticwebassets.endpoints.json");
        var wwwrootPath = Path.Combine(baseDirectory, "wwwroot");
        if (!File.Exists(manifestPath) || !Directory.Exists(wwwrootPath))
        {
            return null;
        }

        using var stream = File.OpenRead(manifestPath);
        var document = JsonSerializer.Deserialize<StaticAssetManifestDocument>(stream);
        if (document?.Endpoints is null || document.Endpoints.Count == 0)
        {
            return null;
        }

        var routeCandidates = new Dictionary<string, List<StaticAssetCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in document.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Route) || string.IsNullOrWhiteSpace(endpoint.AssetFile))
            {
                continue;
            }

            var filePath = ResolveFilePath(wwwrootPath, endpoint.AssetFile);
            if (filePath is null || !File.Exists(filePath))
            {
                continue;
            }

            var normalizedRoute = NormalizeRoute(endpoint.Route);
            if (!routeCandidates.TryGetValue(normalizedRoute, out var candidates))
            {
                candidates = [];
                routeCandidates[normalizedRoute] = candidates;
            }

            candidates.Add(StaticAssetCandidate.Create(endpoint, filePath));
        }

        if (routeCandidates.Count == 0)
        {
            return null;
        }

        return new PublishedStaticAssetManifest(routeCandidates.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<StaticAssetCandidate>)pair.Value,
            StringComparer.OrdinalIgnoreCase));
    }

    public async Task<bool> TryWriteResponseAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            return false;
        }

        var normalizedRoute = NormalizeRoute(context.Request.Path.Value);
        if (string.IsNullOrWhiteSpace(normalizedRoute)
            || !routes.TryGetValue(normalizedRoute, out var candidates)
            || candidates.Count == 0)
        {
            return false;
        }

        var selected = SelectCandidate(candidates, context.Request.Headers.AcceptEncoding.ToString());
        if (selected is null)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        foreach (var header in selected.ResponseHeaders)
        {
            if (header.Name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                || header.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            context.Response.Headers[header.Name] = header.Value;
        }

        context.Response.ContentType = ResolveContentType(selected.FilePath, selected.ResponseHeaders);

        await using var stream = File.OpenRead(selected.FilePath);
        context.Response.ContentLength = stream.Length;
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return true;
        }

        await stream.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
        return true;
    }

    private string ResolveContentType(string filePath, IReadOnlyList<StaticAssetHeader> headers)
    {
        var manifestType = headers
            .FirstOrDefault(header => header.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        if (!string.IsNullOrWhiteSpace(manifestType))
        {
            return manifestType;
        }

        return contentTypeProvider.TryGetContentType(filePath, out var resolvedType)
            ? resolvedType
            : "application/octet-stream";
    }

    private static StaticAssetCandidate? SelectCandidate(
        IReadOnlyList<StaticAssetCandidate> candidates,
        string acceptEncodingHeader)
    {
        foreach (var encoding in ParseAcceptedEncodings(acceptEncodingHeader))
        {
            var encodedMatch = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.ContentEncoding, encoding, StringComparison.OrdinalIgnoreCase));
            if (encodedMatch is not null)
            {
                return encodedMatch;
            }
        }

        return candidates.FirstOrDefault(candidate => string.IsNullOrWhiteSpace(candidate.ContentEncoding))
            ?? candidates[0];
    }

    private static IEnumerable<string> ParseAcceptedEncodings(string acceptEncodingHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptEncodingHeader))
        {
            yield break;
        }

        var accepted = acceptEncodingHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split(';', 2)[0].Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (accepted.Contains("br") || accepted.Contains("*"))
        {
            yield return "br";
        }

        if (accepted.Contains("gzip") || accepted.Contains("*"))
        {
            yield return "gzip";
        }
    }

    private static string NormalizeRoute(string? route)
        => string.IsNullOrWhiteSpace(route)
            ? string.Empty
            : route.Trim().TrimStart('/');

    private static string? ResolveFilePath(string wwwrootPath, string assetFile)
    {
        var normalizedAssetFile = assetFile.Replace('/', Path.DirectorySeparatorChar);
        var combinedPath = Path.GetFullPath(Path.Combine(wwwrootPath, normalizedAssetFile));
        var normalizedRoot = Path.GetFullPath(wwwrootPath);
        return combinedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? combinedPath
            : null;
    }

    private sealed record StaticAssetCandidate(
        string FilePath,
        string? ContentEncoding,
        IReadOnlyList<StaticAssetHeader> ResponseHeaders)
    {
        public static StaticAssetCandidate Create(StaticAssetManifestEndpoint endpoint, string filePath)
        {
            var headers = (endpoint.ResponseHeaders ?? [])
                .Where(header => !string.IsNullOrWhiteSpace(header.Name))
                .Select(header => new StaticAssetHeader(header.Name.Trim(), header.Value?.Trim() ?? string.Empty))
                .ToList();

            var contentEncoding = headers
                .FirstOrDefault(header => header.Name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return new StaticAssetCandidate(filePath, contentEncoding, headers);
        }
    }

    private sealed record StaticAssetHeader(string Name, string Value);

    private sealed class StaticAssetManifestDocument
    {
        public List<StaticAssetManifestEndpoint> Endpoints { get; init; } = [];
    }

    private sealed class StaticAssetManifestEndpoint
    {
        public string Route { get; init; } = string.Empty;

        public string AssetFile { get; init; } = string.Empty;

        public List<StaticAssetManifestHeader>? ResponseHeaders { get; init; }
    }

    private sealed class StaticAssetManifestHeader
    {
        public string Name { get; init; } = string.Empty;

        public string? Value { get; init; }
    }
}
