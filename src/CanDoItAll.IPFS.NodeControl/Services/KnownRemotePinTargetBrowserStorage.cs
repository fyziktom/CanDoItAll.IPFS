using System.Text.Json;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.JSInterop;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class KnownRemotePinTargetBrowserStorage(IJSRuntime jsRuntime)
{
    private const string StorageKey = "ipfs-node-control.remote-pin-targets";

    public async Task<IReadOnlyList<KnownRemotePinTarget>> LoadAsync()
    {
        var json = await jsRuntime.InvokeAsync<string?>("ipfsNodeControlSettingsStorage.get", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<KnownRemotePinTarget>>(json) ?? [])
                .Select(Normalize)
                .Where(target => !string.IsNullOrWhiteSpace(target.ControlAppUrl))
                .OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(target => target.ControlAppUrl, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public Task SaveAsync(IReadOnlyList<KnownRemotePinTarget> targets)
    {
        var normalizedTargets = (targets ?? [])
            .Select(Normalize)
            .Where(target => !string.IsNullOrWhiteSpace(target.ControlAppUrl))
            .GroupBy(target => target.ControlAppUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.ControlAppUrl, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(normalizedTargets);
        return jsRuntime.InvokeVoidAsync("ipfsNodeControlSettingsStorage.set", StorageKey, json).AsTask();
    }

    private static KnownRemotePinTarget Normalize(KnownRemotePinTarget? target)
    {
        var normalized = target ?? new KnownRemotePinTarget();
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id)
            ? Guid.NewGuid().ToString("N")
            : normalized.Id.Trim();
        normalized.Label = string.IsNullOrWhiteSpace(normalized.Label)
            ? "Remote receiver"
            : normalized.Label.Trim();
        normalized.ControlAppUrl = string.IsNullOrWhiteSpace(normalized.ControlAppUrl)
            ? string.Empty
            : RemotePinShareService.NormalizeControlAppUrl(normalized.ControlAppUrl);
        normalized.LastKnownNodeLabel = string.IsNullOrWhiteSpace(normalized.LastKnownNodeLabel)
            ? null
            : normalized.LastKnownNodeLabel.Trim();
        normalized.LastKnownPeerId = string.IsNullOrWhiteSpace(normalized.LastKnownPeerId)
            ? null
            : normalized.LastKnownPeerId.Trim();
        normalized.LastFailureMessage = string.IsNullOrWhiteSpace(normalized.LastFailureMessage)
            ? null
            : normalized.LastFailureMessage.Trim();
        return normalized;
    }
}
