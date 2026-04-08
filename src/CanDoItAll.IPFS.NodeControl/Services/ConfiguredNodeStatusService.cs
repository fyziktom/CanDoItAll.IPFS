using System.Net;
using System.Net.Sockets;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;
using Newtonsoft.Json.Linq;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class ConfiguredNodeStatusService(
    INodeConnectionLeaseFactory currentNodeLeaseFactory,
    CurrentNodeTargetRegistry currentNodeTargetRegistry,
    HostedUrlRegistry hostedUrlRegistry)
{
    public NodeConnectionSettings GetConfiguredSettings()
        => currentNodeTargetRegistry.Current;

    public async Task<RemotePinReceiverProbeSnapshot> GetReceiverProbeAsync(CancellationToken cancellationToken)
    {
        using var lease = await currentNodeLeaseFactory.CreateLeaseAsync(
            NodeConnectionRequestCategory.ReadOnlyUi,
            cancellationToken).ConfigureAwait(false);
        var peer = await lease.Client.Generic.IdAsync(cancel: cancellationToken).ConfigureAwait(false);
        var swarmPort = await GetConfiguredSwarmTcpPortAsync(lease, cancellationToken).ConfigureAwait(false);
        var addresses = BuildAdvertisedAddresses(
            peer.Addresses.Select(address => address.ToString()),
            lease.Settings.BuildBaseAddress(),
            swarmPort);

        return new RemotePinReceiverProbeSnapshot
        {
            ControlAppUrl = hostedUrlRegistry.PreferredUrl,
            NodeLabel = lease.Settings.Label,
            NodeBaseUrl = lease.Settings.BaseUrl,
            ApiPath = lease.Settings.ApiPath,
            NodeHealthy = true,
            PeerId = peer.Id.ToString(),
            AgentVersion = peer.AgentVersion,
            Addresses = addresses
        };
    }

    public RemotePinReceiverProbeSnapshot BuildFailedProbeSnapshot(string diagnosticMessage)
    {
        var settings = currentNodeTargetRegistry.Current;
        return new RemotePinReceiverProbeSnapshot
        {
            ControlAppUrl = hostedUrlRegistry.PreferredUrl,
            NodeLabel = settings.Label,
            NodeBaseUrl = settings.BaseUrl,
            ApiPath = settings.ApiPath,
            NodeHealthy = false,
            PeerId = string.Empty,
            AgentVersion = "unavailable",
            DiagnosticMessage = diagnosticMessage
        };
    }

    internal static IReadOnlyList<string> BuildAdvertisedAddresses(
        IEnumerable<string> reportedAddresses,
        Uri nodeBaseAddress,
        int swarmPort)
    {
        ArgumentNullException.ThrowIfNull(reportedAddresses);
        ArgumentNullException.ThrowIfNull(nodeBaseAddress);

        var addresses = reportedAddresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!addresses.Any(IsRoutableDialAddress))
        {
            var fallback = TryBuildFallbackDialAddress(nodeBaseAddress, swarmPort);
            if (!string.IsNullOrWhiteSpace(fallback) && !addresses.Contains(fallback, StringComparer.Ordinal))
            {
                addresses.Add(fallback);
            }
        }

        return addresses;
    }

    private static async Task<int> GetConfiguredSwarmTcpPortAsync(
        IpfsClientLease lease,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuredSwarm = await lease.Client.Config.GetAsync("Addresses.Swarm", cancellationToken).ConfigureAwait(false);
            if (TryGetTcpPort(configuredSwarm, out var configuredPort))
            {
                return configuredPort;
            }
        }
        catch
        {
            // Fall back to the default swarm TCP port when config inspection is unavailable.
        }

        return 4001;
    }

    private static bool TryGetTcpPort(JToken configuredSwarm, out int port)
    {
        switch (configuredSwarm.Type)
        {
            case JTokenType.Array:
                foreach (var candidate in configuredSwarm.Values<string>())
                {
                    if (TryGetTcpPort(candidate, out port))
                    {
                        return true;
                    }
                }

                break;
            case JTokenType.String:
                if (TryGetTcpPort(configuredSwarm.Value<string>(), out port))
                {
                    return true;
                }

                break;
        }

        port = default;
        return false;
    }

    private static bool TryGetTcpPort(string? address, out int port)
    {
        port = default;

        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var segments = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!segments[i].Equals("tcp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(segments[i + 1], out port) && port > 0 && port <= 65535)
            {
                return true;
            }
        }

        port = default;
        return false;
    }

    private static bool IsRoutableDialAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var segments = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var protocol = segments[i];
            var value = segments[i + 1];
            if (protocol.Equals("ip4", StringComparison.OrdinalIgnoreCase)
                || protocol.Equals("ip6", StringComparison.OrdinalIgnoreCase))
            {
                return IsRoutableIpAddress(value);
            }

            if (protocol.Equals("dns", StringComparison.OrdinalIgnoreCase)
                || protocol.Equals("dns4", StringComparison.OrdinalIgnoreCase)
                || protocol.Equals("dns6", StringComparison.OrdinalIgnoreCase)
                || protocol.Equals("dnsaddr", StringComparison.OrdinalIgnoreCase))
            {
                return !string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static bool IsRoutableIpAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var ipAddress))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ipAddress))
        {
            return false;
        }

        if (ipAddress.Equals(IPAddress.Any)
            || ipAddress.Equals(IPAddress.IPv6Any)
            || ipAddress.Equals(IPAddress.None)
            || ipAddress.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        return !(ipAddress.AddressFamily == AddressFamily.InterNetworkV6 && ipAddress.IsIPv6LinkLocal);
    }

    private static string? TryBuildFallbackDialAddress(Uri nodeBaseAddress, int swarmPort)
    {
        if (swarmPort is <= 0 or > 65535)
        {
            return null;
        }

        var host = nodeBaseAddress.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (IPAddress.TryParse(host, out var ipAddress))
        {
            var protocol = ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? "ip6" : "ip4";
            return $"/{protocol}/{ipAddress}/tcp/{swarmPort}";
        }

        return $"/dns/{host}/tcp/{swarmPort}";
    }
}
