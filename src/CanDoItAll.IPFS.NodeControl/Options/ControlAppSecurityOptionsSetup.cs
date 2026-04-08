using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class ControlAppSecurityOptionsSetup(IOptions<OperatingProfileOptions> operatingProfileOptions)
    : IPostConfigureOptions<ControlAppSecurityOptions>
{
    public void PostConfigure(string? name, ControlAppSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var operatingProfile = operatingProfileOptions.Value;
        options.AdminAccessKey = NormalizeSecret(options.AdminAccessKey);
        options.RemotePinAccessKey = NormalizeSecret(options.RemotePinAccessKey);
        options.AllowAnonymousLocalAdmin ??= !(operatingProfile.RequireAdminAuthentication ?? false);
        options.AllowAnonymousLocalRemotePin ??= operatingProfile.AllowLegacyRemotePinCompatibility ?? true;
        options.AdminPermitLimit = options.AdminPermitLimit > 0 ? options.AdminPermitLimit : 60;
        options.RemotePinPermitLimit = options.RemotePinPermitLimit > 0 ? options.RemotePinPermitLimit : 12;
        options.RateLimitWindowSeconds = options.RateLimitWindowSeconds > 0 ? options.RateLimitWindowSeconds : 60;
    }

    private static string? NormalizeSecret(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
