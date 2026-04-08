using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class CurrentNodeReadinessHealthCheck(
    CurrentNodeTargetRegistry currentNodeTargetRegistry,
    IHttpClientFactory httpClientFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = currentNodeTargetRegistry.Current.Normalize();
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "health" },
            { NodeControlTelemetry.OperationTagName, "current-node-readiness" },
            { "node.label", settings.Label },
            { "node.base_url", settings.BaseUrl },
            { "node.api_path", settings.ApiPath }
        };
        using var activity = NodeControlTelemetry.StartActivity("health.current-node-readiness", ActivityKind.Client, tags);

        try
        {
            using var client = httpClientFactory.CreateClient(Composition.NodeControlHttpClientNames.NodeRead);
            client.BaseAddress = settings.BuildBaseAddress();
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 30));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.ApiPath.Trim('/')}/version");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var elapsed = Stopwatch.GetElapsedTime(start);
            if (!response.IsSuccessStatusCode)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Node probe returned HTTP {(int)response.StatusCode}.");
                NodeControlTelemetry.RecordOperation("health", "current-node-readiness", "unhealthy", elapsed, tags);
                return HealthCheckResult.Unhealthy(
                    description: $"Current node readiness probe returned HTTP {(int)response.StatusCode}.",
                    data: new Dictionary<string, object>
                    {
                        ["baseUrl"] = settings.BaseUrl,
                        ["apiPath"] = settings.ApiPath,
                        ["statusCode"] = (int)response.StatusCode
                    });
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation("health", "current-node-readiness", "healthy", elapsed, tags);
            return HealthCheckResult.Healthy(
                description: "The configured IPFS node responded to the version probe.",
                data: new Dictionary<string, object>
                {
                    ["baseUrl"] = settings.BaseUrl,
                    ["apiPath"] = settings.ApiPath
                });
        }
        catch (Exception ex)
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation("health", "current-node-readiness", "unhealthy", elapsed, tags);
            return HealthCheckResult.Unhealthy(
                description: "The configured IPFS node did not respond to the readiness probe.",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["baseUrl"] = settings.BaseUrl,
                    ["apiPath"] = settings.ApiPath
                });
        }
    }
}
