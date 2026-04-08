using System.Text.Json;
using System.Diagnostics;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class ServerNodeSettingsStoreOptions
{
    public string? FilePath { get; set; }
}

public sealed class ServerNodeSettingsStore(IOptions<ServerNodeSettingsStoreOptions> options) : IServerNodeSettingsStore
{
    internal const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object sync = new();
    private readonly string filePath = ResolveFilePath(options.Value?.FilePath);

    public string FilePath => filePath;

    public NodeConnectionSettings? Load()
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "server-node-settings-load" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.server-node-settings.load", ActivityKind.Internal, tags);
        lock (sync)
        {
            if (!File.Exists(filePath))
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "not-found", Stopwatch.GetElapsedTime(start), tags);
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    PersistentFileUtilities.QuarantineFile(filePath, new InvalidDataException("The persisted node settings document must be a JSON object."));
                    activity?.SetStatus(ActivityStatusCode.Error, "The persisted node settings document must be a JSON object.");
                    NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "failure", Stopwatch.GetElapsedTime(start), tags);
                    return null;
                }

                if (document.RootElement.TryGetProperty("schemaVersion", out _))
                {
                    var persistedDocument = JsonSerializer.Deserialize<ServerNodeSettingsDocument>(json, SerializerOptions);
                    if (persistedDocument is null || persistedDocument.SchemaVersion != CurrentSchemaVersion)
                    {
                        PersistentFileUtilities.QuarantineFile(filePath, new InvalidDataException("The persisted node settings schema version is not supported."));
                        activity?.SetStatus(ActivityStatusCode.Error, "The persisted node settings schema version is not supported.");
                        NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "failure", Stopwatch.GetElapsedTime(start), tags);
                        return null;
                    }

                    activity?.SetStatus(ActivityStatusCode.Ok);
                    NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "success", Stopwatch.GetElapsedTime(start), tags);
                    return persistedDocument.CurrentNode.Normalize();
                }

                var legacySettings = JsonSerializer.Deserialize<NodeConnectionSettings>(json, SerializerOptions)?.Normalize();
                if (legacySettings is null)
                {
                    PersistentFileUtilities.QuarantineFile(filePath, new InvalidDataException("The persisted node settings document could not be deserialized."));
                    activity?.SetStatus(ActivityStatusCode.Error, "The persisted node settings document could not be deserialized.");
                    NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "failure", Stopwatch.GetElapsedTime(start), tags);
                    return null;
                }

                SaveCore(legacySettings);
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "migrated", Stopwatch.GetElapsedTime(start), tags);
                return legacySettings;
            }
            catch (IOException)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "The persisted node settings document could not be read.");
                NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "failure", Stopwatch.GetElapsedTime(start), tags);
                return null;
            }
            catch (JsonException ex)
            {
                PersistentFileUtilities.QuarantineFile(filePath, ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-load", "failure", Stopwatch.GetElapsedTime(start), tags);
                return null;
            }
        }
    }

    public void Save(NodeConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Clone().Normalize();
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "server-node-settings-save" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.server-node-settings.save", ActivityKind.Internal, tags);
        lock (sync)
        {
            try
            {
                SaveCore(normalized);
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-save", "success", Stopwatch.GetElapsedTime(start), tags);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "server-node-settings-save", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    internal string GetFilePathForTests()
        => filePath;

    private void SaveCore(NodeConnectionSettings settings)
    {
        var persistedDocument = new ServerNodeSettingsDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            SavedAtUtc = DateTimeOffset.UtcNow,
            CurrentNode = settings
        };

        var json = JsonSerializer.Serialize(persistedDocument, SerializerOptions);
        PersistentFileUtilities.WriteAllTextAtomically(filePath, json);
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

        return Path.Combine(baseDirectory, "IpfsNodeControl", "current-node-settings.json");
    }

    private sealed class ServerNodeSettingsDocument
    {
        public int SchemaVersion { get; init; }

        public DateTimeOffset SavedAtUtc { get; init; }

        public required NodeConnectionSettings CurrentNode { get; init; }
    }
}
