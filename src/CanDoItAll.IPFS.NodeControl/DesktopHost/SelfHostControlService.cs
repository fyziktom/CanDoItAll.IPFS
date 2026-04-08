using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.IPFS.DesktopHost;

internal sealed class SelfHostControlService(
    IHostApplicationLifetime applicationLifetime,
    ILogger<SelfHostControlService> logger)
{
    public Task StopAsync()
    {
        applicationLifetime.StopApplication();
        return Task.CompletedTask;
    }

    public Task RestartAsync()
    {
        var restartedProcess = DesktopAppProcessUtilities.StartCurrentProcessClone();
        if (restartedProcess is null)
        {
            throw new InvalidOperationException("Could not restart the current host process.");
        }

        logger.LogInformation("Started replacement process {ProcessId} before shutting down the current host.", restartedProcess.Id);
        applicationLifetime.StopApplication();
        return Task.CompletedTask;
    }
}
