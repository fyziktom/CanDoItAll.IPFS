using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using Ipfs.Engine.Client.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeOperatorMaintenanceWorkflowTests
{
    [TestMethod]
    public async Task Maintenance_Config_RoundTrips_And_Reports_Repository_Version()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var configKey = $"NodeControlMaintenance.{Guid.NewGuid():N}";

        await service.SetConfigValueAsync(configKey, "alpha", treatAsJson: false, CancellationToken.None).ConfigureAwait(false);

        var configValue = await service.GetConfigValueAsync(configKey, CancellationToken.None).ConfigureAwait(false);
        var fullConfig = await service.GetFullConfigAsync(CancellationToken.None).ConfigureAwait(false);
        var repositoryVersion = await service.GetRepositoryVersionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("\"alpha\"", configValue);
        StringAssert.Contains(fullConfig, "\"NodeControlMaintenance\"");
        StringAssert.Contains(fullConfig, configKey.Split('.')[1]);
        Assert.IsFalse(string.IsNullOrWhiteSpace(repositoryVersion));
    }

    [TestMethod]
    public async Task Maintenance_Repository_Gc_Completes_And_Verify_Surfaces_The_Current_Server_Gap()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);

        await service.RunRepositoryGcAsync(CancellationToken.None).ConfigureAwait(false);
        var exception = await ThrowsAsync<IpfsApiException>(() =>
            service.VerifyRepositoryAsync(CancellationToken.None)).ConfigureAwait(false);

        Assert.AreEqual(System.Net.HttpStatusCode.NotImplemented, exception.StatusCode);
    }

    [TestMethod]
    public async Task ShutdownNodeAsync_Stops_The_Current_Node_Listener()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        var dialAddress = (await TestIpfsHttpHost.GetDialAddressAsync(host.Node).ConfigureAwait(false)).ToString();
        var endpoint = ParseTcpEndpoint(dialAddress);

        await AssertEndpointListeningAsync(endpoint.host, endpoint.port, shouldBeListening: true).ConfigureAwait(false);

        await service.ShutdownNodeAsync().ConfigureAwait(false);

        await AssertEndpointListeningAsync(endpoint.host, endpoint.port, shouldBeListening: false).ConfigureAwait(false);
    }

    private static async Task AssertEndpointListeningAsync(string host, int port, bool shouldBeListening)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        while (!timeoutCts.IsCancellationRequested)
        {
            using var probe = new TcpClient();
            try
            {
                await probe.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
                if (shouldBeListening)
                {
                    return;
                }
            }
            catch
            {
                if (!shouldBeListening)
                {
                    return;
                }
            }

            await Task.Delay(250, timeoutCts.Token).ConfigureAwait(false);
        }

        Assert.Fail(shouldBeListening
            ? $"Timed out waiting for {host}:{port} to start listening."
            : $"Timed out waiting for {host}:{port} to stop listening.");
    }

    private static (string host, int port) ParseTcpEndpoint(string dialAddress)
    {
        var segments = dialAddress.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hostIndex = Array.FindIndex(segments, segment =>
            string.Equals(segment, "ip4", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "ip6", StringComparison.OrdinalIgnoreCase));
        var portIndex = Array.FindIndex(segments, segment => string.Equals(segment, "tcp", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(hostIndex >= 0 && hostIndex < segments.Length - 1, $"Could not parse a host from '{dialAddress}'.");
        Assert.IsTrue(portIndex >= 0 && portIndex < segments.Length - 1, $"Could not parse a TCP port from '{dialAddress}'.");

        return (segments[hostIndex + 1], int.Parse(segments[portIndex + 1], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task<T> ThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (T ex)
        {
            return ex;
        }

        Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
        return null!;
    }
}
