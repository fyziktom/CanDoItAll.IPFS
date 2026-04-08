using System.Text.Json;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Services;

public static class RemotePinRequestContractSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(RemotePinRequestEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    public static RemotePinRequestEnvelope Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("A remote pin request payload is required.");
        }

        return JsonSerializer.Deserialize<RemotePinRequestEnvelope>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The remote pin request payload could not be parsed.");
    }

    public static async Task<RemotePinRequestEnvelope> DeserializeAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var request = await JsonSerializer.DeserializeAsync<RemotePinRequestEnvelope>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return request ?? throw new InvalidOperationException("The remote pin request payload could not be parsed.");
    }

    internal static string SerializeSigningPayload(RemotePinRequestEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new SigningPayload
        {
            Version = request.Version,
            RequestId = request.RequestId,
            RequestedAtUtc = request.RequestedAtUtc,
            SenderId = request.SenderId,
            KeyId = request.KeyId,
            ExpiresAtUtc = request.ExpiresAtUtc,
            Nonce = request.Nonce,
            SignatureAlgorithm = request.SignatureAlgorithm,
            Note = request.Note,
            Sender = request.Sender,
            Content = request.Content
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private sealed class SigningPayload
    {
        public required int Version { get; init; }

        public required string RequestId { get; init; }

        public required DateTimeOffset RequestedAtUtc { get; init; }

        public string? SenderId { get; init; }

        public string? KeyId { get; init; }

        public DateTimeOffset? ExpiresAtUtc { get; init; }

        public string? Nonce { get; init; }

        public string? SignatureAlgorithm { get; init; }

        public string Note { get; init; } = string.Empty;

        public required RemotePinSenderSnapshot Sender { get; init; }

        public required RemotePinContentSnapshot Content { get; init; }
    }
}
