using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CanDoItAll.IPFS.NodeControl.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ResiliencePolicyClassificationTests
{
    [TestMethod]
    public async Task NodeRead_Client_Retries_Post_Based_Read_Semantics()
    {
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using var provider = CreateProvider(NodeControlHttpClientNames.NodeRead, handler);
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(NodeControlHttpClientNames.NodeRead);
        client.BaseAddress = new Uri("http://127.0.0.1/");

        using var response = await client.PostAsync("api/v0/version", content: null, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(handler.RequestCount > 1, "Read semantics should retry transient POST failures.");
    }

    [TestMethod]
    [DataRow(NodeControlHttpClientNames.NodeMutation)]
    [DataRow(NodeControlHttpClientNames.NodeAdmin)]
    [DataRow(NodeControlHttpClientNames.NodeRemotePin)]
    public async Task Mutation_And_Admin_Clients_Do_Not_Retry_Unsafe_Posts(string clientName)
    {
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using var provider = CreateProvider(clientName, handler);
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(clientName);
        client.BaseAddress = new Uri("http://127.0.0.1/");

        using var response = await client.PostAsync("api/v0/test", content: null, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(1, handler.RequestCount, "Unsafe mutation/admin semantics should not retry POST failures.");
    }

    private static ServiceProvider CreateProvider(string clientName, SequenceHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNodeControlHttpClients();
        services.Configure<HttpClientFactoryOptions>(clientName, options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler);
        });
        return services.BuildServiceProvider();
    }

    private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> statuses = new(statuses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var status = statuses.Count > 0 ? statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(string.Empty),
                RequestMessage = request
            });
        }
    }
}
