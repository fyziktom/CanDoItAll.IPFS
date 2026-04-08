using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface IApplicationLogStore
{
    string FilePath { get; }

    byte[] BuildPlainTextSlice(ApplicationLogSlice slice);

    IReadOnlyList<ApplicationLogWindowPreset> GetWindowPresets();

    ApplicationLogSlice ReadRecent(string? windowKey, int? maxEntries = null);

    void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception);

    void Write(ApplicationLogEntry entry);
}
