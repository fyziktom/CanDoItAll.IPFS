using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class CurrentNodeTargetRegistry
{
    private readonly object sync = new();

    private NodeConnectionSettings currentSettings;

    public CurrentNodeTargetRegistry()
    {
        currentSettings = new NodeConnectionSettings().Normalize();
    }

    public CurrentNodeTargetRegistry(
        IOptions<NodeConnectionSettings> defaults,
        IServerNodeSettingsStore serverNodeSettingsStore)
    {
        var persistedSettings = serverNodeSettingsStore.Load();
        currentSettings = persistedSettings
            ?? defaults.Value?.Clone()
            ?? new NodeConnectionSettings();
        currentSettings.Normalize();
        IsHydrated = persistedSettings is not null;
    }

    public bool IsHydrated { get; private set; }

    public NodeConnectionSettings Current
    {
        get
        {
            lock (sync)
            {
                return currentSettings.Clone();
            }
        }
    }

    public void Update(NodeConnectionSettings settings, bool isHydrated)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (sync)
        {
            if (!IsHydrated || isHydrated)
            {
                currentSettings = settings.Clone().Normalize();
            }

            IsHydrated = IsHydrated || isHydrated;
        }
    }
}
