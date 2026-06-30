#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeControlCompositionTests
{
    [TestMethod]
    public void AddIpfsNodeControlApplication_Resolves_Compatibility_Service_Graph_And_Interface_Aliases()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{nameof(NodeControlCompositionTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            using var provider = CreateProvider(tempRoot);
            using var scope = provider.CreateScope();

            var services = scope.ServiceProvider;

            Assert.AreSame(
                services.GetRequiredService<ServerNodeSettingsStore>(),
                services.GetRequiredService<IServerNodeSettingsStore>());
            Assert.AreSame(
                services.GetRequiredService<RemotePinRequestStore>(),
                services.GetRequiredService<IRemotePinRequestStore>());
            Assert.AreSame(
                services.GetRequiredService<ApplicationLogStore>(),
                services.GetRequiredService<IApplicationLogStore>());
            Assert.AreSame(
                services.GetRequiredService<ExplorerIndexStore>(),
                services.GetRequiredService<IExplorerIndexStore>());
            Assert.AreSame(
                services.GetRequiredService<CurrentNodeLeaseFactory>(),
                services.GetRequiredService<INodeConnectionLeaseFactory>());
            Assert.AreSame(
                services.GetRequiredService<CurrentNodeLeaseFactory>(),
                services.GetRequiredService<INodeConnectionDriver>());

            Assert.IsNotNull(services.GetRequiredService<INodeHostController>());
            Assert.IsNotNull(services.GetRequiredService<LocalNodeBootstrapService>());
            Assert.IsNotNull(services.GetRequiredService<ConfiguredNodeStatusService>());
            Assert.IsNotNull(services.GetRequiredService<NodeGatewayService>());
            Assert.IsNotNull(services.GetRequiredService<RemotePinRequestWorkflowService>());
            Assert.IsNotNull(services.GetRequiredService<NodeSessionState>());
            Assert.IsNotNull(services.GetRequiredService<IpfsClientFactory>());
            Assert.AreSame(
                services.GetRequiredService<NodeFileWorkflowService>(),
                services.GetRequiredService<INodeFileWorkflow>());
            Assert.AreSame(
                services.GetRequiredService<NodeExplorerWorkflowService>(),
                services.GetRequiredService<INodeExplorerWorkflow>());
            Assert.AreSame(
                services.GetRequiredService<NodeContentWorkflowService>(),
                services.GetRequiredService<INodeContentWorkflow>());
            Assert.AreSame(
                services.GetRequiredService<NodeNetworkWorkflowService>(),
                services.GetRequiredService<INodeNetworkWorkflow>());
            Assert.AreSame(
                services.GetRequiredService<NodeMaintenanceWorkflowService>(),
                services.GetRequiredService<INodeMaintenanceWorkflow>());
            Assert.AreSame(
                services.GetRequiredService<NodeOperatorService>(),
                services.GetRequiredService<INodeOperator>());
            Assert.IsNotNull(services.GetRequiredService<RemotePinShareService>());
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [TestMethod]
    public void AddIpfsNodeControlApplication_Binds_Persistence_Store_FilePaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{nameof(NodeControlCompositionTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsPath = Path.Combine(tempRoot, "settings", "current-node-settings.json");
            var requestsPath = Path.Combine(tempRoot, "remote-pin", "remote-pin-requests.json");
            var logPath = Path.Combine(tempRoot, "logs", "application.log");
            var explorerPath = Path.Combine(tempRoot, "explorer", "explorer.db");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OperatingProfile:Mode"] = "Light",
                    ["NodeSettingsDefaults:BaseUrl"] = "http://ipfs-node:5001/",
                    ["NodeSettingsDefaults:ApiPath"] = "api/v0",
                    ["NodeSettingsDefaults:TimeoutSeconds"] = "120",
                    ["ServerNodeSettingsStore:FilePath"] = settingsPath,
                    ["RemotePinRequestStore:FilePath"] = requestsPath,
                    ["ApplicationLogStore:FilePath"] = logPath,
                    ["ExplorerIndexStore:FilePath"] = explorerPath
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddIpfsNodeControlApplication(configuration);

            using var provider = services.BuildServiceProvider();

            Assert.AreEqual(Path.GetFullPath(settingsPath), provider.GetRequiredService<ServerNodeSettingsStore>().FilePath);
            Assert.AreEqual(Path.GetFullPath(requestsPath), provider.GetRequiredService<RemotePinRequestStore>().FilePath);
            Assert.AreEqual(Path.GetFullPath(logPath), provider.GetRequiredService<ApplicationLogStore>().FilePath);
            Assert.AreEqual(Path.GetFullPath(explorerPath), provider.GetRequiredService<ExplorerIndexStore>().FilePath);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static ServiceProvider CreateProvider(string tempRoot)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OperatingProfile:Mode"] = "Light",
                ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
                ["NodeSettingsDefaults:ApiPath"] = "api/v0",
                ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIpfsNodeControlApplication(configuration);
        services.PostConfigure<ServerNodeSettingsStoreOptions>(options => options.FilePath = Path.Combine(tempRoot, "current-node-settings.json"));
        services.PostConfigure<RemotePinRequestStoreOptions>(options => options.FilePath = Path.Combine(tempRoot, "remote-pin-requests.json"));
        services.PostConfigure<ApplicationLogStoreOptions>(options => options.FilePath = Path.Combine(tempRoot, "application.log"));
        services.PostConfigure<ExplorerIndexStoreOptions>(options => options.FilePath = Path.Combine(tempRoot, "explorer.db"));
        return services.BuildServiceProvider();
    }
}
