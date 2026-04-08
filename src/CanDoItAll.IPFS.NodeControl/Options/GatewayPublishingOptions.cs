namespace CanDoItAll.IPFS.NodeControl.Options;

public enum GatewayPublishingMode
{
    Preview = 0,
    Publish = 1
}

public sealed class GatewayPublishingOptions
{
    public const string SectionName = "GatewayPublishing";

    public bool? EnableDirectoryListings { get; set; }

    public int ImmutableFileMaxAgeSeconds { get; set; } = 31536000;

    public GatewayPublishingMode? Mode { get; set; }

    public int MutableContentMaxAgeSeconds { get; set; } = 60;
}
