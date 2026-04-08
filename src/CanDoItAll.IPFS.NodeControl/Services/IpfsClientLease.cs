using Ipfs.Engine.Client;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class IpfsClientLease(HttpClient httpClient, IpfsEngineClient client, NodeConnectionSettings settings) : IDisposable
{
    public HttpClient HttpClient { get; } = httpClient;

    public IpfsEngineClient Client { get; } = client;

    public NodeConnectionSettings Settings { get; } = settings;

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}
