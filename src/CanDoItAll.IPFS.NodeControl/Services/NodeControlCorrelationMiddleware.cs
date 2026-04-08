namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeControlCorrelationMiddleware(
    RequestDelegate next,
    ILogger<NodeControlCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var incomingCorrelationId = context.Request.Headers[NodeControlTelemetry.CorrelationHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incomingCorrelationId))
        {
            context.TraceIdentifier = incomingCorrelationId.Trim();
        }

        var correlationId = NodeControlTelemetry.ResolveCorrelationId(context) ?? context.TraceIdentifier;
        context.Response.Headers[NodeControlTelemetry.CorrelationHeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [NodeControlTelemetry.CorrelationScopeKey] = correlationId
        });

        await next(context).ConfigureAwait(false);
    }
}
