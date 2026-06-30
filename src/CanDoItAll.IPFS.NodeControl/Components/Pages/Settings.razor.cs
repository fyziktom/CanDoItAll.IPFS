using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages;

public partial class Settings
{
    private static readonly NodeCapabilityNote FallbackMaintenanceCapability = new(
        "Node maintenance",
        "Partial",
        "warning",
        "Config read/write, repo gc, repo version, and shutdown are supported. Repo verify remains unavailable because the current HTTP surface returns 501.");
    private NodeConnectionSettings editModel = new();
    private int settingsTabIndex;
    private bool isBusy;
    private bool configTreatValueAsJson;
    private string? errorMessage;
    private string? connectionMessage;
    private string configKey = string.Empty;
    private string configValue = string.Empty;
    private string fullConfig = string.Empty;
    private string repoVersion = string.Empty;
    private string configValuePlaceholder = "true or { \"API\": \"/ip4/127.0.0.1/tcp/5009\" }";
    private NodeCapabilityNote maintenanceCapability => MaintenanceWorkflow.GetCapabilityNotes()
        .FirstOrDefault(note => string.Equals(note.Feature, "Node maintenance", StringComparison.Ordinal))
        ?? FallbackMaintenanceCapability;

    protected override void OnInitialized()
    {
        editModel = NodeSessionState.CurrentSettings;
        NodeSessionState.Changed += HandleNodeSessionChanged;
    }

    private Task HandleSettingsTabChanged(int value)
    {
        settingsTabIndex = value;
        return Task.CompletedTask;
    }

    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var normalized = editModel.Clone().Normalize();
            NodeSessionState.Update(normalized);
            editModel = normalized.Clone();
            ServerNodeSettingsStore.Save(normalized);
            await NodeSettingsBrowserStorage.SaveAsync(normalized);
            connectionMessage = $"Saved settings for {normalized.Label}.";
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Settings saved",
                Detail = normalized.BaseUrl
            });
        });
    }

    private async Task ResetSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            editModel = new NodeConnectionSettings();
            NodeSessionState.Update(editModel);
            ServerNodeSettingsStore.Clear();
            await NodeSettingsBrowserStorage.ClearAsync();
            connectionMessage = "Reset to the default local endpoint and cleared the saved browser copy.";
        });
    }

    private async Task TestConnectionAsync()
    {
        await RunBusyAsync(async () =>
        {
            var prior = NodeSessionState.CurrentSettings;
            var candidate = editModel.Clone().Normalize();
            NodeSessionState.Update(candidate);
            try
            {
                var summary = await NodeDashboardService.GetSummaryAsync(CancellationToken.None);
                connectionMessage = $"Connected to {summary.AgentVersion} with peer {summary.PeerId}.";
            }
            finally
            {
                NodeSessionState.Update(prior);
            }
        });
    }

    private async Task LoadFullConfigAsync()
    {
        await RunBusyAsync(async () =>
        {
            fullConfig = await MaintenanceWorkflow.GetFullConfigAsync(CancellationToken.None);
        });
    }

    private async Task LoadConfigValueAsync()
    {
        if (string.IsNullOrWhiteSpace(configKey))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            configValue = await MaintenanceWorkflow.GetConfigValueAsync(configKey.Trim(), CancellationToken.None);
        });
    }

    private async Task SaveConfigValueAsync()
    {
        if (string.IsNullOrWhiteSpace(configKey))
        {
            errorMessage = "A config key is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await MaintenanceWorkflow.SetConfigValueAsync(configKey.Trim(), configValue, configTreatValueAsJson, CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Config updated",
                Detail = configKey.Trim()
            });
        });
    }

    private async Task LoadRepoVersionAsync()
    {
        await RunBusyAsync(async () =>
        {
            repoVersion = await MaintenanceWorkflow.GetRepositoryVersionAsync(CancellationToken.None);
        });
    }

    private async Task RunRepoGcAsync()
    {
        await RunBusyAsync(async () =>
        {
            await MaintenanceWorkflow.RunRepositoryGcAsync(CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = "Repo GC finished",
                Detail = "Garbage collection completed on the connected node."
            });
        });
    }

    private async Task ShutdownNodeAsync()
    {
        await RunBusyAsync(async () =>
        {
            await MaintenanceWorkflow.ShutdownNodeAsync();
            connectionMessage = "Shutdown request sent to the connected node.";
        });
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        errorMessage = null;
        isBusy = true;

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Settings action failed",
                Detail = ex.Message
            });
        }
        finally
        {
            isBusy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task HandleLabelChanged(string? value)
    {
        editModel.Label = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleBaseUrlChanged(string? value)
    {
        editModel.BaseUrl = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleApiPathChanged(string? value)
    {
        editModel.ApiPath = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleTimeoutChanged(int value)
    {
        editModel.TimeoutSeconds = value;
        return Task.CompletedTask;
    }

    private Task HandleConfigKeyChanged(string? value)
    {
        configKey = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleConfigValueChanged(string? value)
    {
        configValue = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleConfigTreatValueAsJsonChanged(bool value)
    {
        configTreatValueAsJson = value;
        return Task.CompletedTask;
    }

    private static string Shorten(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= limit)
        {
            return value;
        }

        return $"{value[..(limit - 3)]}...";
    }

    private void HandleNodeSessionChanged()
    {
        editModel = NodeSessionState.CurrentSettings;
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NodeSessionState.Changed -= HandleNodeSessionChanged;
    }
}

