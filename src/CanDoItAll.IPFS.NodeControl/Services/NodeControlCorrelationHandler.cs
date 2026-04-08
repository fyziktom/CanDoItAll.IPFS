using System.Diagnostics;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeControlCorrelationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = NodeControlTelemetry.ResolveCorrelationId(httpContextAccessor.HttpContext);
        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(NodeControlTelemetry.CorrelationHeaderName))
        {
            request.Headers.TryAddWithoutValidation(NodeControlTelemetry.CorrelationHeaderName, correlationId);
        }

        Activity.Current?.SetTag(NodeControlTelemetry.CorrelationTagName, correlationId);
        return base.SendAsync(request, cancellationToken);
    }
}
