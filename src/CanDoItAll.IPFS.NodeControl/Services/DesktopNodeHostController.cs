using CanDoItAll.IPFS.DesktopHost;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using Ipfs.Server;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class DesktopNodeHostController : INodeHostController
{
    private static readonly RepoAppDescriptor NodeHostDescriptor =
        new(
            "CanDoItAll IPFS node",
            Path.Combine("src", "CanDoItAll.IPFS.Engine"),
            "CanDoItAll.IPFS.Engine.csproj",
            "CanDoItAll.IPFS.Engine",
            "http://127.0.0.1:5001/");
    private readonly IOptions<HttpApiHostOptions> hostOptions;

    public DesktopNodeHostController(IOptions<HttpApiHostOptions> hostOptions)
    {
        this.hostOptions = hostOptions;
    }

    public DesktopNodeHostController()
        : this(Microsoft.Extensions.Options.Options.Create(new HttpApiHostOptions()))
    {
    }

    public string? FindRepoRoot(string? startPath = null)
        => DesktopAppProcessUtilities.FindAppRoot(NodeHostDescriptor, startPath)
            ?? DesktopAppProcessUtilities.FindRepoRoot(startPath);

    public bool IsLocalEndpoint(Uri endpoint)
        => DesktopAppProcessUtilities.IsLocalEndpoint(endpoint);

    public Task<bool> IsEndpointListeningAsync(Uri endpoint, CancellationToken cancellationToken = default)
        => DesktopAppProcessUtilities.IsTcpEndpointListeningAsync(endpoint, cancellationToken);

    public Task<bool> IsEndpointHealthyAsync(
        Uri endpoint,
        string relativePath,
        HttpMethod? method = null,
        CancellationToken cancellationToken = default)
        => DesktopAppProcessUtilities.IsHttpEndpointHealthyAsync(endpoint, relativePath, method, cancellationToken);

    public Task WaitForEndpointStateAsync(
        Uri endpoint,
        bool shouldBeListening,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => DesktopAppProcessUtilities.WaitForEndpointStateAsync(endpoint, shouldBeListening, timeout, cancellationToken);

    public bool TryStartLocalNodeHost(string repoRoot, Uri endpoint, out int? processId)
    {
        processId = null;
        var environment = new Dictionary<string, string?>
        {
            ["IPFS_NODE_API_URL"] = endpoint.GetLeftPart(UriPartial.Authority)
        };

        var configuredPassphrase = HttpApiHostPassphraseResolver.ResolvePassphrase(hostOptions.Value);
        if (!string.IsNullOrWhiteSpace(configuredPassphrase))
        {
            environment[HttpApiHostPassphraseResolver.EnvironmentVariableName] = configuredPassphrase;
        }

        var startedProcess = DesktopAppProcessUtilities.StartRepoApp(
            repoRoot,
            NodeHostDescriptor,
            environment);
        if (startedProcess is null)
        {
            return false;
        }

        processId = startedProcess.Id;
        startedProcess.Dispose();
        return true;
    }

    public bool TryStopLocalNodeHost(string repoRoot, Uri endpoint, TimeSpan waitTimeout, out int? processId)
        => DesktopAppProcessUtilities.TryStopRepoAppProcess(repoRoot, NodeHostDescriptor, endpoint, waitTimeout, out processId);
}
