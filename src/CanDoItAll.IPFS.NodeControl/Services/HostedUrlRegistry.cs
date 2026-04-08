namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class HostedUrlRegistry
{
    private readonly object sync = new();
    private string preferredUrl = "http://127.0.0.1:5092";

    public string PreferredUrl
    {
        get
        {
            lock (sync)
            {
                return preferredUrl;
            }
        }
    }

    public void Update(IEnumerable<string> urls)
    {
        var chosen = urls
            .Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri is not null)
            .OrderBy(uri => uri!.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(uri => uri!.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(uri => uri!.ToString())
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(chosen))
        {
            return;
        }

        lock (sync)
        {
            preferredUrl = chosen;
        }
    }
}
