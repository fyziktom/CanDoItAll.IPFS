using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages;

public partial class Content
{
    private static readonly IReadOnlyList<SelectOption> keyTypeOptions =
    [
        new("RSA", "rsa"),
        new("Ed25519", "ed25519")
    ];

    private IReadOnlyList<NodeKeySnapshot> keys = [];
    private NodeBlockSnapshot? blockSnapshot;
    private NodeObjectSnapshot? objectSnapshot;
    private NodeDagSnapshot? dagSnapshot;
    private NodeNamePublishSnapshot? publishedName;
    private int contentTabIndex;
    private bool isBusy;
    private bool pinNewBlock;
    private bool pinDagWrites = true;
    private string? errorMessage;
    private string blockText = string.Empty;
    private string blockCid = string.Empty;
    private string objectCid = string.Empty;
    private string emptyDirectoryCid = string.Empty;
    private string dagRequest = string.Empty;
    private string dagInputJson = "{\n  \"name\": \"ipfs-engine\"\n}";
    private string resolveName = string.Empty;
    private string resolvedPath = string.Empty;
    private string publishPath = string.Empty;
    private string publishKey = "self";
    private int publishLifetimeHours = 24;
    private string newKeyName = string.Empty;
    private string newKeyType = "rsa";
    private int newKeySize = 2048;
    private string renameKeyFrom = string.Empty;
    private string renameKeyTo = string.Empty;
    private string removeKeyName = string.Empty;
    private bool hasStartedInitialLoad;

    protected override void OnInitialized()
    {
        NodeSessionState.Changed += HandleNodeSessionChanged;
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !NodeSessionState.IsHydrated || hasStartedInitialLoad)
        {
            return Task.CompletedTask;
        }

        hasStartedInitialLoad = true;
        return LoadKeysAsync();
    }

    private Task HandleContentTabChanged(int value)
    {
        contentTabIndex = value;
        return Task.CompletedTask;
    }

    private async Task LoadKeysAsync()
    {
        await RunBusyAsync(async () =>
        {
            keys = await ContentWorkflow.ListKeysAsync(CancellationToken.None);
        });
    }

    private async Task PutBlockAsync()
    {
        if (string.IsNullOrWhiteSpace(blockText))
        {
            errorMessage = "Block text is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            blockCid = await ContentWorkflow.PutBlockTextAsync(blockText, pinNewBlock, CancellationToken.None);
            blockSnapshot = await ContentWorkflow.GetBlockAsync(blockCid, CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Block written",
                Detail = blockCid
            });
        });
    }

    private async Task LoadBlockAsync()
    {
        if (string.IsNullOrWhiteSpace(blockCid))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            blockSnapshot = await ContentWorkflow.GetBlockAsync(blockCid.Trim(), CancellationToken.None);
        });
    }

    private async Task RemoveBlockAsync()
    {
        if (string.IsNullOrWhiteSpace(blockCid))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await ContentWorkflow.RemoveBlockAsync(blockCid.Trim(), ignoreMissing: true, CancellationToken.None);
            blockSnapshot = null;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = "Block removed",
                Detail = blockCid.Trim()
            });
        });
    }

    private async Task LoadObjectAsync()
    {
        if (string.IsNullOrWhiteSpace(objectCid))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            objectSnapshot = await ContentWorkflow.GetObjectAsync(objectCid.Trim(), CancellationToken.None);
        });
    }

    private async Task CreateEmptyDirectoryAsync()
    {
        await RunBusyAsync(async () =>
        {
            emptyDirectoryCid = await ContentWorkflow.CreateEmptyDirectoryAsync(CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Empty directory created",
                Detail = emptyDirectoryCid
            });
        });
    }

    private async Task LoadDagAsync()
    {
        if (string.IsNullOrWhiteSpace(dagRequest))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            dagSnapshot = await ContentWorkflow.GetDagAsync(dagRequest.Trim(), CancellationToken.None);
        });
    }

    private async Task PutDagAsync()
    {
        if (string.IsNullOrWhiteSpace(dagInputJson))
        {
            errorMessage = "DAG JSON is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var cid = await ContentWorkflow.PutDagJsonAsync(dagInputJson, pinDagWrites, CancellationToken.None);
            dagRequest = cid;
            dagSnapshot = await ContentWorkflow.GetDagAsync(cid, CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "DAG stored",
                Detail = cid
            });
        });
    }

    private async Task ResolveNameAsync()
    {
        if (string.IsNullOrWhiteSpace(resolveName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            resolvedPath = await ContentWorkflow.ResolveNameAsync(resolveName.Trim(), recursive: true, CancellationToken.None);
        });
    }

    private async Task PublishNameAsync()
    {
        if (string.IsNullOrWhiteSpace(publishPath))
        {
            errorMessage = "A publish path is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            publishedName = await ContentWorkflow.PublishNameAsync(
                publishPath.Trim(),
                string.IsNullOrWhiteSpace(publishKey) ? "self" : publishKey.Trim(),
                TimeSpan.FromHours(publishLifetimeHours),
                CancellationToken.None);

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "IPNS name published",
                Detail = publishedName.NamePath
            });
        });
    }

    private async Task CreateKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(newKeyName))
        {
            errorMessage = "A key name is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var key = await ContentWorkflow.CreateKeyAsync(newKeyName.Trim(), newKeyType, newKeySize, CancellationToken.None);
            await LoadKeysAsync();
            publishKey = key.Name;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Key created",
                Detail = key.Name
            });
        });
    }

    private async Task RenameKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(renameKeyFrom) || string.IsNullOrWhiteSpace(renameKeyTo))
        {
            errorMessage = "Both rename fields are required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await ContentWorkflow.RenameKeyAsync(renameKeyFrom.Trim(), renameKeyTo.Trim(), CancellationToken.None);
            await LoadKeysAsync();
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Key renamed",
                Detail = $"{renameKeyFrom} -> {renameKeyTo}"
            });
        });
    }

    private async Task RemoveKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(removeKeyName))
        {
            errorMessage = "A key name is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await ContentWorkflow.RemoveKeyAsync(removeKeyName.Trim(), CancellationToken.None);
            await LoadKeysAsync();
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = "Key removed",
                Detail = removeKeyName.Trim()
            });
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
                Summary = "Content action failed",
                Detail = ex.Message
            });
        }
        finally
        {
            isBusy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void HandleNodeSessionChanged()
    {
        if (!NodeSessionState.IsHydrated)
        {
            return;
        }

        hasStartedInitialLoad = true;
        blockSnapshot = null;
        objectSnapshot = null;
        dagSnapshot = null;
        _ = InvokeAsync(LoadKeysAsync);
    }

    public void Dispose()
    {
        NodeSessionState.Changed -= HandleNodeSessionChanged;
    }

    private Task HandleBlockTextChanged(string? value)
    {
        blockText = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleBlockCidChanged(string? value)
    {
        blockCid = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleObjectCidChanged(string? value)
    {
        objectCid = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleEmptyDirectoryCidChanged(string? value)
    {
        emptyDirectoryCid = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleDagRequestChanged(string? value)
    {
        dagRequest = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleDagInputJsonChanged(string? value)
    {
        dagInputJson = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleResolveNameChanged(string? value)
    {
        resolveName = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandlePublishPathChanged(string? value)
    {
        publishPath = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandlePublishKeyChanged(string? value)
    {
        publishKey = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandlePublishLifetimeChanged(int value)
    {
        publishLifetimeHours = value;
        return Task.CompletedTask;
    }

    private Task HandleNewKeyNameChanged(string? value)
    {
        newKeyName = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleNewKeyTypeChanged(string? value)
    {
        newKeyType = string.IsNullOrWhiteSpace(value) ? "rsa" : value;
        return Task.CompletedTask;
    }

    private Task HandleNewKeySizeChanged(int value)
    {
        newKeySize = value;
        return Task.CompletedTask;
    }

    private Task HandleRenameKeyFromChanged(string? value)
    {
        renameKeyFrom = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleRenameKeyToChanged(string? value)
    {
        renameKeyTo = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleRemoveKeyNameChanged(string? value)
    {
        removeKeyName = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandlePinNewBlockChanged(bool value)
    {
        pinNewBlock = value;
        return Task.CompletedTask;
    }

    private Task HandlePinDagWritesChanged(bool value)
    {
        pinDagWrites = value;
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

    private sealed record SelectOption(string Text, string Value);
}

