using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class LocalNodeBootstrapService
{
    private const string ManagedApiPath = "api/v0";
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(20);

    private readonly CurrentNodeTargetRegistry targetRegistry;
    private readonly INodeHostController nodeHostController;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<LocalNodeBootstrapService> logger;
    private readonly SemaphoreSlim startGate = new(1, 1);

    public LocalNodeBootstrapService(
        CurrentNodeTargetRegistry targetRegistry,
        INodeHostController nodeHostController,
        IHttpClientFactory httpClientFactory,
        ILogger<LocalNodeBootstrapService> logger)
    {
        this.targetRegistry = targetRegistry;
        this.nodeHostController = nodeHostController;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public LocalNodeBootstrapService(
        CurrentNodeTargetRegistry targetRegistry,
        INodeHostController nodeHostController,
        ILogger<LocalNodeBootstrapService> logger)
        : this(
            targetRegistry,
            nodeHostController,
            NodeControlServiceCollectionExtensions.CreateCompatibilityHttpClientFactory(),
            logger)
    {
    }

    public LocalNodeBootstrapService(
        CurrentNodeTargetRegistry targetRegistry,
        ILogger<LocalNodeBootstrapService> logger)
        : this(
            targetRegistry,
            new DesktopNodeHostController(),
            NodeControlServiceCollectionExtensions.CreateCompatibilityHttpClientFactory(),
            logger)
    {
    }

    public Task EnsureNodeForCurrentTargetAsync(CancellationToken cancellationToken = default)
        => EnsureNodeForSettingsAsync(targetRegistry.Current, cancellationToken);

    public async Task EnsureNodeForSettingsAsync(NodeConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!targetRegistry.IsHydrated)
        {
            logger.LogDebug("Skipping local node bootstrap because the target registry has not been hydrated yet.");
            return;
        }

        var normalized = settings.Clone().Normalize();
        var endpoint = normalized.BuildBaseAddress();
        logger.LogInformation("Evaluating local node bootstrap for endpoint {Endpoint}.", endpoint);
        if (!nodeHostController.IsLocalEndpoint(endpoint))
        {
            logger.LogInformation("Skipping local node bootstrap because endpoint {Endpoint} is not local.", endpoint);
            return;
        }

        if (await IsEndpointHealthyAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Skipping local node bootstrap because endpoint {Endpoint} is healthy.", endpoint);
            return;
        }

        if (await nodeHostController.IsEndpointListeningAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Endpoint {Endpoint} is listening but failed the managed local-node health probe. Attempting recovery.", endpoint);
            await RecoverUnhealthyLocalNodeAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return;
        }

        await StartLocalNodeCoreAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NodeConnectionSettings> ResolveStartupSettingsAsync(
        NodeConnectionSettings preferredSettings,
        NodeConnectionSettings fallbackSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferredSettings);
        ArgumentNullException.ThrowIfNull(fallbackSettings);

        var preferred = preferredSettings.Clone().Normalize();
        var preferredEndpoint = preferred.BuildBaseAddress();
        if (nodeHostController.IsLocalEndpoint(preferredEndpoint))
        {
            await EnsureNodeForSettingsAsync(preferred, cancellationToken).ConfigureAwait(false);
            return preferred;
        }

        if (await IsEndpointHealthyAsync(preferredEndpoint, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation(
                "Using configured remote endpoint {Endpoint} because it passed the startup health probe.",
                preferredEndpoint);
            return preferred;
        }

        var fallback = fallbackSettings.Clone().Normalize();
        var fallbackEndpoint = fallback.BuildBaseAddress();
        if (Uri.Compare(preferredEndpoint, fallbackEndpoint, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            logger.LogWarning(
                "Configured endpoint {Endpoint} failed the startup health probe and no distinct fallback endpoint is available.",
                preferredEndpoint);
            return preferred;
        }

        if (!nodeHostController.IsLocalEndpoint(fallbackEndpoint))
        {
            logger.LogWarning(
                "Configured endpoint {Endpoint} failed the startup health probe, but fallback endpoint {FallbackEndpoint} is not local.",
                preferredEndpoint,
                fallbackEndpoint);
            return preferred;
        }

        logger.LogWarning(
            "Configured endpoint {Endpoint} failed the startup health probe. Falling back to local endpoint {FallbackEndpoint}.",
            preferredEndpoint,
            fallbackEndpoint);

        try
        {
            await EnsureNodeForSettingsAsync(fallback, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Fallback local endpoint {FallbackEndpoint} could not be started after configured endpoint {Endpoint} failed.",
                fallbackEndpoint,
                preferredEndpoint);
            return preferred;
        }

        if (await IsEndpointHealthyAsync(fallbackEndpoint, cancellationToken).ConfigureAwait(false))
        {
            return fallback;
        }

        logger.LogWarning(
            "Fallback local endpoint {FallbackEndpoint} did not pass the startup health probe after configured endpoint {Endpoint} failed.",
            fallbackEndpoint,
            preferredEndpoint);
        return preferred;
    }

    public async Task StartLocalNodeAsync(CancellationToken cancellationToken = default)
    {
        var settings = targetRegistry.Current;
        settings.Normalize();

        if (!nodeHostController.IsLocalEndpoint(settings.BuildBaseAddress()))
        {
            throw new InvalidOperationException("The current node target is not a local endpoint, so the local node host will not be started.");
        }

        var endpoint = settings.BuildBaseAddress();
        if (await IsEndpointHealthyAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (await nodeHostController.IsEndpointListeningAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Endpoint {Endpoint} is listening but failed the managed local-node health probe. Attempting recovery.", endpoint);
            await RecoverUnhealthyLocalNodeAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return;
        }

        await StartLocalNodeCoreAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopLocalNodeAsync(CancellationToken cancellationToken = default)
    {
        var settings = targetRegistry.Current;
        settings.Normalize();

        if (!nodeHostController.IsLocalEndpoint(settings.BuildBaseAddress()))
        {
            throw new InvalidOperationException("The current node target is not a local endpoint, so the local node host cannot be stopped from this tray menu.");
        }

        var endpoint = settings.BuildBaseAddress();
        if (!await nodeHostController.IsEndpointListeningAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!await IsEndpointHealthyAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Endpoint {Endpoint} is listening but failed the managed local-node health probe. Attempting to stop the local repo-owned process directly.", endpoint);
            await StopUnhealthyLocalNodeAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var httpClient = CreateAdminHttpClient(endpoint, settings.TimeoutSeconds);

        try
        {
            using var response = await httpClient.PostAsync($"{ManagedApiPath}/shutdown", content: null, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            if (await nodeHostController.IsEndpointListeningAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }
        }
        catch (TaskCanceledException)
        {
            if (await nodeHostController.IsEndpointListeningAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }
        }

        await nodeHostController.WaitForEndpointStateAsync(endpoint, shouldBeListening: false, StopTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestartLocalNodeAsync(CancellationToken cancellationToken = default)
    {
        var settings = targetRegistry.Current;
        settings.Normalize();

        if (!nodeHostController.IsLocalEndpoint(settings.BuildBaseAddress()))
        {
            throw new InvalidOperationException("The current node target is not a local endpoint, so the local node host cannot be restarted from this tray menu.");
        }

        await StopLocalNodeAsync(cancellationToken).ConfigureAwait(false);
        await StartLocalNodeCoreAsync(settings.BuildBaseAddress(), cancellationToken).ConfigureAwait(false);
    }

    private async Task StartLocalNodeCoreAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        if (await IsEndpointHealthyAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await IsEndpointHealthyAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (await nodeHostController.IsEndpointListeningAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await RecoverUnhealthyLocalNodeAsync(endpoint, cancellationToken).ConfigureAwait(false);
                return;
            }

            var applicationRoot = nodeHostController.FindRepoRoot();
            if (string.IsNullOrWhiteSpace(applicationRoot))
            {
                throw new InvalidOperationException("Could not locate a repository or published deployment root to start the local IPFS host.");
            }

            logger.LogInformation("Starting local node host from application root {ApplicationRoot} for endpoint {Endpoint}.", applicationRoot, endpoint);

            if (!nodeHostController.TryStartLocalNodeHost(applicationRoot, endpoint, out var processId))
            {
                throw new InvalidOperationException("Could not start the local IPFS host from the current repository or published deployment layout.");
            }

            logger.LogInformation(
                "Started local node host process {ProcessId} for endpoint {Endpoint}.",
                processId,
                endpoint);

            await nodeHostController.WaitForEndpointStateAsync(endpoint, shouldBeListening: true, StartTimeout, cancellationToken).ConfigureAwait(false);
            await WaitForHealthyEndpointAsync(endpoint, StartTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            startGate.Release();
        }
    }

    private Task<bool> IsEndpointHealthyAsync(Uri endpoint, CancellationToken cancellationToken)
        => nodeHostController.IsEndpointHealthyAsync(
            endpoint,
            $"{ManagedApiPath}/version",
            HttpMethod.Post,
            cancellationToken);

    private HttpClient CreateAdminHttpClient(Uri endpoint, int timeoutSeconds)
    {
        var httpClient = httpClientFactory.CreateClient(NodeControlHttpClientNames.NodeAdmin);
        httpClient.BaseAddress = endpoint;
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 30));
        return httpClient;
    }

    private async Task RecoverUnhealthyLocalNodeAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        await StopUnhealthyLocalNodeAsync(endpoint, cancellationToken).ConfigureAwait(false);
        await StartLocalNodeCoreAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task StopUnhealthyLocalNodeAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var applicationRoot = nodeHostController.FindRepoRoot();
        if (string.IsNullOrWhiteSpace(applicationRoot))
        {
            throw new InvalidOperationException($"Endpoint {endpoint} is listening but the managed local-node health probe failed, and no repository or published deployment root could be located for recovery.");
        }

        if (!nodeHostController.TryStopLocalNodeHost(applicationRoot, endpoint, StopTimeout, out var processId))
        {
            throw new InvalidOperationException($"Endpoint {endpoint} is listening but the managed local-node health probe failed, and no repo-owned local node process could be stopped automatically.");
        }

        logger.LogWarning("Stopped unhealthy local node host process {ProcessId} for endpoint {Endpoint}.", processId, endpoint);
        await nodeHostController.WaitForEndpointStateAsync(endpoint, shouldBeListening: false, StopTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForHealthyEndpointAsync(Uri endpoint, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (!timeoutCts.IsCancellationRequested)
            {
                if (await IsEndpointHealthyAsync(endpoint, timeoutCts.Token).ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Let the method throw the timeout-specific message below.
        }

        throw new TimeoutException($"Timed out waiting for {endpoint} to pass the managed local-node health probe.");
    }
}
