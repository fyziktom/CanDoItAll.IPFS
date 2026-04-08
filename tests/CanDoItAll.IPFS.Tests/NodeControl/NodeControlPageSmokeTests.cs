using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using CanDoItAll.Components.BaseLib;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Components.Pages;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeControlPageSmokeTests
{
    [TestMethod]
    public async Task Settings_Page_Renders_Node_And_Maintenance_Workflows()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var cut = context.RenderComponent<Settings>();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Endpoint and maintenance");
                StringAssert.Contains(cut.Markup, "Config read and write");
                StringAssert.Contains(cut.Markup, host.BaseAddress.ToString());
                StringAssert.Contains(cut.Markup, "Repo and node maintenance");
                StringAssert.Contains(cut.Markup, "Repo verify is intentionally omitted here because the current engine HTTP surface still returns HTTP 501 for verify requests.");
                Assert.IsFalse(cut.FindAll("button").Any(button => string.Equals(button.TextContent.Trim(), "Verify repo", StringComparison.Ordinal)));
            }, TimeSpan.FromSeconds(10));

            cut.FindAll("button")
                .Single(button => string.Equals(button.TextContent.Trim(), "Test connection", StringComparison.Ordinal))
                .Click();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Connected to");
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task Network_Page_Loads_Workbench_And_Network_Sections()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var cut = context.RenderComponent<Network>();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Swarm and bootstrap");
                StringAssert.Contains(cut.Markup, "Known node host or API URL");
                StringAssert.Contains(cut.Markup, "Resolve and connect");
                StringAssert.Contains(cut.Markup, "DHT lookups");
                StringAssert.Contains(cut.Markup, "PubSub publish and peers");
                StringAssert.Contains(cut.Markup, "No connected peers");
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task Content_Page_Loads_Key_And_Content_Sections()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var cut = context.RenderComponent<Content>();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Block store");
                StringAssert.Contains(cut.Markup, "DAG JSON");
                StringAssert.Contains(cut.Markup, "IPNS");
                StringAssert.Contains(cut.Markup, "self");
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
        context.Services.AddSingleton(Options.Create(new ServerNodeSettingsStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "current-node-settings.json")
        }));
        context.Services.AddSingleton<ExplorerIndexStore>();
        context.Services.AddSingleton<IExplorerIndexStore>(serviceProvider => serviceProvider.GetRequiredService<ExplorerIndexStore>());
        context.Services.AddSingleton<ServerNodeSettingsStore>();
        context.Services.AddSingleton<IServerNodeSettingsStore>(serviceProvider => serviceProvider.GetRequiredService<ServerNodeSettingsStore>());
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
                Label = "Local node",
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
        context.Services.AddScoped<NodeDashboardService>();
        context.Services.AddScoped<NodeSettingsBrowserStorage>();
        context.Services.AddScoped<KnownRemotePinTargetBrowserStorage>();
        context.Services.AddScoped<NodeCanvasSurfaceFactory>();
        context.Services.AddScoped<NodeOperatorService>();
        context.Services.AddScoped<RemotePinShareService>();
        return context;
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"node-control-page-smoke-{Guid.NewGuid():N}");
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
            // Best-effort cleanup only.
        }
    }
}
