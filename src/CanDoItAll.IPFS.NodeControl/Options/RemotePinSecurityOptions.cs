namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class RemotePinSecurityOptions
{
    public const string SectionName = "RemotePinSecurity";

    public int AllowedClockSkewSeconds { get; set; }

    public bool? CompatibilityModeEnabled { get; set; }

    public string? LocalKeyId { get; set; }

    public string? LocalSharedSecret { get; set; }

    public int RequestExpiryMinutes { get; set; }

    public List<RemotePinTrustedSenderOptions> TrustedSenders { get; set; } = [];
}

public sealed class RemotePinTrustedSenderOptions
{
    public string? SenderId { get; set; }

    public string? Label { get; set; }

    public string? KeyId { get; set; }

    public string? SharedSecret { get; set; }
}
