using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Security;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed class RemotePinShareService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConfiguredNodeStatusService configuredNodeStatusService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly RemotePinRequestSecurityService remotePinRequestSecurityService;
    private readonly IOptions<ControlAppSecurityOptions> securityOptions;

    public RemotePinShareService(
        ConfiguredNodeStatusService configuredNodeStatusService,
        IHttpClientFactory httpClientFactory,
        RemotePinRequestSecurityService remotePinRequestSecurityService,
        IOptions<ControlAppSecurityOptions> securityOptions)
    {
        this.configuredNodeStatusService = configuredNodeStatusService;
        this.httpClientFactory = httpClientFactory;
        this.remotePinRequestSecurityService = remotePinRequestSecurityService;
        this.securityOptions = securityOptions;
    }

    public RemotePinShareService(ConfiguredNodeStatusService configuredNodeStatusService)
        : this(
            configuredNodeStatusService,
            NodeControlServiceCollectionExtensions.CreateCompatibilityHttpClientFactory(),
            new RemotePinRequestSecurityService(Microsoft.Extensions.Options.Options.Create(new RemotePinSecurityOptions
            {
                CompatibilityModeEnabled = true
            })),
            Microsoft.Extensions.Options.Options.Create(new ControlAppSecurityOptions()))
    {
    }

    public async Task<RemotePinRequestEnvelope> CreateEnvelopeAsync(
        RemotePinContentSnapshot content,
        string? note,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var senderProbe = await configuredNodeStatusService.GetReceiverProbeAsync(cancellationToken).ConfigureAwait(false);
        var envelope = new RemotePinRequestEnvelope
        {
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim(),
            Sender = new RemotePinSenderSnapshot(
                senderProbe.NodeLabel,
                senderProbe.ControlAppUrl,
                senderProbe.NodeBaseUrl,
                senderProbe.PeerId,
                senderProbe.Addresses),
            Content = content
        };

        return remotePinRequestSecurityService.PrepareOutgoingEnvelope(envelope);
    }

    public RemotePinOutgoingSecuritySnapshot DescribeOutgoingSecurity()
        => remotePinRequestSecurityService.DescribeOutgoingSecurity();

    public async Task<RemotePinReceiverProbeSnapshot> ProbeAsync(string controlAppUrl, CancellationToken cancellationToken)
    {
        var endpoint = BuildApiUri(controlAppUrl, "api/remote-pin/probe");
        using var httpClient = CreateHttpClient(endpoint, isSend: false);
        using var response = await httpClient.GetAsync(string.Empty, cancellationToken).ConfigureAwait(false);

        var snapshot = await TryReadJsonAsync<RemotePinReceiverProbeSnapshot>(response, cancellationToken).ConfigureAwait(false);
        if (snapshot is not null)
        {
            return snapshot;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
            ? $"Remote probe failed with HTTP {(int)response.StatusCode}."
            : detail);
    }

    public async Task<StoredRemotePinRequest> SendAsync(
        string controlAppUrl,
        RemotePinRequestEnvelope request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoint = BuildApiUri(controlAppUrl, "api/remote-pin/requests");
        using var httpClient = CreateHttpClient(endpoint, isSend: true);
        using var response = await httpClient.PostAsJsonAsync(string.Empty, request, SerializerOptions, cancellationToken).ConfigureAwait(false);
        var stored = await TryReadJsonAsync<StoredRemotePinRequest>(response, cancellationToken).ConfigureAwait(false);
        if (stored is not null && response.IsSuccessStatusCode)
        {
            return stored;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
            ? $"Remote send failed with HTTP {(int)response.StatusCode}."
            : detail);
    }

    public static string NormalizeControlAppUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("A remote control-app URL is required.");
        }

        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"http://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Enter a valid absolute control-app URL.");
        }

        var builder = new UriBuilder(uri);
        if (string.IsNullOrWhiteSpace(builder.Path))
        {
            builder.Path = "/";
        }
        else if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return builder.Uri.ToString();
    }

    private static Uri BuildApiUri(string controlAppUrl, string relativePath)
        => new(new Uri(NormalizeControlAppUrl(controlAppUrl), UriKind.Absolute), relativePath);

    private HttpClient CreateHttpClient(Uri endpoint, bool isSend)
    {
        var httpClient = httpClientFactory.CreateClient(ResolveHttpClientName(endpoint, isSend));
        httpClient.BaseAddress = endpoint;
        httpClient.Timeout = TimeSpan.FromSeconds(20);
        var remotePinAccessKey = ResolveRemotePinAccessKey();
        if (!string.IsNullOrWhiteSpace(remotePinAccessKey))
        {
            httpClient.DefaultRequestHeaders.Remove(ControlAppSecurityHeaders.RemotePinAccessKey);
            httpClient.DefaultRequestHeaders.Add(ControlAppSecurityHeaders.RemotePinAccessKey, remotePinAccessKey);
        }

        return httpClient;
    }

    internal static bool ShouldAllowPrivateNetworkCertificateBypass(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || endpoint.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(endpoint.Host, out var address))
        {
            return false;
        }

        address = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 0xfc || bytes[0] == 0xfd;
        }

        return false;
    }

    internal static string ResolveHttpClientName(Uri endpoint, bool isSend)
    {
        var useInsecureHandler = ShouldAllowPrivateNetworkCertificateBypass(endpoint);
        return (isSend, useInsecureHandler) switch
        {
            (false, false) => NodeControlHttpClientNames.RemotePinProbe,
            (false, true) => NodeControlHttpClientNames.RemotePinProbeInsecure,
            (true, false) => NodeControlHttpClientNames.RemotePinSend,
            (true, true) => NodeControlHttpClientNames.RemotePinSendInsecure
        };
    }

    private string? ResolveRemotePinAccessKey()
    {
        var configuredOptions = securityOptions.Value;
        return string.IsNullOrWhiteSpace(configuredOptions.RemotePinAccessKey)
            ? configuredOptions.AdminAccessKey
            : configuredOptions.RemotePinAccessKey;
    }

    private static async Task<T?> TryReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            return default;
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
