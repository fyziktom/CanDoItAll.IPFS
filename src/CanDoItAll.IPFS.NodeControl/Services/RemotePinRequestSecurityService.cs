using System.Security.Cryptography;
using System.Text;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Services;

public sealed record RemotePinOutgoingSecuritySnapshot(
    bool CanCreateRequests,
    bool UsesCompatibilityMode,
    int EnvelopeVersion,
    string Summary,
    string Detail,
    string? KeyId,
    int RequestExpiryMinutes);

public sealed record RemotePinValidationResult(
    bool IsAccepted,
    bool ShouldPersistRejectedRequest,
    RemotePinSecurityDisposition Disposition,
    string Message,
    string? TrustedSenderLabel);

public sealed class RemotePinRequestSecurityService(IOptions<RemotePinSecurityOptions> options)
{
    public const string DefaultKeyId = "default";
    public const string HmacSha256Algorithm = "hmac-sha256";
    public const int SignedEnvelopeVersion = 2;

    public RemotePinOutgoingSecuritySnapshot DescribeOutgoingSecurity()
    {
        var configuredOptions = options.Value;
        var requestExpiryMinutes = Math.Max(1, configuredOptions.RequestExpiryMinutes);
        if (!string.IsNullOrWhiteSpace(configuredOptions.LocalSharedSecret))
        {
            return new RemotePinOutgoingSecuritySnapshot(
                CanCreateRequests: true,
                UsesCompatibilityMode: false,
                EnvelopeVersion: SignedEnvelopeVersion,
                Summary: "Secure envelope signing",
                Detail: $"Requests will be signed with HMAC-SHA256 using key '{configuredOptions.LocalKeyId ?? DefaultKeyId}' and expire after {requestExpiryMinutes} minute{(requestExpiryMinutes == 1 ? string.Empty : "s")}.",
                KeyId: configuredOptions.LocalKeyId ?? DefaultKeyId,
                RequestExpiryMinutes: requestExpiryMinutes);
        }

        if (configuredOptions.CompatibilityModeEnabled == true)
        {
            return new RemotePinOutgoingSecuritySnapshot(
                CanCreateRequests: true,
                UsesCompatibilityMode: true,
                EnvelopeVersion: 1,
                Summary: "Compatibility mode",
                Detail: "Requests will be exported as unsigned legacy envelopes and require receiver-side compatibility mode.",
                KeyId: null,
                RequestExpiryMinutes: requestExpiryMinutes);
        }

        return new RemotePinOutgoingSecuritySnapshot(
            CanCreateRequests: false,
            UsesCompatibilityMode: false,
            EnvelopeVersion: SignedEnvelopeVersion,
            Summary: "Remote pin signing is not configured",
            Detail: $"Configure '{RemotePinSecurityOptions.SectionName}:LocalSharedSecret' or explicitly re-enable compatibility mode before sending or exporting requests.",
            KeyId: configuredOptions.LocalKeyId,
            RequestExpiryMinutes: requestExpiryMinutes);
    }

    public RemotePinRequestEnvelope PrepareOutgoingEnvelope(RemotePinRequestEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvelopeShape(request);

        var configuredOptions = options.Value;
        if (string.IsNullOrWhiteSpace(configuredOptions.LocalSharedSecret))
        {
            if (configuredOptions.CompatibilityModeEnabled == true)
            {
                return request.WithSecurity(
                    version: 1,
                    senderId: null,
                    keyId: null,
                    expiresAtUtc: null,
                    nonce: null,
                    signatureAlgorithm: null,
                    signature: null);
            }

            throw new InvalidOperationException(
                $"Remote pin compatibility mode is disabled and no local signing secret is configured. Set '{RemotePinSecurityOptions.SectionName}:LocalSharedSecret' or explicitly enable '{RemotePinSecurityOptions.SectionName}:CompatibilityModeEnabled' only for transitional compatibility.");
        }

        var senderId = ResolveSenderId(request);
        if (string.IsNullOrWhiteSpace(senderId))
        {
            throw new InvalidOperationException("A sender identity is required to create a signed remote pin request.");
        }

        var signedRequest = request.WithSecurity(
            version: SignedEnvelopeVersion,
            senderId: senderId,
            keyId: configuredOptions.LocalKeyId ?? DefaultKeyId,
            expiresAtUtc: request.RequestedAtUtc.AddMinutes(Math.Max(1, configuredOptions.RequestExpiryMinutes)),
            nonce: Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            signatureAlgorithm: HmacSha256Algorithm,
            signature: null);

        var signature = Convert.ToBase64String(ComputeSignatureBytes(signedRequest, configuredOptions.LocalSharedSecret));
        return signedRequest.WithSecurity(
            version: signedRequest.Version,
            senderId: signedRequest.SenderId,
            keyId: signedRequest.KeyId,
            expiresAtUtc: signedRequest.ExpiresAtUtc,
            nonce: signedRequest.Nonce,
            signatureAlgorithm: signedRequest.SignatureAlgorithm,
            signature: signature);
    }

    public RemotePinValidationResult ValidateIncomingEnvelope(
        RemotePinRequestEnvelope request,
        IReadOnlyList<StoredRemotePinRequest> existingRequests)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvelopeShape(request);

        existingRequests ??= [];
        var configuredOptions = options.Value;
        if (!ContainsSecureEnvelopeMetadata(request))
        {
            if (configuredOptions.CompatibilityModeEnabled != true)
            {
                return Reject("Unsigned remote pin requests are disabled. Enable compatibility mode only for transitional compatibility.", shouldPersistRejectedRequest: true);
            }

            return Accept(
                RemotePinSecurityDisposition.Compatibility,
                "Accepted via compatibility mode without envelope signing.",
                trustedSenderLabel: null);
        }

        if (request.Version < SignedEnvelopeVersion)
        {
            return Reject("Remote pin security metadata requires envelope version 2 or later.", shouldPersistRejectedRequest: true);
        }

        var senderId = ResolveSenderId(request);
        if (string.IsNullOrWhiteSpace(senderId))
        {
            return Reject("A signed remote pin request must declare a sender identity.", shouldPersistRejectedRequest: true);
        }

        if (!string.Equals(senderId, request.Sender.PeerId, StringComparison.Ordinal))
        {
            return Reject("The signed sender identity does not match the sender peer ID.", shouldPersistRejectedRequest: true);
        }

        if (string.IsNullOrWhiteSpace(request.KeyId))
        {
            return Reject("A signed remote pin request must include a key ID.", shouldPersistRejectedRequest: true);
        }

        if (!request.ExpiresAtUtc.HasValue)
        {
            return Reject("A signed remote pin request must include an expiry timestamp.", shouldPersistRejectedRequest: true);
        }

        if (string.IsNullOrWhiteSpace(request.Nonce))
        {
            return Reject("A signed remote pin request must include a nonce.", shouldPersistRejectedRequest: true);
        }

        if (string.IsNullOrWhiteSpace(request.Signature))
        {
            return Reject("A signed remote pin request must include a signature.", shouldPersistRejectedRequest: true);
        }

        if (!string.Equals(request.SignatureAlgorithm, HmacSha256Algorithm, StringComparison.OrdinalIgnoreCase))
        {
            return Reject($"The remote pin signature algorithm '{request.SignatureAlgorithm}' is not supported.", shouldPersistRejectedRequest: true);
        }

        if (existingRequests.Any(item => string.Equals(item.Request.RequestId, request.RequestId, StringComparison.Ordinal)))
        {
            return Reject("A remote pin request with this request ID already exists.", shouldPersistRejectedRequest: false);
        }

        if (existingRequests.Any(item =>
                !string.IsNullOrWhiteSpace(item.Request.Nonce)
                && string.Equals(item.Request.Nonce, request.Nonce, StringComparison.Ordinal)))
        {
            return Reject("A remote pin request with this nonce was already processed.", shouldPersistRejectedRequest: false);
        }

        var clockSkew = TimeSpan.FromSeconds(Math.Max(0, configuredOptions.AllowedClockSkewSeconds));
        var now = DateTimeOffset.UtcNow;
        if (request.RequestedAtUtc - clockSkew > now)
        {
            return Reject("The remote pin request timestamp is in the future.", shouldPersistRejectedRequest: true);
        }

        if (request.ExpiresAtUtc.Value <= request.RequestedAtUtc)
        {
            return Reject("The remote pin request expiry must be later than its creation timestamp.", shouldPersistRejectedRequest: true);
        }

        if (request.ExpiresAtUtc.Value + clockSkew < now)
        {
            return Reject("The remote pin request has expired.", shouldPersistRejectedRequest: true);
        }

        var trustedSender = configuredOptions.TrustedSenders.FirstOrDefault(sender =>
            string.Equals(sender.SenderId, senderId, StringComparison.Ordinal)
            && string.Equals(sender.KeyId ?? DefaultKeyId, request.KeyId, StringComparison.Ordinal));
        if (trustedSender is null || string.IsNullOrWhiteSpace(trustedSender.SharedSecret))
        {
            return Reject($"Sender '{senderId}' is not trusted for remote pin requests.", shouldPersistRejectedRequest: true);
        }

        if (!TryDecodeSignature(request.Signature, out var providedSignatureBytes))
        {
            return Reject("The remote pin signature is not valid Base64.", shouldPersistRejectedRequest: true);
        }

        var expectedSignatureBytes = ComputeSignatureBytes(
            request.WithSecurity(
                request.Version,
                request.SenderId,
                request.KeyId,
                request.ExpiresAtUtc,
                request.Nonce,
                request.SignatureAlgorithm,
                signature: null),
            trustedSender.SharedSecret);
        if (!CryptographicOperations.FixedTimeEquals(expectedSignatureBytes, providedSignatureBytes))
        {
            return Reject("Remote pin signature verification failed.", shouldPersistRejectedRequest: true);
        }

        return Accept(
            RemotePinSecurityDisposition.Verified,
            $"Verified sender '{trustedSender.Label ?? senderId}' using HMAC key '{request.KeyId}'.",
            trustedSender.Label ?? senderId);
    }

    private static RemotePinValidationResult Accept(
        RemotePinSecurityDisposition disposition,
        string message,
        string? trustedSenderLabel)
        => new(
            IsAccepted: true,
            ShouldPersistRejectedRequest: false,
            Disposition: disposition,
            Message: message,
            TrustedSenderLabel: trustedSenderLabel);

    private static RemotePinValidationResult Reject(string message, bool shouldPersistRejectedRequest)
        => new(
            IsAccepted: false,
            ShouldPersistRejectedRequest: shouldPersistRejectedRequest,
            Disposition: RemotePinSecurityDisposition.Rejected,
            Message: message,
            TrustedSenderLabel: null);

    private static bool ContainsSecureEnvelopeMetadata(RemotePinRequestEnvelope request)
        => request.Version >= SignedEnvelopeVersion
           || !string.IsNullOrWhiteSpace(request.SenderId)
           || !string.IsNullOrWhiteSpace(request.KeyId)
           || request.ExpiresAtUtc.HasValue
           || !string.IsNullOrWhiteSpace(request.Nonce)
           || !string.IsNullOrWhiteSpace(request.SignatureAlgorithm)
           || !string.IsNullOrWhiteSpace(request.Signature);

    private static byte[] ComputeSignatureBytes(RemotePinRequestEnvelope request, string sharedSecret)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(RemotePinRequestContractSerializer.SerializeSigningPayload(request));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
        return hmac.ComputeHash(payloadBytes);
    }

    private static string? ResolveSenderId(RemotePinRequestEnvelope request)
        => string.IsNullOrWhiteSpace(request.SenderId)
            ? request.Sender.PeerId?.Trim()
            : request.SenderId.Trim();

    private static bool TryDecodeSignature(string signature, out byte[] signatureBytes)
    {
        try
        {
            signatureBytes = Convert.FromBase64String(signature);
            return true;
        }
        catch (FormatException)
        {
            signatureBytes = [];
            return false;
        }
    }

    private static void ValidateEnvelopeShape(RemotePinRequestEnvelope request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestId);
        ArgumentNullException.ThrowIfNull(request.Sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Sender.PeerId);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content.Cid);
    }
}
