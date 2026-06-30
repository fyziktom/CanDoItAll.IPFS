#nullable enable

using System;
using System.Linq;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeControlLayeringTests
{
    [TestMethod]
    public void AbstractionsAssembly_DoesNotReference_NodeControlWebOrUiAssemblies()
    {
        var referencedAssemblyNames = typeof(INodeOperator).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var forbiddenReferences = referencedAssemblyNames
            .Where(name =>
                string.Equals(name, "CanDoItAll.IPFS.NodeControl", StringComparison.Ordinal)
                || name.StartsWith("CanDoItAll.Components.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal)
                || name.Contains("WindowsForms", StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            forbiddenReferences.Length,
            "The UI-independent NodeControl contracts assembly must not reference Web, Blazor component, or desktop assemblies. References: "
                + string.Join(", ", referencedAssemblyNames));
    }

    [TestMethod]
    public void NodeOperatorContract_And_NodeSnapshots_Live_In_ContractsAssembly()
    {
        Assert.AreSame(typeof(INodeOperator).Assembly, typeof(NodeFileSnapshot).Assembly);
        Assert.AreSame(typeof(INodeOperator).Assembly, typeof(NodeNetworkSnapshot).Assembly);
        Assert.AreSame(typeof(INodeConnectionLeaseFactory).Assembly, typeof(IExplorerIndexStore).Assembly);
    }
}
