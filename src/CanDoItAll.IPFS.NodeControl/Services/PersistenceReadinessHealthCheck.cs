using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class PersistenceReadinessHealthCheck(
    ServerNodeSettingsStore serverNodeSettingsStore,
    RemotePinRequestStore remotePinRequestStore,
    ApplicationLogStore applicationLogStore,
    ExplorerIndexStore explorerIndexStore)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var start = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { NodeControlTelemetry.AreaTagName, "health" },
            { NodeControlTelemetry.OperationTagName, "persistence-readiness" }
        };
        using var activity = NodeControlTelemetry.StartActivity("health.persistence-readiness", ActivityKind.Internal, tags);

        try
        {
            var data = new Dictionary<string, object>();

            ProbeFileStore("settings", serverNodeSettingsStore.FilePath);
            _ = serverNodeSettingsStore.Load();
            data["settingsStorePath"] = serverNodeSettingsStore.FilePath;

            ProbeFileStore("remotePin", remotePinRequestStore.FilePath);
            data["remotePinRequestCount"] = remotePinRequestStore.List().Count;
            data["remotePinStorePath"] = remotePinRequestStore.FilePath;

            ProbeFileStore("applicationLog", applicationLogStore.FilePath);
            _ = applicationLogStore.ReadRecent("10m", 1);
            data["applicationLogPath"] = applicationLogStore.FilePath;

            ProbeFileStore("explorerIndex", explorerIndexStore.FilePath);
            data["hasPinnedRoots"] = explorerIndexStore.HasPinnedRoots();
            data["explorerIndexPath"] = explorerIndexStore.FilePath;

            var elapsed = Stopwatch.GetElapsedTime(start);
            activity?.SetStatus(ActivityStatusCode.Ok);
            NodeControlTelemetry.RecordOperation("health", "persistence-readiness", "healthy", elapsed, tags);
            return Task.FromResult(HealthCheckResult.Healthy(
                description: "Control-app persistence stores are reachable.",
                data: data));
        }
        catch (Exception ex)
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NodeControlTelemetry.RecordOperation("health", "persistence-readiness", "unhealthy", elapsed, tags);
            return Task.FromResult(HealthCheckResult.Unhealthy(
                description: "One or more control-app persistence stores are not reachable.",
                exception: ex));
        }
    }

    private static void ProbeFileStore(string name, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException($"The {name} store path is not configured.");
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"The {name} store path '{filePath}' does not have a parent directory.");
        }

        Directory.CreateDirectory(directory);
        var probePath = Path.Combine(directory, $".health-probe-{Guid.NewGuid():N}");
        using var _ = new FileStream(
            probePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.DeleteOnClose);
    }
}
