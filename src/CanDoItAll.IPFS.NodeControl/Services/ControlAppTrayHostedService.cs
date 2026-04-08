using System.Drawing;
using CanDoItAll.IPFS.DesktopHost;

namespace CanDoItAll.IPFS.NodeControl.Services;

internal sealed class ControlAppTrayHostedService(
    HostedUrlRegistry hostedUrlRegistry,
    LocalNodeBootstrapService localNodeBootstrapService,
    SelfHostControlService selfHostControlService,
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
            new("Restart control app", () => selfHostControlService.RestartAsync(), BeginGroup: true),
            new("Stop control app", () => selfHostControlService.StopAsync()),
            new("Exit control app", () => selfHostControlService.StopAsync())
        ];

    protected override Task OnDoubleClickAsync()
        => OpenControlAppAsync();

    private Task OpenControlAppAsync()
    {
        DesktopAppProcessUtilities.OpenBrowser(hostedUrlRegistry.PreferredUrl);
        return Task.CompletedTask;
    }
}
