using System.Diagnostics;
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
    private readonly SemaphoreSlim processGate = new(1, 1);
    private Process? ownedNodeHost;
    private string? ownedEndpointKey;

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
        var endpointKey = GetEndpointKey(endpoint);

        processGate.Wait();
        try
        {
            if (ownedNodeHost is not null)
            {
                processId = TryGetProcessId(ownedNodeHost);
                if (!HasExited(ownedNodeHost))
                {
                    return false;
                }

                ReleaseOwnedNodeHost();
            }

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
            ownedNodeHost = startedProcess;
            ownedEndpointKey = endpointKey;
            return true;
        }
        finally
        {
            processGate.Release();
        }
    }

    public bool TryStopLocalNodeHost(string repoRoot, Uri endpoint, TimeSpan waitTimeout, out int? processId)
    {
        _ = repoRoot;
        processId = null;
        var endpointKey = GetEndpointKey(endpoint);

        processGate.Wait();
        try
        {
            if (ownedNodeHost is null
                || !string.Equals(ownedEndpointKey, endpointKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            processId = TryGetProcessId(ownedNodeHost);
            var exited = HasExited(ownedNodeHost)
                || TryKillAndWaitForExit(ownedNodeHost, waitTimeout);
            if (exited)
            {
                ReleaseOwnedNodeHost();
            }

            return exited;
        }
        finally
        {
            processGate.Release();
        }
    }

    public async Task<bool> EnsureOwnedLocalNodeHostExitedAsync(
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (gracefulTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(gracefulTimeout), "The graceful process-exit timeout must be positive.");
        }

        await processGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ownedNodeHost is null)
            {
                return true;
            }

            var process = ownedNodeHost;
            var exited = HasExited(process);
            if (!exited)
            {
                using var gracefulCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                gracefulCts.CancelAfter(gracefulTimeout);

                try
                {
                    await process.WaitForExitAsync(gracefulCts.Token).ConfigureAwait(false);
                    exited = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    exited = TryKill(process);
                    if (exited)
                    {
                        using var forcedExitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        forcedExitCts.CancelAfter(gracefulTimeout);
                        try
                        {
                            await process.WaitForExitAsync(forcedExitCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            exited = false;
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    exited = HasExited(process);
                }
            }

            if (exited || HasExited(process))
            {
                ReleaseOwnedNodeHost();
                return true;
            }

            return false;
        }
        finally
        {
            processGate.Release();
        }
    }

    private static string GetEndpointKey(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("The node endpoint must be an absolute URI.", nameof(endpoint));
        }

        var host = endpoint.IsLoopback
            ? "loopback"
            : endpoint.IdnHost.TrimEnd('.').ToLowerInvariant();
        return $"{endpoint.Scheme.ToLowerInvariant()}://{host}:{endpoint.Port}";
    }

    private void ReleaseOwnedNodeHost()
    {
        var process = ownedNodeHost;
        ownedNodeHost = null;
        ownedEndpointKey = null;
        process?.Dispose();
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool TryKillAndWaitForExit(Process process, TimeSpan waitTimeout)
    {
        if (!TryKill(process))
        {
            return HasExited(process);
        }

        try
        {
            var waitMilliseconds = (int)Math.Clamp(waitTimeout.TotalMilliseconds, 0, int.MaxValue);
            return process.WaitForExit(waitMilliseconds) && process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return HasExited(process);
        }
    }

    private static bool TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            return HasExited(process);
        }
        catch
        {
            return false;
        }
    }
}
