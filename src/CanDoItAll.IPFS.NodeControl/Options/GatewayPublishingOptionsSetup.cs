using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class GatewayPublishingOptionsSetup(IOptions<OperatingProfileOptions> operatingProfileOptions)
    : IPostConfigureOptions<GatewayPublishingOptions>
{
    public void PostConfigure(string? name, GatewayPublishingOptions options)
        => ApplyDefaults(operatingProfileOptions.Value, options);

    public static void ApplyDefaults(OperatingProfileOptions operatingProfileOptions, GatewayPublishingOptions options)
    {
        ArgumentNullException.ThrowIfNull(operatingProfileOptions);
        ArgumentNullException.ThrowIfNull(options);

        options.Mode ??= operatingProfileOptions.EnablePublishingHardening == true
            ? GatewayPublishingMode.Publish
            : GatewayPublishingMode.Preview;
        options.EnableDirectoryListings ??= options.Mode == GatewayPublishingMode.Preview;
        options.ImmutableFileMaxAgeSeconds = options.ImmutableFileMaxAgeSeconds > 0
            ? options.ImmutableFileMaxAgeSeconds
            : 31536000;
        options.MutableContentMaxAgeSeconds = options.MutableContentMaxAgeSeconds > 0
            ? options.MutableContentMaxAgeSeconds
            : 60;
    }
}
