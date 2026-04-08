using System.Text.Json;
using System.Diagnostics;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class RemotePinRequestStoreOptions
{
    public string? FilePath { get; set; }
}

public sealed class RemotePinRequestStore(IOptions<RemotePinRequestStoreOptions> options) : IRemotePinRequestStore
{
    internal const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object sync = new();
    private readonly string filePath = ResolveFilePath(options.Value?.FilePath);

    public string FilePath => filePath;

    public event Action<StoredRemotePinRequest>? RequestChanged;

    public IReadOnlyList<StoredRemotePinRequest> List()
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "remote-pin-request-list" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.remote-pin-request.list", ActivityKind.Internal, tags);
        lock (sync)
        {
            try
            {
                var items = LoadAllCore()
                    .OrderByDescending(item => item.ReceivedAtUtc)
                    .ToList();
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "remote-pin-request-list", "success", Stopwatch.GetElapsedTime(start), tags);
                return items;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "remote-pin-request-list", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }
    }

    public StoredRemotePinRequest Add(RemotePinRequestEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "remote-pin-request-add" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.remote-pin-request.add", ActivityKind.Internal, tags);
        StoredRemotePinRequest stored;
        lock (sync)
        {
            try
            {
                var items = LoadAllCore();
                stored = items.FirstOrDefault(item => string.Equals(item.Request.RequestId, request.RequestId, StringComparison.Ordinal))
                    ?? new StoredRemotePinRequest
                    {
                        Request = request,
                        ReceivedAtUtc = DateTimeOffset.UtcNow,
                        State = RemotePinRequestState.Pending
                    };

                if (!items.Any(item => string.Equals(item.Request.RequestId, request.RequestId, StringComparison.Ordinal)))
                {
                    items.Add(stored);
                    SaveAllCore(items);
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "remote-pin-request-add", "success", Stopwatch.GetElapsedTime(start), tags);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "remote-pin-request-add", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }

        RequestChanged?.Invoke(stored);
        return stored;
    }

    public StoredRemotePinRequest? Get(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        lock (sync)
        {
            return LoadAllCore()
                .FirstOrDefault(item => string.Equals(item.Request.RequestId, requestId.Trim(), StringComparison.Ordinal));
        }
    }

    public StoredRemotePinRequest Update(string requestId, Action<StoredRemotePinRequest> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(update);

        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "remote-pin-request-update" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.remote-pin-request.update", ActivityKind.Internal, tags);
        StoredRemotePinRequest updated;
        lock (sync)
        {
            try
            {
                var items = LoadAllCore();
                updated = items.FirstOrDefault(item => string.Equals(item.Request.RequestId, requestId.Trim(), StringComparison.Ordinal))
                    ?? throw new KeyNotFoundException($"Remote pin request '{requestId}' was not found.");

                update(updated);
                SaveAllCore(items);
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "remote-pin-request-update", "success", Stopwatch.GetElapsedTime(start), tags);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "remote-pin-request-update", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }

        RequestChanged?.Invoke(updated);
        return updated;
    }

    private List<StoredRemotePinRequest> LoadAllCore()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyItems = JsonSerializer.Deserialize<List<StoredRemotePinRequest>>(json, SerializerOptions) ?? [];
                SaveAllCore(legacyItems);
                return legacyItems;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                PersistentFileUtilities.QuarantineFile(filePath, new InvalidDataException("The remote pin request store document must be an object or array."));
                return [];
            }

            var persistedDocument = JsonSerializer.Deserialize<RemotePinRequestStoreDocument>(json, SerializerOptions);
            if (persistedDocument is null || persistedDocument.SchemaVersion != CurrentSchemaVersion)
            {
                PersistentFileUtilities.QuarantineFile(filePath, new InvalidDataException("The remote pin request store schema version is not supported."));
                return [];
            }

            return persistedDocument.Requests ?? [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (JsonException ex)
        {
            PersistentFileUtilities.QuarantineFile(filePath, ex);
            return [];
        }
    }

    private void SaveAllCore(IReadOnlyList<StoredRemotePinRequest> items)
    {
        var persistedDocument = new RemotePinRequestStoreDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            SavedAtUtc = DateTimeOffset.UtcNow,
            Requests = [.. items]
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

        return Path.Combine(baseDirectory, "IpfsNodeControl", "remote-pin-requests.json");
    }

    private sealed class RemotePinRequestStoreDocument
    {
        public int SchemaVersion { get; init; }

        public DateTimeOffset SavedAtUtc { get; init; }

        public List<StoredRemotePinRequest>? Requests { get; init; }
    }
}
