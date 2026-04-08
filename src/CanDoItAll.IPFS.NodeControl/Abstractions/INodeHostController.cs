using System.Net.Http;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface INodeHostController
{
    string? FindRepoRoot(string? startPath = null);

    bool IsLocalEndpoint(Uri endpoint);

    Task<bool> IsEndpointListeningAsync(Uri endpoint, CancellationToken cancellationToken = default);

    Task<bool> IsEndpointHealthyAsync(
        Uri endpoint,
        string relativePath,
        HttpMethod? method = null,
        CancellationToken cancellationToken = default);

    Task WaitForEndpointStateAsync(
        Uri endpoint,
        bool shouldBeListening,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    bool TryStartLocalNodeHost(string repoRoot, Uri endpoint, out int? processId);

    bool TryStopLocalNodeHost(string repoRoot, Uri endpoint, TimeSpan waitTimeout, out int? processId);
}
