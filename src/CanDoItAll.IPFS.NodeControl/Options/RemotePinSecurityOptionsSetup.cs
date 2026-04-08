using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class RemotePinSecurityOptionsSetup(IOptions<OperatingProfileOptions> operatingProfileOptions)
    : IPostConfigureOptions<RemotePinSecurityOptions>
{
    public void PostConfigure(string? name, RemotePinSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var operatingProfile = operatingProfileOptions.Value;
        options.CompatibilityModeEnabled ??= operatingProfile.AllowLegacyRemotePinCompatibility ?? operatingProfile.Mode == OperatingProfileMode.Light;
        options.AllowedClockSkewSeconds = options.AllowedClockSkewSeconds >= 0 ? options.AllowedClockSkewSeconds : 60;
        options.RequestExpiryMinutes = options.RequestExpiryMinutes > 0 ? options.RequestExpiryMinutes : 15;
        options.LocalSharedSecret = NormalizeSecret(options.LocalSharedSecret);
        options.LocalKeyId = NormalizeToken(options.LocalKeyId)
            ?? (!string.IsNullOrWhiteSpace(options.LocalSharedSecret) ? RemotePinRequestSecurityService.DefaultKeyId : null);
        options.TrustedSenders = (options.TrustedSenders ?? [])
            .Select(NormalizeSender)
            .Where(static sender => !string.IsNullOrWhiteSpace(sender.SenderId) && !string.IsNullOrWhiteSpace(sender.SharedSecret))
            .ToList();
    }

    private static RemotePinTrustedSenderOptions NormalizeSender(RemotePinTrustedSenderOptions? sender)
    {
        var normalized = sender ?? new RemotePinTrustedSenderOptions();
        normalized.SenderId = NormalizeToken(normalized.SenderId);
        normalized.Label = NormalizeToken(normalized.Label);
        normalized.SharedSecret = NormalizeSecret(normalized.SharedSecret);
        normalized.KeyId = NormalizeToken(normalized.KeyId)
            ?? (!string.IsNullOrWhiteSpace(normalized.SharedSecret) ? RemotePinRequestSecurityService.DefaultKeyId : null);
        return normalized;
    }

    private static string? NormalizeSecret(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeToken(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
