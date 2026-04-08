namespace CanDoItAll.IPFS.NodeControl.Options;

public enum OperatingProfileMode
{
    Light = 0,
    Pro = 1
}

public sealed class OperatingProfileOptions
{
    public const string SectionName = "OperatingProfile";

    public bool? AllowLegacyRemotePinCompatibility { get; set; }

    public bool? EnablePublishingHardening { get; set; }

    public bool? EnableRateLimiting { get; set; }

    public bool? EnableStructuredTelemetry { get; set; }

    public bool? EnableStrictCertificateValidation { get; set; }

    public OperatingProfileMode Mode { get; set; } = OperatingProfileMode.Light;

    public bool? PreferLocalNodeBootstrap { get; set; }

    public bool? RequireAdminAuthentication { get; set; }
}
