using CanDoItAll.IPFS.NodeControl.Abstractions;

namespace CanDoItAll.IPFS.Tests.NodeControl;

internal sealed class NonStartingNodeHostController : INodeHostController
{
    public string? FindRepoRoot(string? startPath = null)
        => null;

    public bool IsLocalEndpoint(Uri endpoint)
        => false;

    public Task<bool> IsEndpointListeningAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> IsEndpointHealthyAsync(
        Uri endpoint,
        string relativePath,
        HttpMethod? method = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task WaitForEndpointStateAsync(
        Uri endpoint,
        bool shouldBeListening,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool TryStartLocalNodeHost(
        string repoRoot,
        Uri endpoint,
        out int? processId)
    {
        processId = null;
        return false;
    }

    public bool TryStopLocalNodeHost(
        string repoRoot,
        Uri endpoint,
        TimeSpan waitTimeout,
        out int? processId)
    {
        processId = null;
        return false;
    }

    public Task<bool> EnsureOwnedLocalNodeHostExitedAsync(
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
