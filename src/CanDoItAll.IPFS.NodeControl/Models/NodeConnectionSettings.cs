namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed class NodeConnectionSettings
{
    public string Label { get; set; } = "Local IPFS node";

    public string BaseUrl { get; set; } = "http://127.0.0.1:5001/";

    public string ApiPath { get; set; } = "api/v0";

    public int TimeoutSeconds { get; set; } = 120;

    public NodeConnectionSettings Clone()
        => new()
        {
            Label = Label,
            BaseUrl = BaseUrl,
            ApiPath = ApiPath,
            TimeoutSeconds = TimeoutSeconds
        };

    public NodeConnectionSettings Normalize()
    {
        Label = string.IsNullOrWhiteSpace(Label) ? "Local IPFS node" : Label.Trim();
        BaseUrl = NormalizeBaseUrl(BaseUrl);
        ApiPath = string.IsNullOrWhiteSpace(ApiPath) ? "api/v0" : ApiPath.Trim().Trim('/');
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 600);
        return this;
    }

    public Uri BuildBaseAddress()
        => new(NormalizeBaseUrl(BaseUrl), UriKind.Absolute);

    private static string NormalizeBaseUrl(string? value)
    {
        var candidate = value?.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
        {
            return "http://127.0.0.1:5001/";
        }

        var builder = new UriBuilder(parsed);
        if (string.IsNullOrWhiteSpace(builder.Path))
        {
            builder.Path = "/";
        }
        else if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return builder.Uri.ToString();
    }
}
