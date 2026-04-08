namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed record RemotePinContentSnapshot(
    string RequestedPath,
    string Cid,
    string DisplayName,
    bool IsDirectory,
    long Size,
    int ChildCount);

public sealed record RemotePinSenderSnapshot(
    string Label,
    string ControlAppUrl,
    string NodeBaseUrl,
    string PeerId,
    IReadOnlyList<string> Addresses);

public sealed class RemotePinRequestEnvelope
{
    public required string RequestId { get; init; }

    public required DateTimeOffset RequestedAtUtc { get; init; }

    public int Version { get; init; } = 1;

    public string? SenderId { get; init; }

    public string? KeyId { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public string? Nonce { get; init; }

    public string? SignatureAlgorithm { get; init; }

    public string? Signature { get; init; }

    public string Note { get; init; } = string.Empty;

    public required RemotePinSenderSnapshot Sender { get; init; }

    public required RemotePinContentSnapshot Content { get; init; }

    public RemotePinRequestEnvelope WithSecurity(
        int version,
        string? senderId,
        string? keyId,
        DateTimeOffset? expiresAtUtc,
        string? nonce,
        string? signatureAlgorithm,
        string? signature)
        => new()
        {
            RequestId = RequestId,
            RequestedAtUtc = RequestedAtUtc,
            Version = version,
            SenderId = senderId,
            KeyId = keyId,
            ExpiresAtUtc = expiresAtUtc,
            Nonce = nonce,
            SignatureAlgorithm = signatureAlgorithm,
            Signature = signature,
            Note = Note,
            Sender = Sender,
            Content = Content
        };
}

public enum RemotePinSecurityDisposition
{
    Unknown = 0,
    Compatibility = 1,
    Verified = 2,
    Rejected = 3
}

public enum RemotePinRequestState
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Failed = 3
}

public sealed class StoredRemotePinRequest
{
    public required RemotePinRequestEnvelope Request { get; init; }

    public required DateTimeOffset ReceivedAtUtc { get; init; }

    public RemotePinRequestState State { get; set; } = RemotePinRequestState.Pending;

    public RemotePinSecurityDisposition SecurityDisposition { get; set; } = RemotePinSecurityDisposition.Unknown;

    public string? SecurityMessage { get; set; }

    public string? TrustedSenderLabel { get; set; }

    public DateTimeOffset? RespondedAtUtc { get; set; }

    public string? ResponseMessage { get; set; }
}

public sealed class RemotePinReceiverProbeSnapshot
{
    public required string ControlAppUrl { get; init; }

    public required string NodeLabel { get; init; }

    public required string NodeBaseUrl { get; init; }

    public required string ApiPath { get; init; }

    public required bool NodeHealthy { get; init; }

    public required string PeerId { get; init; }

    public required string AgentVersion { get; init; }

    public IReadOnlyList<string> Addresses { get; init; } = [];

    public string? DiagnosticMessage { get; init; }
}
