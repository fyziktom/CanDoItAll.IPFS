using System.Text.Json;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.JSInterop;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeSettingsBrowserStorage(IJSRuntime jsRuntime)
{
    private const string StorageKey = "ipfs-node-control.settings";

    public async Task<NodeConnectionSettings?> LoadAsync()
    {
        var json = await jsRuntime.InvokeAsync<string?>("ipfsNodeControlSettingsStorage.get", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NodeConnectionSettings>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task SaveAsync(NodeConnectionSettings settings)
    {
        var normalized = settings.Clone().Normalize();
        var json = JsonSerializer.Serialize(normalized);
        return jsRuntime.InvokeVoidAsync("ipfsNodeControlSettingsStorage.set", StorageKey, json).AsTask();
    }

    public Task ClearAsync()
        => jsRuntime.InvokeVoidAsync("ipfsNodeControlSettingsStorage.remove", StorageKey).AsTask();
}
