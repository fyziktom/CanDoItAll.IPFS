using System.Globalization;
using System.Diagnostics;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class ExplorerIndexStoreOptions
{
    public string? FilePath { get; set; }
}

public sealed class ExplorerIndexStore(IOptions<ExplorerIndexStoreOptions> options) : IExplorerIndexStore
{
    internal const int CurrentSchemaVersion = 1;
    private const string SchemaTableName = "ExplorerIndexSchemaVersion";
    private const string SchemaName = "ExplorerIndexStore";
    private const string TableName = "ExplorerPinnedRootIndex";
    private readonly object sync = new();
    private readonly string filePath = ResolveFilePath(options.Value.FilePath);
    private readonly string connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = ResolveFilePath(options.Value.FilePath),
        Cache = SqliteCacheMode.Shared,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();
    private bool schemaInitialized;

    public string FilePath => filePath;

    public bool HasPinnedRoots()
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "explorer-index-has-pinned-roots" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.explorer-index.has-pinned-roots", ActivityKind.Internal, tags);
        lock (sync)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {TableName} WHERE IsPinned = 1 LIMIT 1);";
                var result = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "explorer-index-has-pinned-roots", "success", Stopwatch.GetElapsedTime(start), tags);
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "explorer-index-has-pinned-roots", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }
    }

    public IReadOnlyList<ExplorerIndexedRootRecord> ListPinnedRoots()
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "explorer-index-list-pinned-roots" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.explorer-index.list-pinned-roots", ActivityKind.Internal, tags);
        lock (sync)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText =
                    $$"""
                    SELECT Target, DisplayName, IsDirectory, Size, ChildCount, FirstPinnedAtUtc, LastSeenPinnedAtUtc, LastMetadataRefreshAtUtc, IsPinned
                    FROM {{TableName}}
                    WHERE IsPinned = 1
                    ORDER BY IsDirectory DESC, DisplayName COLLATE NOCASE, Target;
                    """;
                using var reader = command.ExecuteReader();
                var items = new List<ExplorerIndexedRootRecord>();
                while (reader.Read())
                {
                    items.Add(ReadRecord(reader));
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "explorer-index-list-pinned-roots", "success", Stopwatch.GetElapsedTime(start), tags);
                return items;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "explorer-index-list-pinned-roots", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }
    }

    public ExplorerIndexedRootRecord? GetRoot(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        lock (sync)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                $$"""
                SELECT Target, DisplayName, IsDirectory, Size, ChildCount, FirstPinnedAtUtc, LastSeenPinnedAtUtc, LastMetadataRefreshAtUtc, IsPinned
                FROM {{TableName}}
                WHERE Target = $target
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$target", target.Trim());
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }
    }

    public void UpsertRoot(ExplorerIndexedRootRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "persistence" },
            { NodeControlTelemetry.OperationTagName, "explorer-index-upsert-root" }
        };
        using var activity = NodeControlTelemetry.StartActivity("persistence.explorer-index.upsert-root", ActivityKind.Internal, tags);
        lock (sync)
        {
            try
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    $$"""
                    INSERT INTO {{TableName}} (
                        Target,
                        DisplayName,
                        IsDirectory,
                        Size,
                        ChildCount,
                        FirstPinnedAtUtc,
                        LastSeenPinnedAtUtc,
                        LastMetadataRefreshAtUtc,
                        IsPinned)
                    VALUES (
                        $target,
                        $displayName,
                        $isDirectory,
                        $size,
                        $childCount,
                        $firstPinnedAtUtc,
                        $lastSeenPinnedAtUtc,
                        $lastMetadataRefreshAtUtc,
                        $isPinned)
                    ON CONFLICT(Target) DO UPDATE SET
                        DisplayName = excluded.DisplayName,
                        IsDirectory = excluded.IsDirectory,
                        Size = excluded.Size,
                        ChildCount = excluded.ChildCount,
                        LastSeenPinnedAtUtc = excluded.LastSeenPinnedAtUtc,
                        LastMetadataRefreshAtUtc = excluded.LastMetadataRefreshAtUtc,
                        IsPinned = excluded.IsPinned,
                        FirstPinnedAtUtc = CASE
                            WHEN {{TableName}}.FirstPinnedAtUtc IS NULL OR {{TableName}}.FirstPinnedAtUtc = ''
                                THEN excluded.FirstPinnedAtUtc
                            ELSE {{TableName}}.FirstPinnedAtUtc
                        END;
                    """;
                AddParameters(command, record);
                command.ExecuteNonQuery();
                transaction.Commit();
                activity?.SetStatus(ActivityStatusCode.Ok);
                NodeControlTelemetry.RecordOperation("persistence", "explorer-index-upsert-root", "success", Stopwatch.GetElapsedTime(start), tags);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                NodeControlTelemetry.RecordOperation("persistence", "explorer-index-upsert-root", "failure", Stopwatch.GetElapsedTime(start), tags);
                throw;
            }
        }
    }

    public void MarkPinnedRootsSeen(IReadOnlyCollection<string> targets, DateTimeOffset seenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count == 0)
        {
            return;
        }

        lock (sync)
        {
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            var parameterNames = new List<string>(targets.Count);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            for (var index = 0; index < targets.Count; index++)
            {
                var parameterName = $"$target{index}";
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, targets.ElementAt(index));
            }

            command.Parameters.AddWithValue("$seenAtUtc", seenAtUtc.ToString("O", CultureInfo.InvariantCulture));
            command.CommandText =
                $$"""
                UPDATE {{TableName}}
                SET IsPinned = 1,
                    LastSeenPinnedAtUtc = $seenAtUtc
                WHERE Target IN ({{string.Join(", ", parameterNames)}});
                """;
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public void MarkMissingPinnedRootsAsUnpinned(IReadOnlyCollection<string> pinnedTargets)
    {
        ArgumentNullException.ThrowIfNull(pinnedTargets);

        lock (sync)
        {
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (pinnedTargets.Count == 0)
            {
                command.CommandText = $"UPDATE {TableName} SET IsPinned = 0;";
            }
            else
            {
                var parameterNames = new List<string>(pinnedTargets.Count);
                for (var index = 0; index < pinnedTargets.Count; index++)
                {
                    var parameterName = $"$target{index}";
                    parameterNames.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, pinnedTargets.ElementAt(index));
                }

                command.CommandText =
                    $$"""
                    UPDATE {{TableName}}
                    SET IsPinned = 0
                    WHERE Target NOT IN ({{string.Join(", ", parameterNames)}});
                    """;
            }

            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public void MarkUnpinned(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        lock (sync)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                $$"""
                UPDATE {{TableName}}
                SET IsPinned = 0
                WHERE Target = $target;
                """;
            command.Parameters.AddWithValue("$target", target.Trim());
            command.ExecuteNonQuery();
        }
    }

    private SqliteConnection OpenConnection()
    {
        if (!schemaInitialized)
        {
            EnsureDirectory();
        }

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        connection.DefaultTimeout = 10;
        if (!schemaInitialized)
        {
            try
            {
                EnsureSchema(connection);
            }
            catch (SqliteException ex)
            {
                connection.Dispose();
                PersistentFileUtilities.QuarantineRelatedFiles(filePath, ex, "-wal", "-shm");

                var recoveredConnection = new SqliteConnection(connectionString);
                recoveredConnection.Open();
                recoveredConnection.DefaultTimeout = 10;
                EnsureSchema(recoveredConnection);
                return recoveredConnection;
            }
        }

        return connection;
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void EnsureSchema(SqliteConnection connection)
    {
        if (schemaInitialized)
        {
            return;
        }

        using var journalCommand = connection.CreateCommand();
        journalCommand.CommandText = "PRAGMA journal_mode = WAL;";
        journalCommand.ExecuteScalar();

        var schemaVersion = GetSchemaVersion(connection);
        if (schemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Explorer index schema version {schemaVersion} is newer than the supported version {CurrentSchemaVersion}.");
        }

        if (schemaVersion < CurrentSchemaVersion)
        {
            ApplyVersionOneSchema(connection);
        }
        else
        {
            EnsureCurrentSchemaArtifacts(connection);
        }

        schemaInitialized = true;
    }

    internal int GetSchemaVersionForTests()
    {
        lock (sync)
        {
            using var connection = OpenConnection();
            return GetSchemaVersion(connection);
        }
    }

    private void ApplyVersionOneSchema(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $$"""
            CREATE TABLE IF NOT EXISTS {{TableName}} (
                Target TEXT NOT NULL PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                IsDirectory INTEGER NOT NULL,
                Size INTEGER NOT NULL,
                ChildCount INTEGER NOT NULL,
                FirstPinnedAtUtc TEXT NOT NULL,
                LastSeenPinnedAtUtc TEXT NOT NULL,
                LastMetadataRefreshAtUtc TEXT NOT NULL,
                IsPinned INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_{{TableName}}_Pinned_FirstPinnedAtUtc
                ON {{TableName}} (IsPinned, FirstPinnedAtUtc);
            CREATE TABLE IF NOT EXISTS {{SchemaTableName}} (
                Name TEXT NOT NULL PRIMARY KEY,
                SchemaVersion INTEGER NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            INSERT INTO {{SchemaTableName}} (Name, SchemaVersion, UpdatedAtUtc)
            VALUES ($schemaName, $schemaVersion, $updatedAtUtc)
            ON CONFLICT(Name) DO UPDATE SET
                SchemaVersion = excluded.SchemaVersion,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$schemaName", SchemaName);
        command.Parameters.AddWithValue("$schemaVersion", CurrentSchemaVersion);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private void EnsureCurrentSchemaArtifacts(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            $$"""
            CREATE TABLE IF NOT EXISTS {{TableName}} (
                Target TEXT NOT NULL PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                IsDirectory INTEGER NOT NULL,
                Size INTEGER NOT NULL,
                ChildCount INTEGER NOT NULL,
                FirstPinnedAtUtc TEXT NOT NULL,
                LastSeenPinnedAtUtc TEXT NOT NULL,
                LastMetadataRefreshAtUtc TEXT NOT NULL,
                IsPinned INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_{{TableName}}_Pinned_FirstPinnedAtUtc
                ON {{TableName}} (IsPinned, FirstPinnedAtUtc);
            CREATE TABLE IF NOT EXISTS {{SchemaTableName}} (
                Name TEXT NOT NULL PRIMARY KEY,
                SchemaVersion INTEGER NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        if (!TableExists(connection, SchemaTableName))
        {
            return 0;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            $$"""
            SELECT SchemaVersion
            FROM {{SchemaTableName}}
            WHERE Name = $schemaName
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$schemaName", SchemaName);
        var value = command.ExecuteScalar();
        return value is null || value is DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS(
                SELECT 1
                FROM sqlite_master
                WHERE type = 'table' AND name = $tableName
                LIMIT 1);
            """;
        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    private static void AddParameters(SqliteCommand command, ExplorerIndexedRootRecord record)
    {
        command.Parameters.AddWithValue("$target", record.Target);
        command.Parameters.AddWithValue("$displayName", record.DisplayName);
        command.Parameters.AddWithValue("$isDirectory", record.IsDirectory ? 1 : 0);
        command.Parameters.AddWithValue("$size", record.Size);
        command.Parameters.AddWithValue("$childCount", record.ChildCount);
        command.Parameters.AddWithValue("$firstPinnedAtUtc", record.FirstPinnedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lastSeenPinnedAtUtc", record.LastSeenPinnedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lastMetadataRefreshAtUtc", record.LastMetadataRefreshAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$isPinned", record.IsPinned ? 1 : 0);
    }

    private static ExplorerIndexedRootRecord ReadRecord(SqliteDataReader reader)
        => new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2) == 1,
            reader.GetInt64(3),
            reader.GetInt32(4),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.GetInt64(8) == 1);

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
        return Path.Combine(baseDirectory, "IpfsNodeControl", "explorer-index", "explorer.db");
    }
}
