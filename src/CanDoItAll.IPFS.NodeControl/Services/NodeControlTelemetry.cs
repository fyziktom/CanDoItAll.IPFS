using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CanDoItAll.IPFS.NodeControl.Services;

public static class NodeControlTelemetry
{
    public const string ActivitySourceName = "IpfsNodeControl";
    public const string MeterName = "IpfsNodeControl";
    public const string CorrelationHeaderName = "X-Correlation-ID";
    public const string CorrelationScopeKey = "CorrelationId";
    public const string CorrelationTagName = "nodecontrol.correlation_id";
    public const string AreaTagName = "nodecontrol.area";
    public const string OperationTagName = "nodecontrol.operation";
    public const string OutcomeTagName = "nodecontrol.outcome";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(
        "ipfs.nodecontrol.operation.count",
        unit: "{operation}",
        description: "Counts instrumented IPFS Node Control operations.");

    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "ipfs.nodecontrol.operation.duration",
        unit: "ms",
        description: "Records the duration of instrumented IPFS Node Control operations.");

    public static Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        in TagList tags = default)
        => ActivitySource.StartActivity(name, kind, default(ActivityContext), tags);

    public static void RecordOperation(
        string area,
        string operation,
        string outcome,
        TimeSpan elapsed,
        in TagList tags = default)
    {
        var metricTags = tags;
        metricTags.Add(AreaTagName, area);
        metricTags.Add(OperationTagName, operation);
        metricTags.Add(OutcomeTagName, outcome);
        OperationCounter.Add(1, metricTags);
        OperationDuration.Record(elapsed.TotalMilliseconds, metricTags);
    }

    public static string? ResolveCorrelationId(HttpContext? httpContext)
    {
        var candidate = httpContext?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        var currentActivity = Activity.Current;
        if (currentActivity is null)
        {
            return null;
        }

        return currentActivity.TraceId != default
            ? currentActivity.TraceId.ToString()
            : currentActivity.Id;
    }
}
