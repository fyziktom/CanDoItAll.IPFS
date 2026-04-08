using System.Linq;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class NodeCanvasSurfaceFactoryTests
{
    [TestMethod]
    public void CreateNetworkSurface_Includes_Known_Peers_And_Group_Frames()
    {
        var factory = new NodeCanvasSurfaceFactory();
        var surface = factory.CreateNetworkSurface(
            new NodeNetworkSnapshot
            {
                ConnectedPeers =
                [
                    new NodePeerSnapshot(
                        "12D3KooWConnected",
                        "net-ipfs/0.42.0",
                        "ipfs/0.1.0",
                        "/ip4/192.168.0.191/tcp/4001",
                        "n/a",
                        ["/ip4/192.168.0.191/tcp/4001/p2p/12D3KooWConnected"])
                ],
                KnownPeers =
                [
                    new NodePeerSnapshot(
                        "12D3KooWConnected",
                        "net-ipfs/0.42.0",
                        "ipfs/0.1.0",
                        "not connected",
                        "n/a",
                        ["/ip4/192.168.0.191/tcp/4001/p2p/12D3KooWConnected"]),
                    new NodePeerSnapshot(
                        "12D3KooWKnown",
                        "net-ipfs/0.42.0",
                        "ipfs/0.1.0",
                        "not connected",
                        "n/a",
                        ["/ip4/192.168.0.192/tcp/4001/p2p/12D3KooWKnown"])
                ],
                BootstrapPeers = ["/ip4/192.168.0.1/tcp/4001/p2p/12D3KooWBootstrap"],
                AddressFilters = ["/ip4/10.0.0.0/ipcidr/8"],
                PubSubTopics = ["operators"]
            },
            uiStateJson: string.Empty);

        Assert.AreEqual("network", surface.SurfaceId);
        Assert.IsTrue(surface.Nodes.Any(node => node.Id == "local-node"));
        Assert.IsTrue(surface.Nodes.Any(node => node.Id == "peer:12D3KooWConnected"));
        Assert.IsTrue(surface.Nodes.Any(node => node.Id == "known-peer:12D3KooWKnown"));
        Assert.IsFalse(surface.Nodes.Any(node => node.Id == "known-peer:12D3KooWConnected"));
        Assert.IsTrue(surface.UiState.GroupFrames.Any(frame => frame.Id == "known-peers"));
        Assert.IsTrue(surface.UiState.GroupFrames.Any(frame => frame.Id == "connected-peers"));
        Assert.IsTrue(surface.Links.Any(link => link.TargetId == "known-peer:12D3KooWKnown" && link.Kind == "known"));
    }
}
