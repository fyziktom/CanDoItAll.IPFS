using System;
using System.IO;
using System.Threading.Tasks;
using Bunit;
using CanDoItAll.Components.BaseLib;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Components.Pages;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class HomeDashboardComponentTests
{
    [TestMethod]
    public async Task Home_Loads_Dashboard_Summary_For_Healthy_Node()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var peer = await host.Client.Generic.IdAsync().ConfigureAwait(false);

        using var context = CreateContext(new NodeConnectionSettings
        {
            BaseUrl = host.BaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        });

        var cut = context.RenderComponent<Home>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, peer.Id.ToString()[..16]);
            StringAssert.Contains(cut.Markup, "Node identity");
            StringAssert.Contains(cut.Markup, "Storage");
            StringAssert.Contains(cut.Markup, "Network activity");
            StringAssert.Contains(cut.Markup, "Data transfer");
            StringAssert.Contains(cut.Markup, "Connected peers");
            StringAssert.Contains(cut.Markup, "Pinned CIDs");
            StringAssert.Contains(cut.Markup, "Active peers");
            Assert.IsFalse(cut.Markup.Contains("Capability posture", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Single file upload", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Repo verify remains unavailable", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("The IPFS API returned HTTP", StringComparison.Ordinal));
        }, TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task Home_Shows_Warning_When_Dashboard_Summary_Fails()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);

        using var context = CreateContext(new NodeConnectionSettings
        {
            BaseUrl = host.BaseAddress.ToString(),
            ApiPath = "api/v0-missing",
            TimeoutSeconds = 15
        });

        var cut = context.RenderComponent<Home>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Unable to load the node dashboard. Check the selected endpoint and try again.");
            Assert.IsFalse(cut.Markup.Contains("The IPFS API returned HTTP 404", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Storage", StringComparison.Ordinal));
        }, TimeSpan.FromSeconds(10));
    }

    private static Bunit.TestContext CreateContext(NodeConnectionSettings settings)
    {
        var context = new Bunit.TestContext();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"home-dashboard-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
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
        context.Services.AddSingleton<INodeHostController, NonStartingNodeHostController>();
        context.Services.AddSingleton<LocalNodeBootstrapService>();
        context.Services.AddSingleton<CurrentNodeLeaseFactory>();
        context.Services.AddSingleton<INodeConnectionLeaseFactory>(serviceProvider => serviceProvider.GetRequiredService<CurrentNodeLeaseFactory>());
        context.Services.AddSingleton<NodeSessionState>(_ =>
        {
            var targetRegistry = _.GetRequiredService<CurrentNodeTargetRegistry>();
            var state = new NodeSessionState(
                Microsoft.Extensions.Options.Options.Create(new NodeConnectionSettings()),
                targetRegistry);
            state.Update(settings);
            return state;
        });
        context.Services.AddSingleton<IpfsClientFactory>();
        context.Services.AddSingleton<NodeDashboardService>();
        context.Services.AddSingleton<NodeFileWorkflowService>();
        context.Services.AddSingleton<INodeFileWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeFileWorkflowService>());
        context.Services.AddSingleton<NodeExplorerWorkflowService>();
        context.Services.AddSingleton<INodeExplorerWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeExplorerWorkflowService>());
        context.Services.AddSingleton<NodeContentWorkflowService>();
        context.Services.AddSingleton<INodeContentWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeContentWorkflowService>());
        context.Services.AddSingleton<NodeNetworkWorkflowService>();
        context.Services.AddSingleton<INodeNetworkWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeNetworkWorkflowService>());
        context.Services.AddSingleton<NodeMaintenanceWorkflowService>();
        context.Services.AddSingleton<INodeMaintenanceWorkflow>(serviceProvider => serviceProvider.GetRequiredService<NodeMaintenanceWorkflowService>());
        context.Services.AddSingleton<NodeOperatorService>();
        return context;
    }
}
