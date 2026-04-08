using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class IpfsClientFactory(
    NodeSessionState nodeSessionState,
    INodeConnectionLeaseFactory currentNodeLeaseFactory)
{
    private const int MaximumLeaseTimeoutSeconds = 1800;

    public IpfsClientLease CreateLease()
        => currentNodeLeaseFactory.CreateLease(nodeSessionState.CurrentSettings);

    public Task<IpfsClientLease> CreateLeaseAsync(
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default)
        => currentNodeLeaseFactory.CreateLeaseAsync(nodeSessionState.CurrentSettings, category, cancellationToken);

    public Task<IpfsClientLease> CreateLeaseWithMinimumTimeoutSecondsAsync(
        int minimumTimeoutSeconds,
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default)
    {
        var settings = nodeSessionState.CurrentSettings;
        settings.TimeoutSeconds = Math.Max(
            settings.TimeoutSeconds,
            Math.Clamp(minimumTimeoutSeconds, 5, MaximumLeaseTimeoutSeconds));
        return currentNodeLeaseFactory.CreateLeaseAsync(settings, category, cancellationToken);
    }
}
