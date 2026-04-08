using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeSessionState
{
    private readonly CurrentNodeTargetRegistry targetRegistry;
    private NodeConnectionSettings currentSettings;

    public NodeSessionState(IOptions<NodeConnectionSettings> defaults, CurrentNodeTargetRegistry targetRegistry)
    {
        this.targetRegistry = targetRegistry;
        currentSettings = targetRegistry.IsHydrated
            ? targetRegistry.Current
            : defaults.Value?.Clone() ?? new NodeConnectionSettings();

        if (string.IsNullOrWhiteSpace(currentSettings.BaseUrl))
        {
            currentSettings = defaults.Value?.Clone() ?? new NodeConnectionSettings();
        }

        currentSettings.Normalize();
        if (!targetRegistry.IsHydrated)
        {
            targetRegistry.Update(currentSettings, isHydrated: false);
        }
    }

    public event Action? Changed;

    public bool IsHydrated { get; private set; }

    public NodeConnectionSettings CurrentSettings => currentSettings.Clone();

    public void Hydrate(NodeConnectionSettings? settings)
    {
        currentSettings = settings?.Clone() ?? currentSettings.Clone();
        currentSettings.Normalize();
        IsHydrated = true;
        targetRegistry.Update(currentSettings, isHydrated: true);
        Changed?.Invoke();
    }

    public void Update(NodeConnectionSettings settings)
    {
        currentSettings = settings?.Clone() ?? new NodeConnectionSettings();
        currentSettings.Normalize();
        IsHydrated = true;
        targetRegistry.Update(currentSettings, isHydrated: true);
        Changed?.Invoke();
    }
}
