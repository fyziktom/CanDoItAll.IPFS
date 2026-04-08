using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class OperatingProfileOptionsSetup : IPostConfigureOptions<OperatingProfileOptions>
{
    public void PostConfigure(string? name, OperatingProfileOptions options)
        => ApplyDefaults(options);

    public static void ApplyDefaults(OperatingProfileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var isPro = options.Mode == OperatingProfileMode.Pro;
        options.RequireAdminAuthentication ??= isPro;
        options.EnableRateLimiting ??= isPro;
        options.EnableStrictCertificateValidation ??= isPro;
        options.EnableStructuredTelemetry ??= isPro;
        options.EnablePublishingHardening ??= isPro;
        options.AllowLegacyRemotePinCompatibility ??= !isPro;
        options.PreferLocalNodeBootstrap ??= true;
    }
}
