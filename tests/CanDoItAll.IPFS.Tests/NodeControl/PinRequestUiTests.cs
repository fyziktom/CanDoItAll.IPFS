using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using CanDoItAll.Components.BaseLib;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Components.Layout;
using CanDoItAll.IPFS.NodeControl.Components.Pages;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class PinRequestUiTests
{
    [TestMethod]
    public async Task PinRequests_Renders_Stored_Request_And_Pending_Actions()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var workflow = context.Services.GetRequiredService<RemotePinRequestWorkflowService>();
            var stored = workflow.Enqueue(CreateEnvelope("Backup copy", "Please pin this folder for the remote team."));

            var cut = context.RenderComponent<PinRequests>();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Backup copy");
                StringAssert.Contains(cut.Markup, "Pending");
                StringAssert.Contains(cut.Markup, "Compatibility");
                StringAssert.Contains(cut.Markup, "Details");
                StringAssert.Contains(cut.Markup, "4 KB");
            }, TimeSpan.FromSeconds(10));

            cut.FindAll("button")
                .Single(button => string.Equals(button.TextContent.Trim(), "Details", StringComparison.Ordinal))
                .Click();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Please pin this folder for the remote team.");
                StringAssert.Contains(cut.Markup, stored.SecurityMessage);
                StringAssert.Contains(cut.Markup, "Envelope");
                StringAssert.Contains(cut.Markup, "Accept");
                StringAssert.Contains(cut.Markup, "Reject");
                StringAssert.Contains(cut.Markup, "Direct items");
                StringAssert.Contains(cut.Markup, "Copy address");
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task PinRequests_Filter_Buttons_Reduce_Card_List()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var workflow = context.Services.GetRequiredService<RemotePinRequestWorkflowService>();
            workflow.Enqueue(CreateEnvelope("Pending archive", "Pending request"));
            var store = context.Services.GetRequiredService<RemotePinRequestStore>();
            var accepted = workflow.Enqueue(CreateEnvelope("Accepted archive", "Accepted request"));
            store.Update(accepted.Request.RequestId, item =>
            {
                item.State = RemotePinRequestState.Accepted;
                item.RespondedAtUtc = DateTimeOffset.UtcNow;
            });

            var cut = context.RenderComponent<PinRequests>();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Pending archive");
                StringAssert.Contains(cut.Markup, "Accepted archive");
            }, TimeSpan.FromSeconds(10));

            cut.FindAll("button")
                .Single(button => button.TextContent.Trim().StartsWith("Pending", StringComparison.Ordinal))
                .Click();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Pending archive");
                Assert.IsFalse(cut.Markup.Contains("Accepted archive", StringComparison.Ordinal));
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public async Task MainLayout_Shows_Pending_Pin_Request_Count_In_Navigation()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var tempRoot = CreateTempRoot();

        try
        {
            using var context = CreateContext(host.BaseAddress, tempRoot);
            var workflow = context.Services.GetRequiredService<RemotePinRequestWorkflowService>();
            workflow.Enqueue(CreateEnvelope("Quarterly archive", "Keep a second copy on the backup receiver."));

            var cut = context.RenderComponent<MainLayout>(parameters => parameters
                .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Dashboard</h1>"))));

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Pin requests (1)");
                StringAssert.Contains(cut.Markup, "IPFS Node Control");
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
        context.Services.AddSingleton(Options.Create(new RemotePinRequestStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "remote-pin-requests.json")
        }));
        context.Services.AddSingleton(Options.Create(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = true
        }));
        context.Services.AddSingleton<RemotePinRequestStore>();
        context.Services.AddSingleton<IRemotePinRequestStore>(serviceProvider => serviceProvider.GetRequiredService<RemotePinRequestStore>());
        context.Services.AddSingleton<CurrentNodeTargetRegistry>();
        context.Services.AddSingleton<INodeHostController, DesktopNodeHostController>();
        context.Services.AddSingleton<LocalNodeBootstrapService>();
        context.Services.AddSingleton<CurrentNodeLeaseFactory>();
        context.Services.AddSingleton<INodeConnectionLeaseFactory>(serviceProvider => serviceProvider.GetRequiredService<CurrentNodeLeaseFactory>());
        context.Services.AddSingleton<ConfiguredNodeStatusService>();
        context.Services.AddSingleton<RemotePinRequestSecurityService>();
        context.Services.AddSingleton<RemotePinRequestWorkflowService>();
        context.Services.AddSingleton<NodeSessionState>(_ =>
        {
            var targetRegistry = _.GetRequiredService<CurrentNodeTargetRegistry>();
            var state = new NodeSessionState(
                Options.Create(new NodeConnectionSettings
                {
                    BaseUrl = baseAddress.ToString(),
                    ApiPath = "api/v0",
                    TimeoutSeconds = 15
                }),
                targetRegistry);
            state.Update(new NodeConnectionSettings
            {
                Label = "Receiver node",
                BaseUrl = baseAddress.ToString(),
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            });
            return state;
        });
        context.Services.AddSingleton<NodeSettingsBrowserStorage>();
        return context;
    }

    private static RemotePinRequestEnvelope CreateEnvelope(string displayName, string note)
        => new()
        {
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = note,
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://sender-app/",
                "http://sender-node/",
                "12D3KooWSender",
                ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
            Content = new RemotePinContentSnapshot(
                "/ipfs/bafy-request",
                "bafy-request",
                displayName,
                IsDirectory: true,
                Size: 4096,
                ChildCount: 4)
        };

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pin-request-ui-{Guid.NewGuid():N}");
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
