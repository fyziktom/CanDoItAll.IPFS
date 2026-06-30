namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed record ApplicationLogEntry(
    DateTimeOffset TimestampUtc,
    string Level,
    string Category,
    string Message,
    int EventId,
    string? Exception,
    string? CorrelationId = null,
    string? TraceId = null,
    string? SpanId = null,
    Dictionary<string, string>? Dimensions = null);

public sealed record ApplicationLogWindowPreset(
    string Key,
    string Label,
    TimeSpan Duration);

public sealed record ApplicationLogSlice(
    string WindowKey,
    string WindowLabel,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ApplicationLogEntry> Entries);
