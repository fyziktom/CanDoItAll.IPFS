using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface INodeConnectionDriver
{
    NodeConnectionSettings CurrentSettings { get; }

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
