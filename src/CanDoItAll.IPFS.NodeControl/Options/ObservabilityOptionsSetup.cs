using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class ObservabilityOptionsSetup(IOptions<OperatingProfileOptions> operatingProfileOptions)
    : IPostConfigureOptions<ObservabilityOptions>
{
    public void PostConfigure(string? name, ObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ApplyDefaults(operatingProfileOptions.Value, options);
    }

    public static void ApplyDefaults(OperatingProfileOptions operatingProfile, ObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(operatingProfile);
        ArgumentNullException.ThrowIfNull(options);

        options.ServiceName = string.IsNullOrWhiteSpace(options.ServiceName)
            ? "CanDoItAll.IPFS.NodeControl"
            : options.ServiceName.Trim();
        options.EnableAspNetCoreInstrumentation ??= operatingProfile.EnableStructuredTelemetry ?? false;
        options.EnableHttpClientInstrumentation ??= operatingProfile.EnableStructuredTelemetry ?? false;
        options.EnableConsoleExporter ??= false;
        options.EnableOtlpExporter ??= false;
    }
}
