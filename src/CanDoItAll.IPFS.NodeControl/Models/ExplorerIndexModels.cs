namespace CanDoItAll.IPFS.NodeControl.Models;

public sealed record ExplorerIndexedRootRecord(
    string Target,
    string DisplayName,
    bool IsDirectory,
    long Size,
    int ChildCount,
    DateTimeOffset FirstPinnedAtUtc,
    DateTimeOffset LastSeenPinnedAtUtc,
    DateTimeOffset LastMetadataRefreshAtUtc,
    bool IsPinned);
