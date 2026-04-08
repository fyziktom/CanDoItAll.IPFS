namespace CanDoItAll.IPFS.NodeControl.Options;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool? EnableAspNetCoreInstrumentation { get; set; }

    public bool? EnableConsoleExporter { get; set; }

    public bool? EnableHttpClientInstrumentation { get; set; }

    public bool? EnableOtlpExporter { get; set; }

    public string ServiceName { get; set; } = "IpfsNodeControl";
}
