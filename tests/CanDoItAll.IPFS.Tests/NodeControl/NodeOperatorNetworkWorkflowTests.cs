using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs;
using Ipfs.Engine;
using Ipfs.Engine.ClientTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeOperatorNetworkWorkflowTests
{
    [TestMethod]
    public async Task SwarmBootstrapAndAddressFilters_Work_Through_NodeOperatorService()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        using var remote = await StartIsolatedNodeAsync().ConfigureAwait(false);

        var remotePeer = await remote.Generic.IdAsync().ConfigureAwait(false);
        var remoteAddress = (await TestIpfsHttpHost.GetDialAddressAsync(remote).ConfigureAwait(false)).ToString();
        var filterAddress = "/ip4/127.0.0.1";

        await service.ConnectAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);
        var connected = await WaitForAsync(
            async () => (await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).ConnectedPeers.Any(peer => peer.Id == remotePeer.Id.ToString()),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.IsTrue(connected, "The remote peer should appear in the swarm snapshot after connect.");

        await service.AddBootstrapAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);
        var bootstrapAdded = await WaitForAsync(
            async () => (await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).BootstrapPeers.Contains(remoteAddress, StringComparer.Ordinal),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsTrue(bootstrapAdded, "The bootstrap snapshot should include the added peer address.");

        await service.RemoveBootstrapAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);
        var bootstrapRemoved = await WaitForAsync(
            async () => !(await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).BootstrapPeers.Contains(remoteAddress, StringComparer.Ordinal),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsTrue(bootstrapRemoved, "The bootstrap snapshot should drop the removed peer address.");

        await service.RemoveAllBootstrapAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(0, (await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).BootstrapPeers.Count);

        var restored = await service.RestoreDefaultBootstrapAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.IsTrue(restored.Count > 0, "Default bootstrap peers should be restorable.");

        await service.AddAddressFilterAsync(filterAddress, CancellationToken.None).ConfigureAwait(false);
        var filterAdded = await WaitForAsync(
            async () => (await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).AddressFilters.Contains(filterAddress, StringComparer.Ordinal),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsTrue(filterAdded, "The address filter should appear in the network snapshot.");

        await service.RemoveAddressFilterAsync(filterAddress, CancellationToken.None).ConfigureAwait(false);
        var filterRemoved = await WaitForAsync(
            async () => !(await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).AddressFilters.Contains(filterAddress, StringComparer.Ordinal),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsTrue(filterRemoved, "The address filter should be removable through the operator service.");

        await service.DisconnectAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task DhtPeerAndProviderLookups_Work_Through_NodeOperatorService()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        using var remote = await StartIsolatedNodeAsync().ConfigureAwait(false);

        var remotePeer = await remote.Generic.IdAsync().ConfigureAwait(false);
        var remoteAddress = (await TestIpfsHttpHost.GetDialAddressAsync(remote).ConfigureAwait(false)).ToString();
        await service.ConnectAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);

        var foundPeer = await service.FindPeerAsync(remotePeer.Id.ToString(), CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(remotePeer.Id.ToString(), foundPeer.Id);
        Assert.IsTrue(foundPeer.Addresses.Count > 0);

        var localFile = await service.UploadTextAsync("provider-proof.txt", "provider lookup proof", pin: true, wrap: false, CancellationToken.None).ConfigureAwait(false);
        var localCid = Cid.Decode(localFile.ResolvedId);
        await host.Node.Dht.ProvideAsync(localCid).ConfigureAwait(false);

        var localPeer = await host.Node.LocalPeer.ConfigureAwait(false);
        var providersReady = await WaitForAsync(
            async () => (await service.FindProvidersAsync(localCid.ToString(), 20, CancellationToken.None).ConfigureAwait(false)).Any(peer => peer.Id == localPeer.Id.ToString()),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.IsTrue(providersReady, "The local node should be returned as a provider for advertised content.");
    }

    [TestMethod]
    public async Task PubSubPublishPeersAndSubscribe_Work_Through_OperatorClientPaths()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var context = NodeOperatorTestHarness.CreateContext(host.BaseAddress);
        var service = context.Service;
        using var remote = await StartIsolatedNodeAsync().ConfigureAwait(false);

        var topic = $"bundle-network-{Guid.NewGuid():N}";
        var payload = $"bundle pubsub proof {Guid.NewGuid():N}";
        var remoteAddress = (await TestIpfsHttpHost.GetDialAddressAsync(remote).ConfigureAwait(false)).ToString();
        await service.ConnectAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);

        using var localCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var remoteCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var localReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var remoteReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var localLease = context.ClientFactory.CreateLease();
        var localSubscription = localLease.Client.PubSub.SubscribeAsync(topic, message =>
        {
            localReceived.TrySetResult(Encoding.UTF8.GetString(message.DataBytes));
        }, localCts.Token);

        var remoteSubscription = remote.PubSub.SubscribeAsync(topic, message =>
        {
            remoteReceived.TrySetResult(Encoding.UTF8.GetString(message.DataBytes));
        }, remoteCts.Token);

        var topicsReady = await WaitForAsync(
            async () => (await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).PubSubTopics.Contains(topic, StringComparer.Ordinal),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.IsTrue(topicsReady, "The operator network snapshot should surface subscribed topics.");

        var peersReady = await WaitForAsync(
            async () => (await service.ListPubSubPeersAsync(topic, CancellationToken.None).ConfigureAwait(false)).Count > 0,
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.IsTrue(peersReady, "The operator service should return topic peers once subscriptions are live.");

        await service.PublishPubSubAsync(topic, payload, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(payload, await localReceived.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false));
        Assert.AreEqual(payload, await remoteReceived.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false));

        localCts.Cancel();
        remoteCts.Cancel();
        await localSubscription.ConfigureAwait(false);
        await remoteSubscription.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CrossNodeShareAndFetch_Work_Through_NodeOperatorService()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(host.BaseAddress);
        using var remote = await StartIsolatedNodeAsync().ConfigureAwait(false);

        var remoteAddress = (await TestIpfsHttpHost.GetDialAddressAsync(remote).ConfigureAwait(false)).ToString();
        await service.ConnectAsync(remoteAddress, CancellationToken.None).ConfigureAwait(false);

        var content = $"cross node fetch proof {Guid.NewGuid():N}";
        var remoteFile = await remote.FileSystem.AddTextAsync(content).ConfigureAwait(false);
        var remoteCid = remoteFile.Id.ToString();

        var preview = await service.ReadFilePreviewAsync(remoteCid, 4096, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(content, preview);

        var pinned = await service.PinAsync(remoteCid, recursive: true, CancellationToken.None).ConfigureAwait(false);
        CollectionAssert.Contains(pinned.ToArray(), remoteCid);
        CollectionAssert.Contains((await service.ListPinsAsync(CancellationToken.None).ConfigureAwait(false)).ToArray(), remoteCid);

        var inspected = await service.InspectFileSystemAsync(remoteCid, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(remoteCid, inspected.ResolvedId);
        Assert.IsFalse(inspected.IsDirectory);
    }

    [TestMethod]
    public async Task ConnectByKnownNodeApiAsync_Resolves_Identity_And_Connects_Using_Explicit_Dial_Address()
    {
        await using var localHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        await using var remoteHost = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var service = NodeOperatorTestHarness.CreateService(localHost.BaseAddress);
        var remoteDialAddress = await TestIpfsHttpHost.GetDialAddressAsync(remoteHost.Node).ConfigureAwait(false);
        var remotePeer = await remoteHost.Client.Generic.IdAsync().ConfigureAwait(false);

        var resolved = await service.ConnectByKnownNodeApiAsync(
            "127.0.0.1",
            remoteHost.BaseAddress.Port,
            GetTcpPort(remoteDialAddress.ToString()),
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(remotePeer.Id.ToString(), resolved.PeerId);
        StringAssert.Contains(resolved.DialAddress, remotePeer.Id.ToString());
        StringAssert.Contains(resolved.DialAddress, "/ip4/127.0.0.1/tcp/");
        CollectionAssert.Contains(
            (await service.GetNetworkSnapshotAsync(CancellationToken.None).ConfigureAwait(false)).ConnectedPeers.Select(peer => peer.Id).ToArray(),
            remotePeer.Id.ToString());
    }

    private static async Task<TempNode> StartIsolatedNodeAsync()
    {
        var node = new TempNode();
        node.Options.Discovery.DisableMdns = true;
        node.Options.Discovery.DisableRandomWalk = true;
        await node.Bootstrap.RemoveAllAsync().ConfigureAwait(false);
        await node.StartAsync().ConfigureAwait(false);
        return node;
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        return await predicate().ConfigureAwait(false);
    }

    private static int GetTcpPort(string dialAddress)
    {
        var segments = dialAddress.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tcpIndex = Array.FindIndex(segments, segment => string.Equals(segment, "tcp", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(tcpIndex >= 0 && tcpIndex < segments.Length - 1, $"Could not parse a TCP port from '{dialAddress}'.");
        return int.Parse(segments[tcpIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
    }
}
