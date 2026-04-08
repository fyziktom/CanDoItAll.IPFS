using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface INodeConnectionLeaseFactory
{
    NodeConnectionSettings CurrentSettings { get; }

    IpfsClientLease CreateLease();

    IpfsClientLease CreateLease(NodeConnectionSettings settings);

    IpfsClientLease CreateLeaseWithMinimumTimeoutSeconds(int minimumTimeoutSeconds);

    Task<IpfsClientLease> CreateLeaseAsync(
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default);

    Task<IpfsClientLease> CreateLeaseAsync(
        NodeConnectionSettings settings,
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default);

    Task<IpfsClientLease> CreateLeaseWithMinimumTimeoutSecondsAsync(
        int minimumTimeoutSeconds,
        NodeConnectionRequestCategory category,
        CancellationToken cancellationToken = default);
}
