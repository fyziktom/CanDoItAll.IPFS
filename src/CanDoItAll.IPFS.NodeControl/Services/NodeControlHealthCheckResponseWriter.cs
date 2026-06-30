using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CanDoItAll.IPFS.NodeControl.Services;

public static class NodeControlHealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions HealthResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMilliseconds = report.TotalDuration.TotalMilliseconds,
            correlationId = NodeControlTelemetry.ResolveCorrelationId(context),
            entries = report.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    status = pair.Value.Status.ToString(),
                    description = pair.Value.Description,
                    durationMilliseconds = pair.Value.Duration.TotalMilliseconds,
                    data = pair.Value.Data
                })
        };

        await JsonSerializer.SerializeAsync(
                context.Response.Body,
                payload,
                HealthResponseJsonOptions)
            .ConfigureAwait(false);
    }
}
