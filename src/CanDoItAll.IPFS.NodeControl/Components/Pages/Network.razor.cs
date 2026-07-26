using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages;

public partial class Network
{
    private readonly List<SubscriptionMessage> subscriptionMessages = [];
    private NodeNetworkSnapshot? snapshot;
    private IReadOnlyList<NodePeerSnapshot> pubSubPeers = [];
    private IReadOnlyList<NodePeerSnapshot> providerResults = [];
    private NodePeerSnapshot? dhtPeerResult;
    private int networkTabIndex;
    private string? errorMessage;
    private string? connectionMessage;
    private string networkCanvasState = string.Empty;
    private string connectAddress = string.Empty;
    private string bootstrapAddress = string.Empty;
    private string filterAddress = string.Empty;
    private string knownNodeHost = string.Empty;
    private int knownNodeApiPort = 5001;
    private int knownNodeSwarmPort = 4001;
    private string dhtPeerId = string.Empty;
    private string providerCid = string.Empty;
    private string pubSubTopic = string.Empty;
    private string pubSubMessage = string.Empty;
    private string subscriptionTopic = string.Empty;
    private bool isBusy;
    private bool isSubscribed;
    private bool hasStartedInitialLoad;
    private CancellationTokenSource? subscriptionCts;
    private IpfsClientLease? subscriptionLease;

    private CanvasWorkbenchSurface networkSurface => NodeCanvasSurfaceFactory.CreateNetworkSurface(snapshot ?? new NodeNetworkSnapshot(), networkCanvasState);

    protected override void OnInitialized()
    {
        NodeSessionState.Changed += HandleNodeSessionChanged;
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !NodeSessionState.IsHydrated || hasStartedInitialLoad)
        {
            return Task.CompletedTask;
        }

        hasStartedInitialLoad = true;
        return LoadAsync();
    }

    private Task HandleNetworkTabChanged(int value)
    {
        networkTabIndex = value;
        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            snapshot = await NetworkWorkflow.GetNetworkSnapshotAsync(CancellationToken.None);
        });
    }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(connectAddress))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.ConnectAsync(connectAddress.Trim(), CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task DisconnectAsync()
    {
        if (string.IsNullOrWhiteSpace(connectAddress))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.DisconnectAsync(connectAddress.Trim(), CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task ConnectKnownNodeAsync()
    {
        if (string.IsNullOrWhiteSpace(knownNodeHost))
        {
            errorMessage = "A known node host or API URL is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var resolved = await NetworkWorkflow.ConnectByKnownNodeApiAsync(
                knownNodeHost.Trim(),
                knownNodeApiPort,
                knownNodeSwarmPort,
                CancellationToken.None);

            connectAddress = resolved.DialAddress;
            bootstrapAddress = resolved.DialAddress;
            dhtPeerId = resolved.PeerId;

            var advertisedKnownHost = resolved.AdvertisedAddresses.Any(address =>
                address.Contains(resolved.RequestedHost, StringComparison.OrdinalIgnoreCase));
            connectionMessage = advertisedKnownHost
                ? $"Connected to {resolved.AgentVersion} at {resolved.DialAddress}."
                : $"Connected to {resolved.AgentVersion} using explicit LAN dial address {resolved.DialAddress} because the remote node advertises non-routable addresses.";

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Peer connected",
                Detail = resolved.DialAddress
            });

            await LoadAsync();
        });
    }

    private async Task AddBootstrapAsync()
    {
        if (string.IsNullOrWhiteSpace(bootstrapAddress))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.AddBootstrapAsync(bootstrapAddress.Trim(), CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task RemoveBootstrapAsync()
    {
        if (string.IsNullOrWhiteSpace(bootstrapAddress))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.RemoveBootstrapAsync(bootstrapAddress.Trim(), CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task RestoreDefaultBootstrapAsync()
    {
        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.RestoreDefaultBootstrapAsync(CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task ClearBootstrapAsync()
    {
        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.RemoveAllBootstrapAsync(CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task AddFilterAsync()
    {
        if (string.IsNullOrWhiteSpace(filterAddress))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.AddAddressFilterAsync(filterAddress.Trim(), CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task RemoveFilterAsync()
    {
        if (string.IsNullOrWhiteSpace(filterAddress))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.RemoveAddressFilterAsync(filterAddress.Trim(), CancellationToken.None);
            await LoadAsync();
        });
    }

    private async Task FindPeerAsync()
    {
        if (string.IsNullOrWhiteSpace(dhtPeerId))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            dhtPeerResult = await NetworkWorkflow.FindPeerAsync(dhtPeerId.Trim(), CancellationToken.None);
        });
    }

    private async Task FindProvidersAsync()
    {
        if (string.IsNullOrWhiteSpace(providerCid))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            providerResults = await NetworkWorkflow.FindProvidersAsync(providerCid.Trim(), 20, CancellationToken.None);
        });
    }

    private async Task LoadPubSubPeersAsync()
    {
        await RunBusyAsync(async () =>
        {
            pubSubPeers = await NetworkWorkflow.ListPubSubPeersAsync(pubSubTopic, CancellationToken.None);
        });
    }

    private async Task PublishPubSubAsync()
    {
        if (string.IsNullOrWhiteSpace(pubSubTopic) || string.IsNullOrWhiteSpace(pubSubMessage))
        {
            errorMessage = "Both topic and message are required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await NetworkWorkflow.PublishPubSubAsync(pubSubTopic.Trim(), pubSubMessage, CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "PubSub message published",
                Detail = pubSubTopic.Trim()
            });
        });
    }

    private async Task StartSubscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(subscriptionTopic))
        {
            errorMessage = "A subscription topic is required.";
            return;
        }

        await StopSubscriptionAsync();
        subscriptionMessages.Clear();
        var lease = IpfsClientFactory.CreateLease();
        var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        subscriptionLease = lease;
        subscriptionCts = cancellationSource;
        isSubscribed = true;

        var topic = subscriptionTopic.Trim();
        _ = Task.Run(async () =>
        {
            try
            {
                await lease.Client.PubSub.SubscribeAsync(topic, message =>
                {
                    var payload = System.Text.Encoding.UTF8.GetString(message.DataBytes);
                    _ = InvokeAsync(() =>
                    {
                        subscriptionMessages.Insert(0, new SubscriptionMessage(DateTimeOffset.UtcNow, topic, message.Sender.Id?.ToString() ?? "unknown", payload));
                        while (subscriptionMessages.Count > 24)
                        {
                            subscriptionMessages.RemoveAt(subscriptionMessages.Count - 1);
                        }

                        StateHasChanged();
                    });
                }, cancellationToken);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await InvokeAsync(() =>
                {
                    errorMessage = ex.Message;
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = "PubSub subscription failed",
                        Detail = ex.Message
                    });
                });
            }
        });
    }

    private Task StopSubscriptionAsync()
    {
        isSubscribed = false;
        try
        {
            subscriptionCts?.Cancel();
        }
        catch
        {
        }

        subscriptionLease?.Dispose();
        subscriptionLease = null;
        subscriptionCts?.Dispose();
        subscriptionCts = null;
        return Task.CompletedTask;
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        errorMessage = null;
        isBusy = true;

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Network action failed",
                Detail = ex.Message
            });
        }
        finally
        {
            isBusy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task HandleWorkbenchSelectionChanged(CanvasWorkbenchSelectionChangedEventArgs args)
        => Task.CompletedTask;

    private Task HandleWorkbenchStateChanged(string stateJson)
    {
        networkCanvasState = stateJson;
        return Task.CompletedTask;
    }

    private Task HandleConnectAddressChanged(string? value)
    {
        connectAddress = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleBootstrapAddressChanged(string? value)
    {
        bootstrapAddress = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleFilterAddressChanged(string? value)
    {
        filterAddress = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleKnownNodeHostChanged(string? value)
    {
        knownNodeHost = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleKnownNodeApiPortChanged(int value)
    {
        knownNodeApiPort = value;
        return Task.CompletedTask;
    }

    private Task HandleKnownNodeSwarmPortChanged(int value)
    {
        knownNodeSwarmPort = value;
        return Task.CompletedTask;
    }

    private Task HandleDhtPeerIdChanged(string? value)
    {
        dhtPeerId = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleProviderCidChanged(string? value)
    {
        providerCid = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandlePubSubTopicChanged(string? value)
    {
        pubSubTopic = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandlePubSubMessageChanged(string? value)
    {
        pubSubMessage = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleSubscriptionTopicChanged(string? value)
    {
        subscriptionTopic = value ?? string.Empty;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        NodeSessionState.Changed -= HandleNodeSessionChanged;
        await StopSubscriptionAsync();
    }

    public void Dispose()
    {
        NodeSessionState.Changed -= HandleNodeSessionChanged;
    }

    private void HandleNodeSessionChanged()
    {
        if (!NodeSessionState.IsHydrated)
        {
            return;
        }

        hasStartedInitialLoad = true;
        _ = InvokeAsync(async () =>
        {
            await StopSubscriptionAsync();
            pubSubPeers = [];
            providerResults = [];
            dhtPeerResult = null;
            await LoadAsync();
        });
    }

    private static string Shorten(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= limit)
        {
            return value;
        }

        return $"{value[..(limit - 3)]}...";
    }

    private static string FormatBytes(ulong bytes)
    {
        const double scale = 1024d;
        var value = (double)bytes;
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unit = 0;
        while (value >= scale && unit < units.Length - 1)
        {
            value /= scale;
            unit++;
        }

        return $"{value:n2} {units[unit]}";
    }

    private sealed record SubscriptionMessage(DateTimeOffset Timestamp, string Topic, string Sender, string Payload);
}


