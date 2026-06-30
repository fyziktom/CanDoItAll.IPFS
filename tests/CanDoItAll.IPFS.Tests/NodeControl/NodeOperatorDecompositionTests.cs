using System;
using System.Linq;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeOperatorDecompositionTests
{
    [TestMethod]
    public void Workflow_Interfaces_Live_In_Ui_Neutral_Abstractions_Assembly()
    {
        var abstractionAssembly = typeof(INodeOperator).Assembly;

        Assert.AreSame(abstractionAssembly, typeof(INodeFileWorkflow).Assembly);
        Assert.AreSame(abstractionAssembly, typeof(INodeExplorerWorkflow).Assembly);
        Assert.AreSame(abstractionAssembly, typeof(INodeContentWorkflow).Assembly);
        Assert.AreSame(abstractionAssembly, typeof(INodeNetworkWorkflow).Assembly);
        Assert.AreSame(abstractionAssembly, typeof(INodeMaintenanceWorkflow).Assembly);
    }

    [TestMethod]
    public void NodeOperatorService_Depends_On_Workflow_Boundaries_Not_Raw_Node_Dependencies()
    {
        var constructorParameterTypes = typeof(NodeOperatorService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(NodeFileWorkflowService),
                typeof(INodeExplorerWorkflow),
                typeof(INodeContentWorkflow),
                typeof(INodeNetworkWorkflow),
                typeof(INodeMaintenanceWorkflow)
            },
            constructorParameterTypes);
        Assert.IsFalse(constructorParameterTypes.Contains(typeof(IpfsClientFactory)));
        Assert.IsFalse(constructorParameterTypes.Contains(typeof(IExplorerIndexStore)));
    }
}
