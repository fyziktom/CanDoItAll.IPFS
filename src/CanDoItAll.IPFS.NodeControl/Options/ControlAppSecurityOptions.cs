namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class ControlAppSecurityOptions
{
    public const string SectionName = "ControlAppSecurity";

    public string? AdminAccessKey { get; set; }

    public string? RemotePinAccessKey { get; set; }

    public bool? AllowAnonymousLocalAdmin { get; set; }

    public bool? AllowAnonymousLocalRemotePin { get; set; }

    public int AdminPermitLimit { get; set; }

    public int RemotePinPermitLimit { get; set; }

    public int RateLimitWindowSeconds { get; set; }
}
