using Ipfs.Engine.Client;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public sealed class IpfsClientLease(HttpClient httpClient, IpfsNodeClient client, NodeConnectionSettings settings) : IDisposable
{
    public HttpClient HttpClient { get; } = httpClient;

    public IpfsNodeClient Client { get; } = client;

    public NodeConnectionSettings Settings { get; } = settings;

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}
