using CanDoItAll.Components.CanvasLib;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class NodeCanvasSurfaceFactory
{
    public CanvasWorkbenchSurface CreateFileSurface(NodeFileSnapshot? snapshot, string uiStateJson)
    {
        var uiState = CanvasWorkbenchUiState.Parse(uiStateJson);
        if (snapshot is null)
        {
            return new CanvasWorkbenchSurface
            {
                SurfaceId = "files",
                Mode = "inspection",
                UiState = uiState,
                Chrome = new CanvasWorkbenchChrome
                {
                    ShowQuickCreateRail = false,
                    HintText = "Load a CID or path to see the file graph.",
                    EmptyStateKicker = "Files",
                    EmptyStateTitle = "Load a file or directory",
                    EmptyStateDescription = "Inspect a CID, upload a file, or pin content to populate the workbench."
                }
            };
        }

        var nodes = new List<CanvasWorkbenchNode>();
        var links = new List<CanvasWorkbenchLink>();
        var rootId = snapshot.ResolvedId;

        nodes.Add(new CanvasWorkbenchNode
        {
            Id = rootId,
            Family = "root",
            Kind = snapshot.IsDirectory ? "directory" : "file",
            Title = snapshot.IsDirectory ? "Directory root" : "File root",
            Subtitle = rootId,
            LeadText = snapshot.RequestedPath,
            Status = snapshot.IsDirectory ? "Directory" : "File",
            StatusPill = snapshot.IsDirectory ? "Folder" : "Blob",
            AccentColor = snapshot.IsDirectory ? "#2563eb" : "#0f766e",
            PaletteKey = snapshot.IsDirectory ? "info" : "success",
            X = 0,
            Y = 0,
            Chips =
            [
                new CanvasWorkbenchChip { Text = $"Size {snapshot.Size:n0} B", Tone = "neutral" },
                new CanvasWorkbenchChip { Text = $"{snapshot.Links.Count} links", Tone = "info" }
            ]
        });

        var index = 0;
        foreach (var item in snapshot.Links)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = item.Target,
                Family = "item",
                Kind = "link",
                ParentId = rootId,
                Title = string.IsNullOrWhiteSpace(item.Name) ? item.Target : item.Name,
                Subtitle = item.Target,
                LeadText = $"Child size {item.Size:n0} bytes",
                Status = "Linked content",
                StatusPill = "Child",
                AccentColor = "#7c3aed",
                PaletteKey = "neutral",
                X = 320,
                Y = index * 150,
                Chips = [new CanvasWorkbenchChip { Text = $"{item.Size:n0} B", Tone = "neutral" }]
            });

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = rootId,
                TargetId = item.Target,
                Kind = "contains"
            });

            index++;
        }

        if (uiState.SelectedNodeIds.Count == 0)
        {
            uiState.SelectedNodeIds = [rootId];
        }

        return new CanvasWorkbenchSurface
        {
            SurfaceId = "files",
            Mode = "inspection",
            Nodes = nodes,
            Links = links,
            UiState = uiState,
            Chrome = new CanvasWorkbenchChrome
            {
                ShowQuickCreateRail = false,
                HintText = "Select a node to inspect it. Double-click a child node to open that CID.",
                EmptyStateKicker = "Files",
                EmptyStateTitle = "No graph",
                EmptyStateDescription = "This node did not return any links."
            }
        };
    }

    public CanvasWorkbenchSurface CreateNetworkSurface(NodeNetworkSnapshot snapshot, string uiStateJson)
    {
        var uiState = CanvasWorkbenchUiState.Parse(uiStateJson);
        var nodes = new List<CanvasWorkbenchNode>();
        var links = new List<CanvasWorkbenchLink>();
        var groupFrames = new List<CanvasWorkbenchGroupFrame>();

        const string rootId = "local-node";
        nodes.Add(new CanvasWorkbenchNode
        {
            Id = rootId,
            Family = "root",
            Kind = "node",
            Title = "Local node",
            Subtitle = "Connected topology",
            LeadText = $"{snapshot.ConnectedPeers.Count} live peers",
            Status = "Active",
            StatusPill = "Node",
            AccentColor = "#1d4ed8",
            PaletteKey = "info",
            X = 0,
            Y = 0,
            Chips =
            [
                new CanvasWorkbenchChip { Text = $"{snapshot.BootstrapPeers.Count} bootstrap", Tone = "warning" },
                new CanvasWorkbenchChip { Text = $"{snapshot.KnownPeers.Count} known", Tone = "info" },
                new CanvasWorkbenchChip { Text = $"{snapshot.AddressFilters.Count} filters", Tone = "neutral" }
            ]
        });

        var connectedIds = new List<string>();
        for (var i = 0; i < snapshot.ConnectedPeers.Count; i++)
        {
            var peer = snapshot.ConnectedPeers[i];
            var nodeId = $"peer:{peer.Id}";
            connectedIds.Add(nodeId);
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = nodeId,
                Family = "peer",
                Kind = "peer",
                ParentId = rootId,
                Title = Shorten(peer.Id, 22),
                Subtitle = peer.AgentVersion,
                LeadText = peer.ConnectedAddress,
                Status = peer.ProtocolVersion,
                StatusPill = "Swarm",
                AccentColor = "#0f766e",
                PaletteKey = "success",
                X = 340,
                Y = i * 150,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = peer.Latency, Tone = "neutral" },
                    new CanvasWorkbenchChip { Text = $"{peer.Addresses.Count} addrs", Tone = "info" }
                ]
            });

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = rootId,
                TargetId = nodeId,
                Kind = "connected"
            });
        }

        var connectedPeerIds = snapshot.ConnectedPeers
            .Select(peer => peer.Id)
            .ToHashSet(StringComparer.Ordinal);
        var knownPeerIds = new List<string>();
        var knownPeers = snapshot.KnownPeers
            .Where(peer => !string.IsNullOrWhiteSpace(peer.Id) && !connectedPeerIds.Contains(peer.Id))
            .OrderBy(peer => peer.Id, StringComparer.Ordinal)
            .ToList();
        for (var i = 0; i < knownPeers.Count; i++)
        {
            var peer = knownPeers[i];
            var nodeId = $"known-peer:{peer.Id}";
            knownPeerIds.Add(nodeId);
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = nodeId,
                Family = "known-peer",
                Kind = "peer",
                ParentId = rootId,
                Title = Shorten(peer.Id, 22),
                Subtitle = "Known peer",
                LeadText = peer.Addresses.FirstOrDefault() ?? "No address reported",
                Status = peer.AgentVersion,
                StatusPill = "Known",
                AccentColor = "#475569",
                PaletteKey = "neutral",
                X = 620,
                Y = i * 140,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = $"{peer.Addresses.Count} addrs", Tone = "info" }
                ]
            });

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = rootId,
                TargetId = nodeId,
                Kind = "known"
            });
        }

        var bootstrapIds = new List<string>();
        for (var i = 0; i < snapshot.BootstrapPeers.Count; i++)
        {
            var address = snapshot.BootstrapPeers[i];
            var nodeId = $"bootstrap:{i}";
            bootstrapIds.Add(nodeId);
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = nodeId,
                Family = "bootstrap",
                Kind = "bootstrap",
                Title = "Bootstrap",
                Subtitle = Shorten(address, 36),
                LeadText = address,
                Status = "Configured",
                StatusPill = "Bootstrap",
                AccentColor = "#b45309",
                PaletteKey = "warning",
                X = -380,
                Y = i * 132
            });

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = nodeId,
                TargetId = rootId,
                Kind = "seed"
            });
        }

        var filterIds = new List<string>();
        for (var i = 0; i < snapshot.AddressFilters.Count; i++)
        {
            var filter = snapshot.AddressFilters[i];
            var nodeId = $"filter:{i}";
            filterIds.Add(nodeId);
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = nodeId,
                Family = "filter",
                Kind = "filter",
                Title = "Address filter",
                Subtitle = Shorten(filter, 28),
                LeadText = filter,
                Status = "Applied",
                StatusPill = "Filter",
                AccentColor = "#7c2d12",
                PaletteKey = "danger",
                X = -140 + (i * 220),
                Y = 360
            });
        }

        var topicIds = new List<string>();
        for (var i = 0; i < snapshot.PubSubTopics.Count; i++)
        {
            var topic = snapshot.PubSubTopics[i];
            var nodeId = $"topic:{topic}";
            topicIds.Add(nodeId);
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = nodeId,
                Family = "topic",
                Kind = "pubsub",
                Title = topic,
                Subtitle = "PubSub topic",
                LeadText = "Locally subscribed",
                Status = "PubSub",
                StatusPill = "Topic",
                AccentColor = "#6d28d9",
                PaletteKey = "accent",
                X = 80 + (i * 220),
                Y = -210
            });

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = rootId,
                TargetId = nodeId,
                Kind = "topic"
            });
        }

        if (connectedIds.Count > 0)
        {
            groupFrames.Add(new CanvasWorkbenchGroupFrame
            {
                Id = "connected-peers",
                Label = "Connected peers",
                Tone = "success",
                AnchorNodeIds = connectedIds
            });
        }

        if (bootstrapIds.Count > 0)
        {
            groupFrames.Add(new CanvasWorkbenchGroupFrame
            {
                Id = "bootstrap-peers",
                Label = "Bootstrap peers",
                Tone = "warning",
                AnchorNodeIds = bootstrapIds
            });
        }

        if (knownPeerIds.Count > 0)
        {
            groupFrames.Add(new CanvasWorkbenchGroupFrame
            {
                Id = "known-peers",
                Label = "Known peers",
                Tone = "neutral",
                AnchorNodeIds = knownPeerIds
            });
        }

        if (filterIds.Count > 0)
        {
            groupFrames.Add(new CanvasWorkbenchGroupFrame
            {
                Id = "address-filters",
                Label = "Address filters",
                Tone = "danger",
                AnchorNodeIds = filterIds
            });
        }

        if (topicIds.Count > 0)
        {
            groupFrames.Add(new CanvasWorkbenchGroupFrame
            {
                Id = "pubsub-topics",
                Label = "PubSub topics",
                Tone = "accent",
                AnchorNodeIds = topicIds
            });
        }

        uiState.GroupFrames = groupFrames;
        if (uiState.SelectedNodeIds.Count == 0)
        {
            uiState.SelectedNodeIds = [rootId];
        }

        return new CanvasWorkbenchSurface
        {
            SurfaceId = "network",
            Mode = "inspection",
            Nodes = nodes,
            Links = links,
            UiState = uiState,
            Chrome = new CanvasWorkbenchChrome
            {
                ShowQuickCreateRail = false,
                HintText = "Connected peers, bootstrap entries, filters, and PubSub topics are arranged around the local node.",
                EmptyStateKicker = "Network",
                EmptyStateTitle = "No network topology yet",
                EmptyStateDescription = "Refresh the network view after connecting to a node."
            }
        };
    }

    private static string Shorten(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= limit)
        {
            return value;
        }

        return $"{value[..(limit - 3)]}...";
    }
}
