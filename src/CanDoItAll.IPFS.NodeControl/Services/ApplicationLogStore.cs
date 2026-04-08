using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class ApplicationLogStoreOptions
{
    public string? FilePath { get; set; }

    public int MaxEntriesPerWindow { get; set; } = 400;

    public int MaxEntriesPerFile { get; set; } = 2000;

    public int RetainedArchiveFileCount { get; set; } = 3;
}

public sealed class ApplicationLogStore(IOptions<ApplicationLogStoreOptions> options) : IApplicationLogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<ApplicationLogWindowPreset> Presets =
    [
        new("10m", "Last 10 minutes", TimeSpan.FromMinutes(10)),
        new("1h", "Last hour", TimeSpan.FromHours(1)),
        new("6h", "Last 6 hours", TimeSpan.FromHours(6)),
        new("24h", "Last 24 hours", TimeSpan.FromHours(24))
    ];

    private readonly object sync = new();
    private readonly string filePath = ResolveFilePath(options.Value.FilePath);
    private readonly int maxEntriesPerWindow = Math.Clamp(options.Value.MaxEntriesPerWindow, 50, 5000);
    private readonly int maxEntriesPerFile = Math.Clamp(options.Value.MaxEntriesPerFile, 2, 100000);
    private readonly int retainedArchiveFileCount = Math.Clamp(options.Value.RetainedArchiveFileCount, 1, 20);

    public string FilePath => filePath;

    public IReadOnlyList<ApplicationLogWindowPreset> GetWindowPresets()
        => Presets;

    public ApplicationLogSlice ReadRecent(string? windowKey, int? maxEntries = null)
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "application-log-read" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.application-log.read", ActivityKind.Internal, tags);
        var preset = ResolveWindow(windowKey);
        try
        {
            var entries = ReadRecentCore(preset.Duration, Math.Clamp(maxEntries ?? maxEntriesPerWindow, 20, 5000));
            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation(
                "persistence",
                "application-log-read",
                "success",
                Stopwatch.GetElapsedTime(start),
                tags);
            return new ApplicationLogSlice(
                preset.Key,
                preset.Label,
                DateTimeOffset.UtcNow,
                entries);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation(
                "persistence",
                "application-log-read",
                "failure",
                Stopwatch.GetElapsedTime(start),
                tags);
            throw;
        }
    }

    public byte[] BuildPlainTextSlice(ApplicationLogSlice slice)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IPFS Node Control logs");
        builder.AppendLine($"Window: {slice.WindowLabel}");
        builder.AppendLine($"Generated: {slice.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine();

        foreach (var entry in slice.Entries)
        {
            builder.Append('[')
                .Append(entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))
                .Append("] ")
                .Append(entry.Level)
                .Append(' ')
                .Append(entry.Category)
                .Append(": ");

            if (!string.IsNullOrWhiteSpace(entry.CorrelationId))
            {
                builder.Append("[corr:")
                    .Append(entry.CorrelationId)
                    .Append("] ");
            }

            if (!string.IsNullOrWhiteSpace(entry.TraceId))
            {
                builder.Append("[trace:")
                    .Append(entry.TraceId)
                    .Append("] ");
            }

            builder
                .AppendLine(entry.Message);

            if (!string.IsNullOrWhiteSpace(entry.Exception))
            {
                builder.AppendLine(entry.Exception);
            }

            if (entry.Dimensions is { Count: > 0 })
            {
                builder.Append("Dimensions: ")
                    .AppendLine(string.Join(
                        ", ",
                        entry.Dimensions
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair => $"{pair.Key}={pair.Value}")));
            }

            builder.AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception)
        => Write(CreateStructuredEntry(
            level.ToString(),
            category,
            eventId.Id,
            string.IsNullOrWhiteSpace(message) ? exception?.Message ?? string.Empty : message,
            exception?.ToString(),
            dimensions: null));

    public void Write(ApplicationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "application-log-write" },
            { "log.category", entry.Category },
            { "log.level", entry.Level }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.application-log.write", ActivityKind.Internal, tags);

        lock (sync)
        {
            try
            {
                PersistentFileUtilities.EnsureParentDirectory(filePath);
                RotateIfNeeded();

                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.WriteLine(JsonSerializer.Serialize(entry, SerializerOptions));
                writer.Flush();
                stream.Flush(true);
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation(
                    "persistence",
                    "application-log-write",
                    "success",
                    Stopwatch.GetElapsedTime(start),
                    tags);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation(
                    "persistence",
                    "application-log-write",
                    "failure",
                    Stopwatch.GetElapsedTime(start),
                    tags);
                throw;
            }
        }
    }

    public static ApplicationLogWindowPreset ResolveWindow(string? windowKey)
        => Presets.FirstOrDefault(preset => string.Equals(preset.Key, windowKey?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? Presets[1];

    private IReadOnlyList<ApplicationLogEntry> ReadRecentCore(TimeSpan window, int maxEntries)
    {
        lock (sync)
        {
            var cutoff = DateTimeOffset.UtcNow - window;
            return GetLogFilesForRead()
                .SelectMany(path => File.ReadLines(path))
                .Select(TryDeserialize)
                .OfType<ApplicationLogEntry>()
                .Where(entry => entry.TimestampUtc >= cutoff)
                .OrderByDescending(entry => entry.TimestampUtc)
                .Take(maxEntries)
                .ToList();
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var currentEntryCount = File.ReadLines(filePath).Count(line => !string.IsNullOrWhiteSpace(line));
        if (currentEntryCount < maxEntriesPerFile)
        {
            return;
        }

        var archivePath = BuildArchivePath();
        File.Move(filePath, archivePath);
        PruneArchives();
    }

    private IEnumerable<string> GetLogFilesForRead()
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            if (File.Exists(filePath))
            {
                yield return filePath;
            }

            yield break;
        }

        if (File.Exists(filePath))
        {
            yield return filePath;
        }

        var archiveSearchPattern = $"{Path.GetFileNameWithoutExtension(filePath)}-*{Path.GetExtension(filePath)}";
        foreach (var archivePath in Directory.EnumerateFiles(directory, archiveSearchPattern, SearchOption.TopDirectoryOnly)
                     .OrderByDescending(path => path, StringComparer.Ordinal))
        {
            yield return archivePath;
        }
    }

    private string BuildArchivePath()
    {
        var directory = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        return Path.Combine(directory, $"{fileNameWithoutExtension}-{timestamp}{extension}");
    }

    private void PruneArchives()
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var archiveSearchPattern = $"{Path.GetFileNameWithoutExtension(filePath)}-*{Path.GetExtension(filePath)}";
        foreach (var archivePath in Directory.EnumerateFiles(directory, archiveSearchPattern, SearchOption.TopDirectoryOnly)
                     .OrderByDescending(path => path, StringComparer.Ordinal)
                     .Skip(retainedArchiveFileCount))
        {
            File.Delete(archivePath);
        }
    }

    private static ApplicationLogEntry? TryDeserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ApplicationLogEntry>(line, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolveFilePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath.Trim());
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.GetTempPath()
            : localAppData;
        return Path.Combine(baseDirectory, "IpfsNodeControl", "logs", "application.log");
    }

    private static ApplicationLogEntry CreateStructuredEntry(
        string level,
        string category,
        int eventId,
        string message,
        string? exception,
        Dictionary<string, string>? dimensions)
    {
        var activity = Activity.Current;
        return new ApplicationLogEntry(
            DateTimeOffset.UtcNow,
            level,
            category,
            message,
            eventId,
            exception,
            ResolveCorrelationId(activity, dimensions),
            activity?.TraceId.ToString(),
            activity?.SpanId.ToString(),
            dimensions);
    }

    private static string? ResolveCorrelationId(Activity? activity, IReadOnlyDictionary<string, string>? dimensions)
    {
        if (dimensions is not null
            && dimensions.TryGetValue(NodeControlTelemetry.CorrelationScopeKey, out var scopedCorrelationId)
            && !string.IsNullOrWhiteSpace(scopedCorrelationId))
        {
            return scopedCorrelationId;
        }

        var taggedCorrelationId = activity?.GetTagItem(NodeControlTelemetry.CorrelationTagName)?.ToString();
        if (!string.IsNullOrWhiteSpace(taggedCorrelationId))
        {
            return taggedCorrelationId;
        }

        if (activity is null)
        {
            return null;
        }

        return activity.TraceId != default
            ? activity.TraceId.ToString()
            : activity.Id;
    }
}

public sealed class ApplicationLogLoggerProvider(IApplicationLogStore store) : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName)
        => new ApplicationLogLogger(categoryName, store, () => scopeProvider);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        => this.scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();

    private sealed class ApplicationLogLogger(
        string categoryName,
        IApplicationLogStore store,
        Func<IExternalScopeProvider> scopeProviderAccessor) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => scopeProviderAccessor().Push(state);

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var dimensions = CollectDimensions(state, scopeProviderAccessor());
            store.Write(new ApplicationLogEntry(
                DateTimeOffset.UtcNow,
                logLevel.ToString(),
                categoryName,
                message,
                eventId.Id,
                exception?.ToString(),
                ResolveCorrelationId(dimensions),
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                dimensions.Count == 0 ? null : dimensions));
        }

        private static Dictionary<string, string> CollectDimensions<TState>(TState state, IExternalScopeProvider scopeProvider)
        {
            var dimensions = new Dictionary<string, string>(StringComparer.Ordinal);
            AppendStructuredValues(dimensions, state);
            scopeProvider.ForEachScope(static (scope, collected) => AppendStructuredValues(collected, scope), dimensions);
            return dimensions;
        }

        private static void AppendStructuredValues(Dictionary<string, string> dimensions, object? state)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                foreach (var pair in structuredState)
                {
                    if (string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (pair.Value is null)
                    {
                        continue;
                    }

                    dimensions[pair.Key] = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? pair.Value.ToString() ?? string.Empty;
                }

                return;
            }

            if (state is KeyValuePair<string, object?> singleValue
                && singleValue.Value is not null)
            {
                dimensions[singleValue.Key] = Convert.ToString(singleValue.Value, CultureInfo.InvariantCulture) ?? singleValue.Value.ToString() ?? string.Empty;
            }
        }

        private static string? ResolveCorrelationId(IReadOnlyDictionary<string, string> dimensions)
        {
            if (dimensions.TryGetValue(NodeControlTelemetry.CorrelationScopeKey, out var correlationId)
                && !string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }

            var taggedCorrelationId = Activity.Current?.GetTagItem(NodeControlTelemetry.CorrelationTagName)?.ToString();
            if (!string.IsNullOrWhiteSpace(taggedCorrelationId))
            {
                return taggedCorrelationId;
            }

            var currentActivity = Activity.Current;
            return currentActivity?.TraceId != default
                ? currentActivity?.TraceId.ToString()
                : currentActivity?.Id;
        }
    }
}
