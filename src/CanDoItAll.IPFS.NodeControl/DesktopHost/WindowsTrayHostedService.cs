using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.IPFS.DesktopHost;

internal sealed record TrayMenuCommand(string Text, Func<Task> Action, bool BeginGroup = false);

internal abstract class WindowsTrayHostedService(ILogger logger) : IHostedService, IDisposable
{
    private readonly TaskCompletionSource<bool> trayReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? trayThread;
    private SynchronizationContext? traySynchronizationContext;
    private TrayApplicationContext? trayContext;

    protected virtual bool IsTrayEnabled =>
        OperatingSystem.IsWindows() &&
        Environment.UserInteractive &&
        !string.Equals(Environment.GetEnvironmentVariable("IPFS_DISABLE_TRAY"), "1", StringComparison.Ordinal);

    protected abstract string TrayText { get; }

    protected abstract Icon TrayIcon { get; }

    protected abstract IReadOnlyList<TrayMenuCommand> BuildMenuCommands();

    protected virtual Task OnDoubleClickAsync() => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsTrayEnabled)
        {
            return Task.CompletedTask;
        }

        trayThread = new Thread(RunTrayLoop)
        {
            IsBackground = true,
            Name = $"{GetType().Name}-Tray"
        };
        trayThread.SetApartmentState(ApartmentState.STA);
        trayThread.Start();
        return trayReady.Task;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (trayContext is null || traySynchronizationContext is null)
        {
            return Task.CompletedTask;
        }

        var stopRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        traySynchronizationContext.Post(_ =>
        {
            try
            {
                trayContext.ExitThread();
                stopRequested.TrySetResult(true);
            }
            catch (Exception ex)
            {
                stopRequested.TrySetException(ex);
            }
        }, null);

        return stopRequested.Task;
    }

    public void Dispose()
    {
        trayContext?.Dispose();
    }

    private void RunTrayLoop()
    {
        try
        {
            traySynchronizationContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(traySynchronizationContext);

            trayContext = new TrayApplicationContext(
                TrayText,
                (Icon)TrayIcon.Clone(),
                BuildMenuCommands(),
                OnDoubleClickAsync,
                logger);

            trayReady.TrySetResult(true);
            Application.Run(trayContext);
        }
        catch (Exception ex)
        {
            trayReady.TrySetException(ex);
            logger.LogError(ex, "Failed to initialize the Windows tray host for {TrayHostType}.", GetType().FullName);
        }
    }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly ILogger logger;
        private readonly ContextMenuStrip menu;
        private readonly NotifyIcon notifyIcon;

        public TrayApplicationContext(
            string trayText,
            Icon trayIcon,
            IReadOnlyList<TrayMenuCommand> commands,
            Func<Task> onDoubleClick,
            ILogger logger)
        {
            this.logger = logger;
            menu = BuildMenu(commands, logger);
            notifyIcon = new NotifyIcon
            {
                Text = trayText.Length > 63 ? trayText[..63] : trayText,
                Icon = trayIcon,
                Visible = true,
                ContextMenuStrip = menu
            };

            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.DoubleClick += async (_, _) => await ExecuteActionAsync(onDoubleClick).ConfigureAwait(true);

            var commandList = string.Join(", ", commands.Select(command => command.Text));
            var startupMessage = $"Tray icon '{notifyIcon.Text}' is visible with menu commands: {commandList}.";
            logger.LogInformation(startupMessage);
            Console.WriteLine(startupMessage);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                menu.Dispose();
            }

            base.Dispose(disposing);
        }

        private static ContextMenuStrip BuildMenu(IReadOnlyList<TrayMenuCommand> commands, ILogger logger)
        {
            var menu = new ContextMenuStrip();
            foreach (var command in commands)
            {
                if (command.BeginGroup && menu.Items.Count > 0)
                {
                    menu.Items.Add(new ToolStripSeparator());
                }

                var item = new ToolStripMenuItem(command.Text);
                item.Click += async (_, _) =>
                {
                    try
                    {
                        await command.Action().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Tray action '{TrayAction}' failed.", command.Text);
                        MessageBox.Show(
                            ex.Message,
                            "Tray action failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                };

                menu.Items.Add(item);
            }

            return menu;
        }

        private async Task ExecuteActionAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Tray double-click action failed.");
                MessageBox.Show(
                    ex.Message,
                    "Tray action failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
