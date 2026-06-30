using System.Globalization;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.NodeControl.Components.Pages.FilesComponents;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages;

public partial class RemotePinShareModal
{
    [Parameter]
    public NodeExplorerItemSnapshot? TargetItem { get; set; }

    [Parameter]
    public EventCallback OnClosed { get; set; }

    private IReadOnlyList<KnownRemotePinTarget> knownTargets = [];
    private RemotePinReceiverProbeSnapshot? probeSnapshot;
    private string? currentTargetKey;
    private string? errorMessage;
    private string? resultMessage;
    private string? selectedTargetId;
    private string newTargetLabel = string.Empty;
    private string newTargetUrl = string.Empty;
    private string noteText = string.Empty;
    private string resultTone = "info";
    private bool hasLoadedKnownTargets;
    private bool isBusy;
    private int shareStep = 1;
    private RemotePinOutgoingSecuritySnapshot outgoingSecurity = new(
        CanCreateRequests: true,
        UsesCompatibilityMode: true,
        EnvelopeVersion: 1,
        Summary: "Compatibility mode",
        Detail: "Requests will be exported as unsigned legacy envelopes and require receiver-side compatibility mode.",
        KeyId: null,
        RequestExpiryMinutes: 15);

    private KnownRemotePinTarget? SelectedTarget
        => knownTargets.FirstOrDefault(target => string.Equals(target.Id, selectedTargetId, StringComparison.Ordinal));

    protected override void OnParametersSet()
    {
        var targetKey = TargetItem is null ? null : $"{TargetItem.Path}|{TargetItem.Target}";
        if (string.Equals(targetKey, currentTargetKey, StringComparison.Ordinal))
        {
            return;
        }

        currentTargetKey = targetKey;
        shareStep = 1;
        probeSnapshot = null;
        errorMessage = null;
        resultMessage = null;
        resultTone = "info";
        noteText = string.Empty;
        outgoingSecurity = RemotePinShareService.DescribeOutgoingSecurity();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (TargetItem is null || hasLoadedKnownTargets)
        {
            return;
        }

        knownTargets = await KnownRemotePinTargetBrowserStorage.LoadAsync();
        hasLoadedKnownTargets = true;
        if (SelectedTarget is null && knownTargets.Count > 0)
        {
            selectedTargetId = knownTargets[0].Id;
        }

        await InvokeAsync(StateHasChanged);
    }

    private bool IsSelectedTarget(KnownRemotePinTarget target)
        => string.Equals(target.Id, selectedTargetId, StringComparison.Ordinal);

    private void SelectTarget(string targetId)
    {
        selectedTargetId = targetId;
        errorMessage = null;
    }

    private Task HandleNewTargetLabelChanged(string? value)
    {
        newTargetLabel = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleNewTargetUrlChanged(string? value)
    {
        newTargetUrl = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleNoteTextChanged(string? value)
    {
        noteText = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private async Task AddKnownTargetAsync()
    {
        errorMessage = null;

        try
        {
            var normalizedUrl = RemotePinShareService.NormalizeControlAppUrl(newTargetUrl);
            var createdTarget = new KnownRemotePinTarget
            {
                Id = Guid.NewGuid().ToString("N"),
                Label = string.IsNullOrWhiteSpace(newTargetLabel) ? "Remote receiver" : newTargetLabel.Trim(),
                ControlAppUrl = normalizedUrl
            };

            var updatedTargets = knownTargets
                .Where(target => !string.Equals(target.ControlAppUrl, createdTarget.ControlAppUrl, StringComparison.OrdinalIgnoreCase))
                .Append(createdTarget)
                .OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(target => target.ControlAppUrl, StringComparer.OrdinalIgnoreCase)
                .ToList();

            knownTargets = updatedTargets;
            selectedTargetId = createdTarget.Id;
            newTargetLabel = string.Empty;
            newTargetUrl = string.Empty;
            await KnownRemotePinTargetBrowserStorage.SaveAsync(knownTargets);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
    }

    private async Task BeginProbeAsync()
    {
        if (SelectedTarget is null)
        {
            errorMessage = "Choose a receiver before probing.";
            return;
        }

        shareStep = 2;
        errorMessage = null;
        resultMessage = null;
        probeSnapshot = null;
        isBusy = true;

        try
        {
            var probe = await RemotePinShareService.ProbeAsync(SelectedTarget.ControlAppUrl, CancellationToken.None);
            probeSnapshot = probe;
            if (!probe.NodeHealthy)
            {
                await RememberProbeFailureAsync(SelectedTarget, probe.DiagnosticMessage ?? "The receiver node is not ready.");
                errorMessage = probe.DiagnosticMessage ?? "The selected receiver did not report a healthy node.";
                shareStep = 1;
                return;
            }

            await RememberProbeSuccessAsync(SelectedTarget, probe);
        }
        catch (Exception ex)
        {
            await RememberProbeFailureAsync(SelectedTarget, ex.Message);
            errorMessage = ex.Message;
            shareStep = 1;
        }
        finally
        {
            isBusy = false;
        }
    }

    private void BackToSelection()
        => shareStep = 1;

    private void ContinueToCompose()
        => shareStep = 3;

    private void BackToProbe()
        => shareStep = 2;

    private async Task SendLiveRequestAsync()
    {
        if (TargetItem is null || SelectedTarget is null)
        {
            errorMessage = "A target item and receiver are required to send a request.";
            return;
        }

        if (!EnsureOutgoingRequestsAllowed())
        {
            return;
        }

        errorMessage = null;
        resultMessage = null;
        isBusy = true;

        try
        {
            var envelope = await BuildEnvelopeAsync(TargetItem).ConfigureAwait(false);
            var stored = await RemotePinShareService.SendAsync(SelectedTarget.ControlAppUrl, envelope, CancellationToken.None).ConfigureAwait(false);
            resultTone = "success";
            resultMessage = $"{GetEnvelopeModeLabel(envelope)} request {stored.Request.RequestId} is pending on {SelectedTarget.Label}. The receiver will see it in the pin-request inbox.";

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Pin request sent",
                Detail = stored.Request.Content.DisplayName
            });
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Pin request failed",
                Detail = ex.Message
            });
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task ExportOfflineAsync()
    {
        if (TargetItem is null)
        {
            errorMessage = "Choose a file or folder before exporting a request.";
            return;
        }

        if (!EnsureOutgoingRequestsAllowed())
        {
            return;
        }

        errorMessage = null;
        resultMessage = null;
        isBusy = true;

        try
        {
            var envelope = await BuildEnvelopeAsync(TargetItem).ConfigureAwait(false);
            var fileName = BuildExportFileName(TargetItem);
            await JSRuntime.InvokeVoidAsync(
                "filesExplorer.downloadTextFile",
                fileName,
                RemotePinRequestContractSerializer.Serialize(envelope),
                "application/json");

            resultTone = "info";
            resultMessage = $"Downloaded {fileName}. It contains a {GetEnvelopeModeLabel(envelope).ToLowerInvariant()} request that the receiver can import into the same inbox if live delivery is unavailable.";

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Request exported",
                Detail = fileName
            });
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Export failed",
                Detail = ex.Message
            });
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task CloseAsync()
    {
        errorMessage = null;
        resultMessage = null;
        probeSnapshot = null;
        noteText = string.Empty;
        shareStep = 1;
        await OnClosed.InvokeAsync();
    }

    private async Task<RemotePinRequestEnvelope> BuildEnvelopeAsync(NodeExplorerItemSnapshot targetItem)
        => await RemotePinShareService.CreateEnvelopeAsync(
            new RemotePinContentSnapshot(
                targetItem.Path,
                targetItem.Target,
                targetItem.DisplayName,
                targetItem.IsDirectory,
                targetItem.Size,
                targetItem.ChildCount),
            noteText,
            CancellationToken.None).ConfigureAwait(false);

    private async Task RememberProbeSuccessAsync(KnownRemotePinTarget selectedTarget, RemotePinReceiverProbeSnapshot probe)
    {
        selectedTarget.LastKnownNodeLabel = probe.NodeLabel;
        selectedTarget.LastKnownPeerId = probe.PeerId;
        selectedTarget.LastProbeSucceededAtUtc = DateTimeOffset.UtcNow;
        selectedTarget.LastFailureMessage = null;
        await KnownRemotePinTargetBrowserStorage.SaveAsync(knownTargets);
    }

    private async Task RememberProbeFailureAsync(KnownRemotePinTarget selectedTarget, string message)
    {
        selectedTarget.LastFailureMessage = message;
        await KnownRemotePinTargetBrowserStorage.SaveAsync(knownTargets);
    }

    private bool EnsureOutgoingRequestsAllowed()
    {
        if (outgoingSecurity.CanCreateRequests)
        {
            return true;
        }

        errorMessage = outgoingSecurity.Detail;
        return false;
    }

    private string GetStepCssClass(int step)
    {
        if (shareStep == step)
        {
            return "is-current";
        }

        return shareStep > step ? "is-complete" : string.Empty;
    }

    private string GetResultTitle()
        => resultTone switch
        {
            "success" => "Live request delivered",
            "error" => "Request failed",
            _ => "Offline request ready"
        };

    private AlertStyle ResolveResultAlertStyle()
        => resultTone switch
        {
            "success" => AlertStyle.Success,
            "error" => AlertStyle.Danger,
            _ => AlertStyle.Info
        };

    private AlertStyle ResolveOutgoingSecurityAlertStyle()
        => outgoingSecurity.CanCreateRequests
            ? outgoingSecurity.UsesCompatibilityMode
                ? AlertStyle.Warning
                : AlertStyle.Success
            : AlertStyle.Danger;

    private static string GetContentSummary(NodeExplorerItemSnapshot targetItem)
    {
        if (targetItem.IsDirectory)
        {
            return $"{targetItem.ChildCount} item{(targetItem.ChildCount == 1 ? string.Empty : "s")} currently visible";
        }

        return FormatSize(targetItem.Size);
    }

    private static string BuildExportFileName(NodeExplorerItemSnapshot targetItem)
    {
        var safeName = string.Join(
            "-",
            (targetItem.DisplayName ?? "pin-request")
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "pin-request";
        }

        return $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.remote-pin.json";
    }

    private static string GetEnvelopeModeLabel(RemotePinRequestEnvelope envelope)
        => envelope.Version >= RemotePinRequestSecurityService.SignedEnvelopeVersion
            ? "Signed"
            : "Compatibility";

    private static string Shorten(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= limit)
        {
            return value;
        }

        return $"{value[..(limit - 3)]}...";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes:n0} B";
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var scaled = bytes / Math.Pow(1024, unitIndex);
        return string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", scaled, units[unitIndex]);
    }
}


