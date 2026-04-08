using Ipfs.Engine.Client;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Models;
using System.Diagnostics;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class CurrentNodeLeaseFactory : INodeConnectionLeaseFactory, INodeConnectionDriver
{
    private const int MaximumLeaseTimeoutSeconds = 1800;
    private const NodeConnectionRequestCategory DefaultCompatibilityCategory = NodeConnectionRequestCategory.ReadOnlyUi;

    private readonly CurrentNodeTargetRegistry targetRegistry;
    private readonly LocalNodeBootstrapService localNodeBootstrapService;
    private readonly IHttpClientFactory httpClientFactory;

    public CurrentNodeLeaseFactory(
        CurrentNodeTargetRegistry targetRegistry,
        LocalNodeBootstrapService localNodeBootstrapService,
        IHttpClientFactory httpClientFactory)
    {
        this.targetRegistry = targetRegistry;
        this.localNodeBootstrapService = localNodeBootstrapService;
        this.httpClientFactory = httpClientFactory;
    }

    public CurrentNodeLeaseFactory(
        CurrentNodeTargetRegistry targetRegistry,
        LocalNodeBootstrapService localNodeBootstrapService)
        : this(
            targetRegistry,
            localNodeBootstrapService,
            NodeControlServiceCollectionExtensions.CreateCompatibilityHttpClientFactory())
    {
    }

    public NodeConnectionSettings CurrentSettings => targetRegistry.Current;

    public IpfsClientLease CreateLease()
        => CreateLeaseAsync(DefaultCompatibilityCategory).GetAwaiter().GetResult();

    public IpfsClientLease CreateLeaseWithMinimumTimeoutSeconds(int minimumTimeoutSeconds)
        => CreateLeaseWithMinimumTimeoutSecondsAsync(
            minimumTimeoutSeconds,
            DefaultCompatibilityCategory).GetAwaiter().GetResult();

    public IpfsClientLease CreateLease(NodeConnectionSettings settings)
        => CreateLeaseAsync(settings, DefaultCompatibilityCategory).GetAwaiter().GetResult();

    public Task<IpfsClientLease> CreateLeaseAsync(
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default)
        => CreateLeaseAsync(targetRegistry.Current, category, cancellationToken);

    public Task<IpfsClientLease> CreateLeaseWithMinimumTimeoutSecondsAsync(
        int minimumTimeoutSeconds,
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default)
    {
        var settings = CreateSettingsWithMinimumTimeoutSeconds(targetRegistry.Current, minimumTimeoutSeconds);
        return CreateLeaseAsync(settings, category, cancellationToken);
    }

    public async Task<IpfsClientLease> CreateLeaseAsync(
        NodeConnectionSettings settings,
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = NormalizeSettings(settings);
        var baseAddress = normalized.BuildBaseAddress();
        var clientName = ResolveHttpClientName(category);
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "node" },
            { NodeControlTelemetry.OperationTagName, "create-lease" },
            { "node.category", category.ToString() },
            { "node.base_url", normalized.BaseUrl },
            { "node.api_path", normalized.ApiPath },
            { "http.client_name", clientName }
        };
        using var activity = NodeControlTelemetry.StartActivity("node.create-lease", ActivityKind.Client, tags);

        try
        {
            await localNodeBootstrapService.EnsureNodeForSettingsAsync(normalized, cancellationToken).ConfigureAwait(false);

            var httpClient = httpClientFactory.CreateClient(clientName);
            httpClient.BaseAddress = baseAddress;
            httpClient.Timeout = TimeSpan.FromSeconds(normalized.TimeoutSeconds);

            var options = new IpfsNodeClientOptions
            {
                BaseAddress = baseAddress,
                ApiPath = normalized.ApiPath
            };

            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation("node", "create-lease", "success", Stopwatch.GetElapsedTime(start), tags);
            return new IpfsClientLease(httpClient, new IpfsEngineClient(httpClient, options), normalized);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation("node", "create-lease", "failure", Stopwatch.GetElapsedTime(start), tags);
            throw;
        }
    }

    internal static string ResolveHttpClientName(NodeConnectionRequestCategory category)
        => category switch
        {
            NodeConnectionRequestCategory.ReadOnlyUi => NodeControlHttpClientNames.NodeRead,
            NodeConnectionRequestCategory.Gateway => NodeControlHttpClientNames.NodeGateway,
            NodeConnectionRequestCategory.Mutation => NodeControlHttpClientNames.NodeMutation,
            NodeConnectionRequestCategory.Admin => NodeControlHttpClientNames.NodeAdmin,
            NodeConnectionRequestCategory.RemotePin => NodeControlHttpClientNames.NodeRemotePin,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported node connection request category.")
        };

    private static NodeConnectionSettings CreateSettingsWithMinimumTimeoutSeconds(
        NodeConnectionSettings settings,
        int minimumTimeoutSeconds)
    {
        var normalized = NormalizeSettings(settings);
        normalized.TimeoutSeconds = Math.Max(
            normalized.TimeoutSeconds,
            Math.Clamp(minimumTimeoutSeconds, 5, MaximumLeaseTimeoutSeconds));
        return normalized;
    }

    private static NodeConnectionSettings NormalizeSettings(NodeConnectionSettings settings)
    {
        var normalized = settings.Clone().Normalize();
        normalized.TimeoutSeconds = Math.Clamp(normalized.TimeoutSeconds, 5, MaximumLeaseTimeoutSeconds);
        return normalized;
    }
}
