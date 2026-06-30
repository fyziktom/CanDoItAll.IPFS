namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed class KnownRemotePinTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Label { get; set; } = "Remote receiver";

    public string ControlAppUrl { get; set; } = string.Empty;

    public string? LastKnownNodeLabel { get; set; }

    public string? LastKnownPeerId { get; set; }

    public DateTimeOffset? LastProbeSucceededAtUtc { get; set; }

    public string? LastFailureMessage { get; set; }
}
