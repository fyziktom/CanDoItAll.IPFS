using System.Net;
using System.Net.Sockets;
using CanDoItAll.IPFS.DesktopHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class DesktopAppProcessUtilitiesTests
{
    [TestMethod]
    public async Task WaitForEndpointStateAsync_InternalDeadline_ThrowsTimeoutException()
    {
        var endpoint = new Uri($"http://127.0.0.1:{GetUnusedPort()}/");

        var exception = await Assert.ThrowsExactlyAsync<TimeoutException>(
            async () => await DesktopAppProcessUtilities.WaitForEndpointStateAsync(
                endpoint,
                shouldBeListening: true,
                TimeSpan.FromMilliseconds(50)));

        StringAssert.Contains(exception.Message, endpoint.ToString());
        StringAssert.Contains(exception.Message, "start listening");
    }

    private static int GetUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
