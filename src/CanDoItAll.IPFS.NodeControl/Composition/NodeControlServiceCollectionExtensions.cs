using CanDoItAll.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.DesktopHost;
using Ipfs.Server;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Security;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Linq;
using System.Threading.RateLimiting;

namespace CanDoItAll.IPFS.NodeControl.Composition;

public static class NodeControlServiceCollectionExtensions
{
    private static readonly TimeSpan NodeClientAttemptTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NodeClientTotalRequestTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan NodeClientCircuitBreakerSamplingDuration = TimeSpan.FromMinutes(20);

    private static readonly Lazy<IServiceProvider> CompatibilityHttpClientProvider = new(() =>
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNodeControlHttpClients();
        return services.BuildServiceProvider();
    });

    public static IServiceCollection AddIpfsNodeControlApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddCanDoItAllBaseLib();

        services.AddOptions<OperatingProfileOptions>()
            .Bind(configuration.GetSection(OperatingProfileOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<OperatingProfileOptions>, OperatingProfileOptionsSetup>();
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<ObservabilityOptions>, ObservabilityOptionsSetup>();
        services.AddOptions<GatewayPublishingOptions>()
            .Bind(configuration.GetSection(GatewayPublishingOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<GatewayPublishingOptions>, GatewayPublishingOptionsSetup>();
        services.AddOptions<ControlAppSecurityOptions>()
            .Bind(configuration.GetSection(ControlAppSecurityOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<ControlAppSecurityOptions>, ControlAppSecurityOptionsSetup>();
        services.AddOptions<RemotePinSecurityOptions>()
            .Bind(configuration.GetSection(RemotePinSecurityOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<RemotePinSecurityOptions>, RemotePinSecurityOptionsSetup>();
        services.AddOptions<HttpApiHostOptions>()
            .Bind(configuration.GetSection(HttpApiHostOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<HttpApiHostOptions>, HttpApiHostOptionsSetup>();

        var operatingProfile = configuration.GetSection(OperatingProfileOptions.SectionName).Get<OperatingProfileOptions>()
            ?? new OperatingProfileOptions();
        OperatingProfileOptionsSetup.ApplyDefaults(operatingProfile);

        var observabilityOptions = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();
        ObservabilityOptionsSetup.ApplyDefaults(operatingProfile, observabilityOptions);

        services.AddNodeControlHttpClients();
        services.AddControlAppSecurity(configuration);
        services.AddNodeControlObservability(configuration, observabilityOptions);

        services.Configure<NodeConnectionSettings>(configuration.GetSection("NodeSettingsDefaults"));
        services.Configure<ServerNodeSettingsStoreOptions>(configuration.GetSection("ServerNodeSettingsStore"));
        services.Configure<RemotePinRequestStoreOptions>(configuration.GetSection("RemotePinRequestStore"));
        services.Configure<ApplicationLogStoreOptions>(configuration.GetSection("ApplicationLogStore"));
        services.Configure<ExplorerIndexStoreOptions>(configuration.GetSection("ExplorerIndexStore"));

        services.AddSingleton<ServerNodeSettingsStore>();
        services.AddSingleton<IServerNodeSettingsStore>(serviceProvider => serviceProvider.GetRequiredService<ServerNodeSettingsStore>());
        services.AddSingleton<RemotePinRequestStore>();
        services.AddSingleton<IRemotePinRequestStore>(serviceProvider => serviceProvider.GetRequiredService<RemotePinRequestStore>());
        services.AddSingleton<ApplicationLogStore>();
        services.AddSingleton<IApplicationLogStore>(serviceProvider => serviceProvider.GetRequiredService<ApplicationLogStore>());
        services.AddSingleton<ExplorerIndexStore>();
        services.AddSingleton<IExplorerIndexStore>(serviceProvider => serviceProvider.GetRequiredService<ExplorerIndexStore>());
        services.AddSingleton<CurrentNodeTargetRegistry>();
        services.AddSingleton<HostedUrlRegistry>();
        services.AddSingleton<INodeHostController, DesktopNodeHostController>();
        services.AddSingleton<LocalNodeBootstrapService>();
        services.AddSingleton<CurrentNodeLeaseFactory>();
        services.AddSingleton<INodeConnectionLeaseFactory>(serviceProvider => serviceProvider.GetRequiredService<CurrentNodeLeaseFactory>());
        services.AddSingleton<INodeConnectionDriver>(serviceProvider => serviceProvider.GetRequiredService<CurrentNodeLeaseFactory>());
        services.AddSingleton<ConfiguredNodeStatusService>();
        services.AddTransient<CurrentNodeReadinessHealthCheck>();
        services.AddTransient<PersistenceReadinessHealthCheck>();
        services.AddSingleton<RemotePinRequestSecurityService>();
        services.AddSingleton<RemotePinRequestWorkflowService>();
        services.AddSingleton<SelfHostControlService>();
        services.AddSingleton<ILoggerProvider, ApplicationLogLoggerProvider>();
#if WINDOWS
        services.AddSingleton<WindowsStartupRegistrationService>();
        services.AddHostedService<ControlAppTrayHostedService>();
#endif
        services.AddScoped<NodeSessionState>();
        services.AddScoped<IpfsClientFactory>();
        services.AddScoped<NodeDashboardService>();
        services.AddScoped<NodeFileWorkflowService>();
        services.AddScoped<INodeFileWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeFileWorkflowService>());
        services.AddScoped<NodeExplorerWorkflowService>();
        services.AddScoped<INodeExplorerWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeExplorerWorkflowService>());
        services.AddScoped<NodeContentWorkflowService>();
        services.AddScoped<INodeContentWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeContentWorkflowService>());
        services.AddScoped<NodeNetworkWorkflowService>();
        services.AddScoped<INodeNetworkWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeNetworkWorkflowService>());
        services.AddScoped<NodeMaintenanceWorkflowService>();
        services.AddScoped<INodeMaintenanceWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeMaintenanceWorkflowService>());
        services.AddScoped<NodeSettingsBrowserStorage>();
        services.AddScoped<KnownRemotePinTargetBrowserStorage>();
        services.AddScoped<NodeCanvasSurfaceFactory>();
        services.AddScoped<NodeOperatorService>();
        services.AddScoped<INodeOperator>(serviceProvider => serviceProvider.GetRequiredService<NodeOperatorService>());
        services.AddScoped<RemotePinShareService>();
        services.AddSingleton<NodeGatewayService>();

        return services;
    }

    public static IServiceCollection AddNodeControlHttpClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddTransient<NodeControlCorrelationHandler>();
        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.NodeRead));
        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.NodeGateway));
        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.NodeMutation), disableRetriesForUnsafeMethods: true);
        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.NodeAdmin), disableRetriesForUnsafeMethods: true);
        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.NodeRemotePin), disableRetriesForUnsafeMethods: true);

        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.RemotePinProbe));
        ConfigureStandardNodeHttpClient(
            services.AddHttpClient(NodeControlHttpClientNames.RemotePinProbeInsecure)
                .ConfigurePrimaryHttpMessageHandler(CreatePrivateNetworkBypassHandler));
        ConfigureStandardNodeHttpClient(services.AddHttpClient(NodeControlHttpClientNames.RemotePinSend), disableRetriesForUnsafeMethods: true);
        ConfigureStandardNodeHttpClient(
            services.AddHttpClient(NodeControlHttpClientNames.RemotePinSendInsecure)
                .ConfigurePrimaryHttpMessageHandler(CreatePrivateNetworkBypassHandler),
            disableRetriesForUnsafeMethods: true);

        return services;
    }

    public static IHttpClientFactory CreateCompatibilityHttpClientFactory()
        => CompatibilityHttpClientProvider.Value.GetRequiredService<IHttpClientFactory>();

    private static IServiceCollection AddControlAppSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddAuthentication(ControlAppAuthenticationSchemes.ApiKey)
            .AddScheme<AuthenticationSchemeOptions, ControlAppApiKeyAuthenticationHandler>(
                ControlAppAuthenticationSchemes.ApiKey,
                static _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(ControlAppAuthorizationPolicyNames.AdminApi, policy =>
            {
                policy.AuthenticationSchemes.Add(ControlAppAuthenticationSchemes.ApiKey);
                policy.Requirements.Add(new ControlAppEndpointAccessRequirement(ControlAppSecurityClaims.Admin));
            })
            .AddPolicy(ControlAppAuthorizationPolicyNames.RemotePinIngress, policy =>
            {
                policy.AuthenticationSchemes.Add(ControlAppAuthenticationSchemes.ApiKey);
                policy.Requirements.Add(new ControlAppEndpointAccessRequirement(ControlAppSecurityClaims.RemotePin));
            });

        services.AddSingleton<IAuthorizationHandler, ControlAppEndpointAccessHandler>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ControlAppRateLimitPolicyNames.AdminApi, static httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<ControlAppSecurityOptions>>().Value;
                return CreateFixedWindowPartition(
                    httpContext,
                    partitionPrefix: "admin",
                    settings.AdminPermitLimit,
                    settings.RateLimitWindowSeconds);
            });
            options.AddPolicy(ControlAppRateLimitPolicyNames.RemotePinIngress, static httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<ControlAppSecurityOptions>>().Value;
                return CreateFixedWindowPartition(
                    httpContext,
                    partitionPrefix: "remote-pin",
                    settings.RemotePinPermitLimit,
                    settings.RateLimitWindowSeconds);
            });
        });

        return services;
    }

    private static IServiceCollection AddNodeControlObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        ObservabilityOptions observabilityOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(observabilityOptions);

        var enableOtlpExporter = observabilityOptions.EnableOtlpExporter == true
            || !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        services.AddHealthChecks()
            .AddCheck<CurrentNodeReadinessHealthCheck>(
                "current-node",
                tags: ["ready"])
            .AddCheck<PersistenceReadinessHealthCheck>(
                "persistence",
                tags: ["ready"]);

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(observabilityOptions.ServiceName));

        openTelemetry.WithMetrics(metrics =>
        {
            metrics.AddMeter(NodeControlTelemetry.MeterName);
            metrics.AddMeter("Microsoft.AspNetCore.Hosting");
            metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
            metrics.AddMeter("System.Net.Http");

            if (observabilityOptions.EnableAspNetCoreInstrumentation == true)
            {
                metrics.AddAspNetCoreInstrumentation();
            }

            if (observabilityOptions.EnableHttpClientInstrumentation == true)
            {
                metrics.AddHttpClientInstrumentation();
            }

            if (observabilityOptions.EnableConsoleExporter == true)
            {
                metrics.AddConsoleExporter();
            }

            if (enableOtlpExporter)
            {
                metrics.AddOtlpExporter();
            }
        });

        openTelemetry.WithTracing(tracing =>
        {
            tracing.AddSource(NodeControlTelemetry.ActivitySourceName);

            if (observabilityOptions.EnableAspNetCoreInstrumentation == true)
            {
                tracing.AddAspNetCoreInstrumentation();
            }

            if (observabilityOptions.EnableHttpClientInstrumentation == true)
            {
                tracing.AddHttpClientInstrumentation();
            }

            if (observabilityOptions.EnableConsoleExporter == true)
            {
                tracing.AddConsoleExporter();
            }

            if (enableOtlpExporter)
            {
                tracing.AddOtlpExporter();
            }
        });

        return services;
    }

    private static HttpClientHandler CreatePrivateNetworkBypassHandler()
        => new()
        {
            ServerCertificateCustomValidationCallback = static (_, _, _, _) => true
        };

    private static IHttpClientBuilder ConfigureStandardNodeHttpClient(
        IHttpClientBuilder builder,
        bool disableRetriesForUnsafeMethods = false)
    {
        builder.AddHttpMessageHandler<NodeControlCorrelationHandler>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = NodeClientAttemptTimeout;
                options.TotalRequestTimeout.Timeout = NodeClientTotalRequestTimeout;
                options.CircuitBreaker.SamplingDuration = NodeClientCircuitBreakerSamplingDuration;
                if (disableRetriesForUnsafeMethods)
                {
                    options.Retry.DisableForUnsafeHttpMethods();
                }
            });

        return builder;
    }

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        HttpContext httpContext,
        string partitionPrefix,
        int permitLimit,
        int windowSeconds)
    {
        var partitionKey = $"{partitionPrefix}:{ResolveRateLimitPartitionKey(httpContext)}";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = Math.Max(1, permitLimit),
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds))
            });
    }

    private static string ResolveRateLimitPartitionKey(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var permissions = httpContext.User.FindAll(ControlAppSecurityClaims.Permission)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
            return $"auth:{string.Join(",", permissions)}";
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "local";
    }
}
