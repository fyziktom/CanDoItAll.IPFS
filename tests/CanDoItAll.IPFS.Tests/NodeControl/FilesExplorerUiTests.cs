using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using CanDoItAll.Components.BaseLib;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Components.Pages;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class FilesExplorerUiTests
{
    [TestMethod]
    public async Task Files_Renders_Folders_At_Root_And_Unsorted_Branch_For_Pinned_Files()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var nodeOperatorService = context.Services.GetRequiredService<NodeOperatorService>();
            var seedPath = Path.Combine(tempRoot, "bundle-seed.txt");
            await File.WriteAllTextAsync(seedPath, "bundle ui seed").ConfigureAwait(false);
            await nodeOperatorService.UploadLocalFileAsync(seedPath, pin: true, wrap: false, default).ConfigureAwait(false);
            await nodeOperatorService.UploadTextAsync("wrapped.txt", "wrapped folder seed", pin: true, wrap: true, default).ConfigureAwait(false);

            var cut = context.RenderComponent<Files>();
            var currentUtc = DateTimeOffset.UtcNow;
            var currentYear = currentUtc.Year.ToString(CultureInfo.InvariantCulture);
            var currentMonth = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(currentUtc.Month);

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "UNSORTED");
                StringAssert.Contains(cut.Markup, "wrapped.txt");
                StringAssert.Contains(cut.Markup, "Pinned files and folders");
            }, TimeSpan.FromSeconds(10));

            cut.WaitForAssertion(() =>
            {
                var rootCards = cut.FindAll("button.fx-card-button");
                Assert.AreEqual(2, rootCards.Count);
                Assert.IsTrue(rootCards.Any(card => card.TextContent.Contains("UNSORTED", StringComparison.Ordinal)));
                Assert.IsTrue(rootCards.Any(card => card.TextContent.Contains("wrapped.txt", StringComparison.Ordinal)));
                Assert.IsFalse(rootCards.Any(card => card.TextContent.Contains("bundle-seed.txt", StringComparison.Ordinal)));
            }, TimeSpan.FromSeconds(10));

            await cut.InvokeAsync(() =>
                cut.FindAll("button.fx-card-button")
                    .Single(card => card.TextContent.Contains("UNSORTED", StringComparison.Ordinal))
                    .TriggerEvent("ondblclick", new MouseEventArgs())).ConfigureAwait(false);

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, currentYear);
            }, TimeSpan.FromSeconds(10));

            await cut.InvokeAsync(() =>
                cut.FindAll("button.fx-card-button")
                    .Single(card => card.TextContent.Contains(currentYear, StringComparison.Ordinal))
                    .TriggerEvent("ondblclick", new MouseEventArgs())).ConfigureAwait(false);

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, currentMonth);
            }, TimeSpan.FromSeconds(10));

            await cut.InvokeAsync(() =>
                cut.FindAll("button.fx-card-button")
                    .Single(card => card.TextContent.Contains(currentMonth, StringComparison.Ordinal))
                    .TriggerEvent("ondblclick", new MouseEventArgs())).ConfigureAwait(false);

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "bundle-seed.txt");
                Assert.IsFalse(cut.FindAll("button.fx-card-button")
                    .Any(card => card.TextContent.Contains("wrapped.txt", StringComparison.Ordinal)));
            }, TimeSpan.FromSeconds(10));

            await cut.InvokeAsync(() =>
                cut.FindAll("button.fx-card-button")
                    .Single(card => card.TextContent.Contains("bundle-seed.txt", StringComparison.Ordinal))
                    .Click()).ConfigureAwait(false);

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Download");
                StringAssert.Contains(cut.Markup, "Show preview");
            }, TimeSpan.FromSeconds(10));

            await cut.InvokeAsync(() =>
                cut.FindAll("button.fx-card-button")
                    .Single(card => card.TextContent.Contains("bundle-seed.txt", StringComparison.Ordinal))
                    .TriggerEvent("oncontextmenu", new MouseEventArgs
                    {
                        ClientX = 48,
                        ClientY = 64
                    })).ConfigureAwait(false);

            cut.WaitForAssertion(() =>
            {
                Assert.IsTrue(cut.FindAll(".fx-context-menu button")
                    .Any(button => string.Equals(button.TextContent.Trim(), "Unpin", StringComparison.Ordinal)));
            }, TimeSpan.FromSeconds(10));

            await cut.InvokeAsync(() =>
                cut.FindAll(".fx-context-menu button")
                    .Single(button => string.Equals(button.TextContent.Trim(), "Unpin", StringComparison.Ordinal))
                    .Click()).ConfigureAwait(false);

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Delete immediately");
                StringAssert.Contains(cut.Markup, "Remove bundle-seed.txt");
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task Files_Does_Not_Render_Stale_Cached_Roots_For_A_Fresh_Node()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            await using (var staleHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false))
            {
                using var staleContext = CreateContext(staleHost.BaseAddress, tempRoot);
                var staleService = staleContext.Services.GetRequiredService<NodeOperatorService>();
                await staleService.UploadTextAsync("ghost.txt", "ghost body", pin: true, wrap: true, default).ConfigureAwait(false);
                await staleService.ListPinnedExplorerItemsAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await using var freshHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
            using var freshContext = CreateContext(freshHost.BaseAddress, tempRoot);

            var cut = freshContext.RenderComponent<Files>();

            cut.WaitForAssertion(() =>
            {
                Assert.IsFalse(cut.Markup.Contains("ghost.txt", StringComparison.Ordinal));
                Assert.AreEqual(0, cut.FindAll("button.fx-card-button").Count);
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static Bunit.TestContext CreateContext(Uri baseAddress, string tempRoot)
    {
        var context = new Bunit.TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<NotificationService>();
        context.Services.AddSingleton(Options.Create(new ExplorerIndexStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "explorer.db")
        }));
        context.Services.AddSingleton<ExplorerIndexStore>();
        context.Services.AddSingleton<IExplorerIndexStore>(serviceProvider => serviceProvider.GetRequiredService<ExplorerIndexStore>());
        context.Services.AddSingleton<CurrentNodeTargetRegistry>();
        context.Services.AddSingleton<HostedUrlRegistry>();
        context.Services.AddSingleton<INodeHostController, DesktopNodeHostController>();
        context.Services.AddSingleton<LocalNodeBootstrapService>();
        context.Services.AddSingleton<CurrentNodeLeaseFactory>();
        context.Services.AddSingleton<INodeConnectionLeaseFactory>(serviceProvider => serviceProvider.GetRequiredService<CurrentNodeLeaseFactory>());
        context.Services.AddSingleton<ConfiguredNodeStatusService>();
        context.Services.AddSingleton(Options.Create(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = true
        }));
        context.Services.AddSingleton<RemotePinRequestSecurityService>();
        context.Services.AddScoped(_ =>
        {
            var settings = new NodeConnectionSettings
            {
                Label = "Local sender node",
                BaseUrl = baseAddress.ToString(),
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            };

            var targetRegistry = _.GetRequiredService<CurrentNodeTargetRegistry>();
            var state = new NodeSessionState(Options.Create(settings), targetRegistry);
            state.Update(settings);
            return state;
        });
        context.Services.AddScoped<IpfsClientFactory>();
        context.Services.AddScoped<NodeFileWorkflowService>();
        context.Services.AddScoped<INodeFileWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeFileWorkflowService>());
        context.Services.AddScoped<NodeExplorerWorkflowService>();
        context.Services.AddScoped<INodeExplorerWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeExplorerWorkflowService>());
        context.Services.AddScoped<NodeContentWorkflowService>();
        context.Services.AddScoped<INodeContentWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeContentWorkflowService>());
        context.Services.AddScoped<NodeNetworkWorkflowService>();
        context.Services.AddScoped<INodeNetworkWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeNetworkWorkflowService>());
        context.Services.AddScoped<NodeMaintenanceWorkflowService>();
        context.Services.AddScoped<INodeMaintenanceWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeMaintenanceWorkflowService>());
        context.Services.AddScoped<NodeCanvasSurfaceFactory>();
        context.Services.AddScoped<NodeOperatorService>();
        context.Services.AddScoped<KnownRemotePinTargetBrowserStorage>();
        context.Services.AddScoped<RemotePinShareService>();
        return context;
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"files-explorer-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore transient cleanup failures from test temp roots.
        }
    }
}
