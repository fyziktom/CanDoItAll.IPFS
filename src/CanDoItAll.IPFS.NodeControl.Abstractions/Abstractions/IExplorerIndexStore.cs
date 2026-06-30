using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface IExplorerIndexStore
{
    string FilePath { get; }

    ExplorerIndexedRootRecord? GetRoot(string target);

    bool HasPinnedRoots();

    IReadOnlyList<ExplorerIndexedRootRecord> ListPinnedRoots();

    void MarkMissingPinnedRootsAsUnpinned(IReadOnlyCollection<string> pinnedTargets);

    void MarkPinnedRootsSeen(IReadOnlyCollection<string> targets, DateTimeOffset seenAtUtc);

    void MarkUnpinned(string target);

    void UpsertRoot(ExplorerIndexedRootRecord record);
}
