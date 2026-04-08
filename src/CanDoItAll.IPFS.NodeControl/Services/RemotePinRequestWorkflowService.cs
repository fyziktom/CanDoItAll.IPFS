using Ipfs;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using System.Diagnostics;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class RemotePinRequestWorkflowService(
    IRemotePinRequestStore remotePinRequestStore,
    INodeConnectionLeaseFactory currentNodeLeaseFactory,
    IExplorerIndexStore explorerIndexStore,
    RemotePinRequestSecurityService remotePinRequestSecurityService,
    ILogger<RemotePinRequestWorkflowService> logger)
{
    private const int MinimumAcceptTimeoutSeconds = 300;
    private const int MinimumRecursiveAcceptTimeoutSeconds = 900;
    private const long LargeContentThresholdBytes = 256L * 1024L * 1024L;
    private const int PinVerificationLeaseTimeoutSeconds = 60;
    private static readonly TimeSpan PostFailureVerificationWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PinVerificationPollInterval = TimeSpan.FromSeconds(2);

    public IReadOnlyList<StoredRemotePinRequest> List()
        => remotePinRequestStore.List();

    public StoredRemotePinRequest Enqueue(RemotePinRequestEnvelope request)
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "remote-pin" },
            { NodeControlTelemetry.OperationTagName, "enqueue" },
            { "remote_pin.request_id", request.RequestId },
            { "remote_pin.cid", request.Content.Cid }
        };
        using var activity = NodeControlTelemetry.StartActivity("remote-pin.enqueue", ActivityKind.Internal, tags);
        var validation = remotePinRequestSecurityService.ValidateIncomingEnvelope(request, remotePinRequestStore.List());
        if (!validation.IsAccepted)
        {
            activity?.SetStatus(ActivityStatusCode.Error, validation.Message);
            logger.LogWarning(
                "Rejected remote pin request {RequestId}: {Reason}",
                request.RequestId,
                validation.Message);

            if (validation.ShouldPersistRejectedRequest)
            {
                var rejected = remotePinRequestStore.Add(request);
                remotePinRequestStore.Update(rejected.Request.RequestId, item =>
                {
                    item.State = RemotePinRequestState.Rejected;
                    item.SecurityDisposition = validation.Disposition;
                    item.SecurityMessage = validation.Message;
                    item.RespondedAtUtc = DateTimeOffset.UtcNow;
                    item.ResponseMessage = validation.Message;
                });
            }

            NodeControlTelemetry.RecordOperation("remote-pin", "enqueue", "rejected", Stopwatch.GetElapsedTime(start), tags);
            throw new ArgumentException(validation.Message, nameof(request));
        }

        var stored = remotePinRequestStore.Add(request);
        activity?.SetStatus(ActivityStatusCode.Ok);
        NodeControlTelemetry.RecordOperation("remote-pin", "enqueue", "accepted", Stopwatch.GetElapsedTime(start), tags);
        return remotePinRequestStore.Update(stored.Request.RequestId, item =>
        {
            item.SecurityDisposition = validation.Disposition;
            item.SecurityMessage = validation.Message;
            item.TrustedSenderLabel = validation.TrustedSenderLabel;
        });
    }

    public async Task<StoredRemotePinRequest> AcceptAsync(string requestId, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "remote-pin" },
            { NodeControlTelemetry.OperationTagName, "accept" },
            { "remote_pin.request_id", requestId }
        };
        using var activity = NodeControlTelemetry.StartActivity("remote-pin.accept", ActivityKind.Internal, tags);
        var stored = remotePinRequestStore.Get(requestId)
            ?? throw new KeyNotFoundException($"Remote pin request '{requestId}' was not found.");
        var pinTimeoutSeconds = ResolvePinTimeoutSeconds(stored.Request.Content, currentNodeLeaseFactory.CurrentSettings.TimeoutSeconds);
        Cid? requestedCid = null;
        var wasAlreadyPinned = false;

        try
        {
            requestedCid = Cid.Decode(stored.Request.Content.Cid);
            using var lease = await currentNodeLeaseFactory.CreateLeaseWithMinimumTimeoutSecondsAsync(
                pinTimeoutSeconds,
                NodeConnectionRequestCategory.RemotePin,
                cancellationToken).ConfigureAwait(false);
            wasAlreadyPinned = await IsPinnedAsync(lease, requestedCid, cancellationToken).ConfigureAwait(false);

            if (!wasAlreadyPinned)
            {
                await ConnectToSenderAsync(lease, stored.Request.Sender, cancellationToken).ConfigureAwait(false);
                await EnsureRootBlockLocalAsync(lease, requestedCid, cancellationToken).ConfigureAwait(false);
                var pinnedCids = (await lease.Client.Pin.AddAsync(stored.Request.Content.Cid, recursive: true, cancellationToken).ConfigureAwait(false))
                    .ToArray();
                var pinConfirmed = pinnedCids.Any(pin => pin == requestedCid)
                    || await TryConfirmPinCompletedAsync(requestedCid, PostFailureVerificationWindow, cancellationToken).ConfigureAwait(false);

                if (!pinConfirmed)
                {
                    throw new InvalidOperationException($"Receiver node fetched '{stored.Request.Content.Cid}' but did not persist the pin.");
                }
            }

            RememberAcceptedContent(stored.Request.Content);
            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation("remote-pin", "accept", "accepted", Stopwatch.GetElapsedTime(start), tags);
            return MarkAccepted(
                requestId,
                wasAlreadyPinned
                    ? "Already pinned on receiver node."
                    : $"Pinned {stored.Request.Content.Cid} on receiver node.");
        }
        catch (Exception ex)
        {
            if (requestedCid is not null
                && !cancellationToken.IsCancellationRequested
                && await TryConfirmPinCompletedAsync(requestedCid, PostFailureVerificationWindow, cancellationToken).ConfigureAwait(false))
            {
                logger.LogWarning(
                    ex,
                    "Accept flow for remote pin request {RequestId} reported an error, but the receiver confirmed CID {Cid} as pinned during recovery.",
                    requestId,
                    stored.Request.Content.Cid);
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("remote-pin", "accept", "accepted-after-recovery", Stopwatch.GetElapsedTime(start), tags);
                RememberAcceptedContent(stored.Request.Content);
                return MarkAccepted(
                    requestId,
                    wasAlreadyPinned
                        ? "Already pinned on receiver node."
                        : $"Pinned {stored.Request.Content.Cid} on receiver node after verifying the final state.");
            }

            logger.LogError(
                ex,
                "Accept flow failed for remote pin request {RequestId} and CID {Cid}.",
                requestId,
                stored.Request.Content.Cid);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation("remote-pin", "accept", "failed", Stopwatch.GetElapsedTime(start), tags);
            return MarkFailed(
                requestId,
                ex is OperationCanceledException && !cancellationToken.IsCancellationRequested
                    ? BuildTimeoutFailureMessage(stored.Request.Content, pinTimeoutSeconds)
                    : ex.Message);
        }
    }

    public StoredRemotePinRequest Reject(string requestId, string? responseMessage = null)
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "remote-pin" },
            { NodeControlTelemetry.OperationTagName, "reject" },
            { "remote_pin.request_id", requestId }
        };
        using var activity = NodeControlTelemetry.StartActivity("remote-pin.reject", ActivityKind.Internal, tags);
        try
        {
            var rejected = remotePinRequestStore.Update(requestId, item =>
            {
                item.State = RemotePinRequestState.Rejected;
                item.RespondedAtUtc = DateTimeOffset.UtcNow;
                item.ResponseMessage = string.IsNullOrWhiteSpace(responseMessage)
                    ? "Rejected by the receiver user."
                    : responseMessage.Trim();
            });
            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation("remote-pin", "reject", "success", Stopwatch.GetElapsedTime(start), tags);
            return rejected;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation("remote-pin", "reject", "failure", Stopwatch.GetElapsedTime(start), tags);
            throw;
        }
    }

    internal static int ResolvePinTimeoutSeconds(RemotePinContentSnapshot content, int configuredTimeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(content);

        var minimumTimeoutSeconds = content.IsDirectory || content.Size >= LargeContentThresholdBytes
            ? MinimumRecursiveAcceptTimeoutSeconds
            : MinimumAcceptTimeoutSeconds;

        return Math.Max(Math.Clamp(configuredTimeoutSeconds, 5, 600), minimumTimeoutSeconds);
    }

    private static async Task ConnectToSenderAsync(
        IpfsClientLease lease,
        RemotePinSenderSnapshot sender,
        CancellationToken cancellationToken)
    {
        foreach (var address in BuildDialAddresses(sender))
        {
            try
            {
                await lease.Client.Swarm.ConnectAsync((MultiAddress)address, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch
            {
                // Try the next address. Pinning may still succeed later if a route already exists.
            }
        }
    }

    private static IReadOnlyList<string> BuildDialAddresses(RemotePinSenderSnapshot sender)
        => sender.Addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address =>
            {
                var trimmed = address.Trim();
                return trimmed.Contains("/p2p/", StringComparison.OrdinalIgnoreCase)
                       || trimmed.Contains("/ipfs/", StringComparison.OrdinalIgnoreCase)
                    ? trimmed
                    : $"{trimmed.TrimEnd('/')}/p2p/{sender.PeerId}";
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static async Task<bool> IsPinnedAsync(
        IpfsClientLease lease,
        Cid cid,
        CancellationToken cancellationToken)
        => (await lease.Client.Pin.ListAsync(cancellationToken).ConfigureAwait(false))
            .Any(pin => pin == cid);

    private static async Task EnsureRootBlockLocalAsync(
        IpfsClientLease lease,
        Cid cid,
        CancellationToken cancellationToken)
    {
        if (await HasLocalBlockAsync(lease, cid, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await lease.Client.Block.GetAsync(cid, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> HasLocalBlockAsync(
        IpfsClientLease lease,
        Cid cid,
        CancellationToken cancellationToken)
    {
        try
        {
            return await lease.Client.Block.StatAsync(cid, cancellationToken).ConfigureAwait(false) is not null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryConfirmPinCompletedAsync(
        Cid cid,
        TimeSpan verificationWindow,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + verificationWindow;
        while (DateTimeOffset.UtcNow <= deadline)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                using var verificationLease = await currentNodeLeaseFactory.CreateLeaseWithMinimumTimeoutSecondsAsync(
                    PinVerificationLeaseTimeoutSeconds,
                    NodeConnectionRequestCategory.RemotePin,
                    cancellationToken).ConfigureAwait(false);
                if (await IsPinnedAsync(verificationLease, cid, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                // Verification is best-effort after an accept-path failure.
            }

            if (DateTimeOffset.UtcNow + PinVerificationPollInterval > deadline)
            {
                break;
            }

            try
            {
                await Task.Delay(PinVerificationPollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    private StoredRemotePinRequest MarkAccepted(string requestId, string message)
        => remotePinRequestStore.Update(requestId, item =>
        {
            item.State = RemotePinRequestState.Accepted;
            item.RespondedAtUtc = DateTimeOffset.UtcNow;
            item.ResponseMessage = message;
        });

    private StoredRemotePinRequest MarkFailed(string requestId, string message)
        => remotePinRequestStore.Update(requestId, item =>
        {
            item.State = RemotePinRequestState.Failed;
            item.RespondedAtUtc = DateTimeOffset.UtcNow;
            item.ResponseMessage = message;
        });

    private void RememberAcceptedContent(RemotePinContentSnapshot content)
    {
        var now = DateTimeOffset.UtcNow;
        explorerIndexStore.UpsertRoot(new ExplorerIndexedRootRecord(
            content.Cid,
            string.IsNullOrWhiteSpace(content.DisplayName) ? content.Cid : content.DisplayName.Trim(),
            content.IsDirectory,
            content.Size,
            content.ChildCount,
            now,
            now,
            now,
            true));
    }

    private static string BuildTimeoutFailureMessage(RemotePinContentSnapshot content, int timeoutSeconds)
    {
        var timeoutDescription = timeoutSeconds >= 60
            ? $"{timeoutSeconds / 60} minute{(timeoutSeconds / 60 == 1 ? string.Empty : "s")}"
            : $"{timeoutSeconds} second{(timeoutSeconds == 1 ? string.Empty : "s")}";
        var contentDescription = content.IsDirectory ? "folder root" : "file";
        return $"Pinning the {contentDescription} '{content.DisplayName}' did not finish within {timeoutDescription}. Keep both nodes online and retry if the receiver still needs to fetch a larger DAG.";
    }
}
