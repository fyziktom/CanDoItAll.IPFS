using System;
using System.IO;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.Tests.NodeControl;

internal static class NodeOperatorTestHarness
{
    public static NodeOperatorService CreateService(Uri baseAddress)
        => CreateContext(baseAddress).Service;

    public static NodeOperatorService CreateService(Uri baseAddress, string indexStorePath)
        => CreateContext(baseAddress, indexStorePath).Service;

    public static NodeOperatorTestContext CreateContext(Uri baseAddress)
        => CreateContext(baseAddress, indexStorePath: null);

    public static NodeOperatorTestContext CreateContext(Uri baseAddress, string? indexStorePath)
    {
        var settings = new NodeConnectionSettings
        {
            BaseUrl = baseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };

        var targetRegistry = new CurrentNodeTargetRegistry();
        var bootstrapService = new LocalNodeBootstrapService(
            targetRegistry,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalNodeBootstrapService>.Instance);
        var leaseFactory = new CurrentNodeLeaseFactory(targetRegistry, bootstrapService);
        var sessionState = new NodeSessionState(Options.Create(settings), targetRegistry);
        var clientFactory = new IpfsClientFactory(sessionState, leaseFactory);
        var indexStore = new ExplorerIndexStore(Options.Create(new ExplorerIndexStoreOptions
        {
            FilePath = string.IsNullOrWhiteSpace(indexStorePath)
                ? Path.Combine(Path.GetTempPath(), "IpfsNodeControlTests", $"explorer-index-{Guid.NewGuid():N}.db")
                : indexStorePath
        }));
        var fileWorkflow = new NodeFileWorkflowService(clientFactory, indexStore);
        var explorerWorkflow = new NodeExplorerWorkflowService(fileWorkflow, indexStore);
        var contentWorkflow = new NodeContentWorkflowService(clientFactory);
        var networkWorkflow = new NodeNetworkWorkflowService(clientFactory);
        var maintenanceWorkflow = new NodeMaintenanceWorkflowService(clientFactory);
        var service = new NodeOperatorService(
            fileWorkflow,
            explorerWorkflow,
            contentWorkflow,
            networkWorkflow,
            maintenanceWorkflow);

        return new NodeOperatorTestContext(
            service,
            clientFactory,
            indexStore,
            fileWorkflow,
            explorerWorkflow,
            contentWorkflow,
            networkWorkflow,
            maintenanceWorkflow);
    }
}

internal sealed record NodeOperatorTestContext(
    NodeOperatorService Service,
    IpfsClientFactory ClientFactory,
    ExplorerIndexStore IndexStore,
    NodeFileWorkflowService FileWorkflow,
    NodeExplorerWorkflowService ExplorerWorkflow,
    NodeContentWorkflowService ContentWorkflow,
    NodeNetworkWorkflowService NetworkWorkflow,
    NodeMaintenanceWorkflowService MaintenanceWorkflow);
