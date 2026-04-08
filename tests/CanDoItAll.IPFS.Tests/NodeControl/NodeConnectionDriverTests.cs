using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeConnectionDriverTests
{
    [TestMethod]
    [DataRow(NodeConnectionRequestCategory.ReadOnlyUi, NodeControlHttpClientNames.NodeRead)]
    [DataRow(NodeConnectionRequestCategory.Gateway, NodeControlHttpClientNames.NodeGateway)]
    [DataRow(NodeConnectionRequestCategory.Mutation, NodeControlHttpClientNames.NodeMutation)]
    [DataRow(NodeConnectionRequestCategory.Admin, NodeControlHttpClientNames.NodeAdmin)]
    [DataRow(NodeConnectionRequestCategory.RemotePin, NodeControlHttpClientNames.NodeRemotePin)]
    public async Task CreateLeaseAsync_Uses_The_Expected_Named_Client(
        NodeConnectionRequestCategory category,
        string expectedClientName)
    {
        var targetRegistry = CreateRegistry("http://203.0.113.10:5001/", timeoutSeconds: 15);
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new CurrentNodeLeaseFactory(
            targetRegistry,
            new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance),
            httpClientFactory);

        using var lease = await factory.CreateLeaseAsync(category, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(expectedClientName, httpClientFactory.RequestedNames.Single());
        Assert.AreEqual(new Uri("http://203.0.113.10:5001/"), lease.HttpClient.BaseAddress);
        Assert.AreEqual(15, (int)lease.HttpClient.Timeout.TotalSeconds);
    }

    [TestMethod]
    public void CreateLease_Compatibility_Adapter_Defaults_To_The_Read_Client_Name()
    {
        var targetRegistry = CreateRegistry("http://203.0.113.10:5001/", timeoutSeconds: 15);
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new CurrentNodeLeaseFactory(
            targetRegistry,
            new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance),
            httpClientFactory);

        using var lease = factory.CreateLease();

        Assert.AreEqual(NodeControlHttpClientNames.NodeRead, httpClientFactory.RequestedNames.Single());
        Assert.AreEqual(new Uri("http://203.0.113.10:5001/"), lease.HttpClient.BaseAddress);
    }

    [TestMethod]
    public async Task CreateLeaseAsync_Reuses_The_Same_Category_Resolution_On_Repeated_Leases()
    {
        var targetRegistry = CreateRegistry("http://203.0.113.10:5001/", timeoutSeconds: 15);
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new CurrentNodeLeaseFactory(
            targetRegistry,
            new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance),
            httpClientFactory);

        using var firstLease = await factory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, CancellationToken.None).ConfigureAwait(false);
        using var secondLease = await factory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi, CancellationToken.None).ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { NodeControlHttpClientNames.NodeRead, NodeControlHttpClientNames.NodeRead },
            httpClientFactory.RequestedNames.ToArray());
    }

    private static CurrentNodeTargetRegistry CreateRegistry(string baseUrl, int timeoutSeconds)
    {
        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(new NodeConnectionSettings
        {
            Label = "Driver test node",
            BaseUrl = baseUrl,
            ApiPath = "api/v0",
            TimeoutSeconds = timeoutSeconds
        }, isHydrated: true);
        return targetRegistry;
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        public List<string> RequestedNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            RequestedNames.Add(name);
            return new HttpClient(new StaticResponseHandler());
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty),
                RequestMessage = request
            });
    }
}
