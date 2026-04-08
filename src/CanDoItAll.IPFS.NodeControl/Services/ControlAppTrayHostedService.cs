using System.Drawing;
using CanDoItAll.IPFS.DesktopHost;

namespace CanDoItAll.IPFS.NodeControl.Services;

internal sealed class ControlAppTrayHostedService(
    HostedUrlRegistry hostedUrlRegistry,
    LocalNodeBootstrapService localNodeBootstrapService,
    SelfHostControlService selfHostControlService,
    WindowsStartupRegistrationService startupRegistrationService,
    ILogger<ControlAppTrayHostedService> logger) : WindowsTrayHostedService(logger)
{
    protected override string TrayText => "IPFS Control App";

    protected override Icon TrayIcon => SystemIcons.Application;

    protected override IReadOnlyList<TrayMenuCommand> BuildMenuCommands()
        =>
        [
            new("Open control app", OpenControlAppAsync),
            new("Start local node", () => localNodeBootstrapService.StartLocalNodeAsync()),
            new("Restart local node", () => localNodeBootstrapService.RestartLocalNodeAsync()),
            new("Stop local node", () => localNodeBootstrapService.StopLocalNodeAsync()),
            new("Enable start after sign-in", EnableStartAfterSignInAsync, BeginGroup: true),
            new("Disable start after sign-in", DisableStartAfterSignInAsync),
            new("Restart control app", () => selfHostControlService.RestartAsync(), BeginGroup: true),
            new("Exit control app", () => selfHostControlService.StopAsync()),
            new("Exit node and control app", ExitNodeAndControlAppAsync)
        ];

    protected override Task OnDoubleClickAsync()
        => OpenControlAppAsync();

    private Task OpenControlAppAsync()
    {
        DesktopAppProcessUtilities.OpenBrowser(hostedUrlRegistry.PreferredUrl);
        return Task.CompletedTask;
    }

    private Task EnableStartAfterSignInAsync()
    {
        startupRegistrationService.Enable();
        logger.LogInformation("Enabled Windows startup registration for the IPFS control app.");
        return Task.CompletedTask;
    }

    private Task DisableStartAfterSignInAsync()
    {
        startupRegistrationService.Disable();
        logger.LogInformation("Disabled Windows startup registration for the IPFS control app.");
        return Task.CompletedTask;
    }

    private async Task ExitNodeAndControlAppAsync()
    {
        try
        {
            await localNodeBootstrapService.StopLocalNodeAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(ex, "Skipping local-node shutdown while exiting because the current target is not local.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The control app is exiting even though the local node did not stop cleanly.");
        }

        await selfHostControlService.StopAsync().ConfigureAwait(false);
    }
}
