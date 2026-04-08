using System.Globalization;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages.PinRequestsComponents;

public enum PinRequestFilter
{
    All,
    Pending,
    NotAccepted,
    Accepted,
    Rejected,
    Failed
}

public sealed record PinRequestCopyRequest(string Value, string Label);

public sealed record PinRequestFilterSummary(PinRequestFilter Filter, string Label, int Count, bool IsActive);

public static class PinRequestPresentation
{
    public static IReadOnlyList<StoredRemotePinRequest> FilterRequests(
        IReadOnlyList<StoredRemotePinRequest> source,
        PinRequestFilter filter)
        => source
            .Where(request => filter switch
            {
                PinRequestFilter.Pending => request.State == RemotePinRequestState.Pending,
                PinRequestFilter.NotAccepted => request.State != RemotePinRequestState.Accepted,
                PinRequestFilter.Accepted => request.State == RemotePinRequestState.Accepted,
                PinRequestFilter.Rejected => request.State == RemotePinRequestState.Rejected,
                PinRequestFilter.Failed => request.State == RemotePinRequestState.Failed,
                _ => true
            })
            .ToList();

    public static string GetFilterLabel(PinRequestFilter filter)
        => filter switch
        {
            PinRequestFilter.Pending => "Pending",
            PinRequestFilter.NotAccepted => "Not accepted",
            PinRequestFilter.Accepted => "Accepted",
            PinRequestFilter.Rejected => "Rejected",
            PinRequestFilter.Failed => "Failed",
            _ => "All"
        };

    public static string GetHistoryMessage(StoredRemotePinRequest request)
    {
        if (!request.RespondedAtUtc.HasValue)
        {
            return "Waiting for action.";
        }

        return $"Updated {FormatTimestamp(request.RespondedAtUtc.Value)}";
    }

    public static string GetTimelineLabel(StoredRemotePinRequest request)
        => request.RespondedAtUtc.HasValue
            ? $"Updated {FormatTimestamp(request.RespondedAtUtc.Value)}"
            : $"Received {FormatTimestamp(request.ReceivedAtUtc)}";

    public static string GetCardSummary(StoredRemotePinRequest request)
    {
        var content = request.Request.Content;
        if (!content.IsDirectory)
        {
            return "Single file";
        }

        if (content.ChildCount == 0)
        {
            return "Empty folder";
        }

        return $"{content.ChildCount} direct item{(content.ChildCount == 1 ? string.Empty : "s")}";
    }

    public static string GetContentLabel(RemotePinContentSnapshot content)
        => content.IsDirectory ? "Folder" : "File";

    public static string GetStateTone(RemotePinRequestState state)
        => state switch
        {
            RemotePinRequestState.Accepted => "success",
            RemotePinRequestState.Rejected => "warning",
            RemotePinRequestState.Failed => "error",
            _ => "info"
        };

    public static string GetResultHeading(RemotePinRequestState state)
        => state switch
        {
            RemotePinRequestState.Accepted => "Pinned on receiver",
            RemotePinRequestState.Rejected => "Rejected by receiver",
            RemotePinRequestState.Failed => "Pin attempt failed",
            _ => "Pending request"
        };

    public static string FormatTimestamp(DateTimeOffset timestamp)
        => timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public static string FormatCompactId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 18)
        {
            return value;
        }

        return $"{value[..10]}...{value[^6..]}";
    }

    public static string BuildIpfsAddress(string cid)
        => $"/ipfs/{cid}";

    public static string GetSecurityBadgeLabel(StoredRemotePinRequest request)
        => request.SecurityDisposition switch
        {
            RemotePinSecurityDisposition.Verified => "Verified",
            RemotePinSecurityDisposition.Compatibility => "Compatibility",
            RemotePinSecurityDisposition.Rejected => "Rejected",
            _ => GetEnvelopeLabel(request.Request)
        };

    public static string GetSecurityLabel(StoredRemotePinRequest request)
        => request.SecurityDisposition switch
        {
            RemotePinSecurityDisposition.Verified => string.IsNullOrWhiteSpace(request.TrustedSenderLabel)
                ? "Verified sender"
                : $"Verified sender: {request.TrustedSenderLabel}",
            RemotePinSecurityDisposition.Compatibility => "Accepted through compatibility mode",
            RemotePinSecurityDisposition.Rejected => "Rejected during security review",
            _ => request.Request.Version >= RemotePinRequestSecurityService.SignedEnvelopeVersion
                ? "Signed envelope not yet reviewed"
                : "Legacy envelope"
        };

    public static string GetSecurityHeading(StoredRemotePinRequest request)
        => request.SecurityDisposition switch
        {
            RemotePinSecurityDisposition.Verified => "Security verification",
            RemotePinSecurityDisposition.Compatibility => "Compatibility mode",
            RemotePinSecurityDisposition.Rejected => "Security rejection",
            _ => "Envelope details"
        };

    public static string? GetSecurityCalloutMessage(StoredRemotePinRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SecurityMessage)
            && !string.Equals(request.SecurityMessage, request.ResponseMessage, StringComparison.Ordinal))
        {
            return request.SecurityMessage;
        }

        if (request.SecurityDisposition == RemotePinSecurityDisposition.Unknown)
        {
            return request.Request.Version >= RemotePinRequestSecurityService.SignedEnvelopeVersion
                ? "This request carries signed envelope metadata, but it was not processed through the new trust workflow."
                : "This request uses the legacy compatibility envelope with no signature or expiry metadata.";
        }

        return request.SecurityDisposition == RemotePinSecurityDisposition.Compatibility
            ? "Legacy compatibility requests should only be accepted on trusted transitional networks."
            : null;
    }

    public static string GetEnvelopeLabel(RemotePinRequestEnvelope request)
        => request.Version >= RemotePinRequestSecurityService.SignedEnvelopeVersion
            ? $"v{request.Version} signed"
            : $"v{request.Version} legacy";

    public static string GetSecurityTone(RemotePinSecurityDisposition disposition)
        => disposition switch
        {
            RemotePinSecurityDisposition.Verified => "success",
            RemotePinSecurityDisposition.Compatibility => "warning",
            RemotePinSecurityDisposition.Rejected => "error",
            _ => "info"
        };

    public static string GetPrimarySenderAddress(StoredRemotePinRequest request)
    {
        var address = request.Request.Sender.Addresses.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        if (!string.IsNullOrWhiteSpace(address))
        {
            return address.Trim();
        }

        if (Uri.TryCreate(request.Request.Sender.NodeBaseUrl, UriKind.Absolute, out var nodeUri))
        {
            return nodeUri.Authority;
        }

        if (Uri.TryCreate(request.Request.Sender.ControlAppUrl, UriKind.Absolute, out var controlUri))
        {
            return controlUri.Authority;
        }

        return "Unknown";
    }

    public static string GetSenderOrigin(StoredRemotePinRequest request)
    {
        var address = GetPrimarySenderAddress(request);
        if (string.IsNullOrWhiteSpace(address) || string.Equals(address, "Unknown", StringComparison.Ordinal))
        {
            return "Unknown";
        }

        if (!address.StartsWith("/", StringComparison.Ordinal))
        {
            return address;
        }

        var segments = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? host = null;
        string? port = null;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            switch (segments[i])
            {
                case "ip4":
                case "ip6":
                case "dns":
                case "dns4":
                case "dns6":
                    host = segments[i + 1];
                    break;
                case "tcp":
                case "udp":
                    port = segments[i + 1];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return address;
        }

        return string.IsNullOrWhiteSpace(port)
            ? host
            : $"{host}:{port}";
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes:n0} B";
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var scaled = bytes / Math.Pow(1024, unitIndex);
        return string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", scaled, units[unitIndex]);
    }
}
