#nullable enable

using System;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class RemotePinRequestSecurityServiceTests
{
    [TestMethod]
    public void PrepareOutgoingEnvelope_Signs_Version_Two_Request_When_Local_Shared_Secret_Is_Configured()
    {
        var service = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            LocalKeyId = "sender-key",
            LocalSharedSecret = "sender-secret",
            RequestExpiryMinutes = 10
        });

        var envelope = service.PrepareOutgoingEnvelope(CreateEnvelope());

        Assert.AreEqual(RemotePinRequestSecurityService.SignedEnvelopeVersion, envelope.Version);
        Assert.AreEqual("12D3KooWSender", envelope.SenderId);
        Assert.AreEqual("sender-key", envelope.KeyId);
        Assert.IsTrue(envelope.ExpiresAtUtc > envelope.RequestedAtUtc);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.Nonce));
        Assert.AreEqual(RemotePinRequestSecurityService.HmacSha256Algorithm, envelope.SignatureAlgorithm);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.Signature));
    }

    [TestMethod]
    public void ValidateIncomingEnvelope_Allows_Legacy_Request_Only_When_Compatibility_Mode_Is_Enabled()
    {
        var envelope = CreateEnvelope();

        var compatible = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = true
        });
        var compatibleResult = compatible.ValidateIncomingEnvelope(envelope, []);
        Assert.IsTrue(compatibleResult.IsAccepted);
        Assert.AreEqual(RemotePinSecurityDisposition.Compatibility, compatibleResult.Disposition);

        var strict = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false
        });
        var strictResult = strict.ValidateIncomingEnvelope(envelope, []);
        Assert.IsFalse(strictResult.IsAccepted);
        Assert.AreEqual(RemotePinSecurityDisposition.Rejected, strictResult.Disposition);
        StringAssert.Contains(strictResult.Message, "Unsigned");
    }

    [TestMethod]
    public void ValidateIncomingEnvelope_Rejects_Expired_Request()
    {
        var signer = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            LocalKeyId = "sender-key",
            LocalSharedSecret = "sender-secret",
            RequestExpiryMinutes = 1
        });
        var receiver = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            AllowedClockSkewSeconds = 0,
            TrustedSenders =
            [
                new RemotePinTrustedSenderOptions
                {
                    SenderId = "12D3KooWSender",
                    Label = "Sender node",
                    KeyId = "sender-key",
                    SharedSecret = "sender-secret"
                }
            ]
        });

        var envelope = signer.PrepareOutgoingEnvelope(CreateEnvelope(DateTimeOffset.UtcNow.AddMinutes(-5)));
        var result = receiver.ValidateIncomingEnvelope(envelope, []);

        Assert.IsFalse(result.IsAccepted);
        StringAssert.Contains(result.Message, "expired");
    }

    [TestMethod]
    public void ValidateIncomingEnvelope_Rejects_Replayed_Nonce()
    {
        var signer = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            LocalKeyId = "sender-key",
            LocalSharedSecret = "sender-secret",
            RequestExpiryMinutes = 10
        });
        var receiver = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            TrustedSenders =
            [
                new RemotePinTrustedSenderOptions
                {
                    SenderId = "12D3KooWSender",
                    Label = "Sender node",
                    KeyId = "sender-key",
                    SharedSecret = "sender-secret"
                }
            ]
        });

        var envelope = signer.PrepareOutgoingEnvelope(CreateEnvelope());
        var replayRecord = new StoredRemotePinRequest
        {
            Request = new RemotePinRequestEnvelope
            {
                RequestId = Guid.NewGuid().ToString("N"),
                RequestedAtUtc = envelope.RequestedAtUtc,
                Version = envelope.Version,
                SenderId = envelope.SenderId,
                KeyId = envelope.KeyId,
                ExpiresAtUtc = envelope.ExpiresAtUtc,
                Nonce = envelope.Nonce,
                SignatureAlgorithm = envelope.SignatureAlgorithm,
                Signature = envelope.Signature,
                Note = envelope.Note,
                Sender = envelope.Sender,
                Content = envelope.Content
            },
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };

        var result = receiver.ValidateIncomingEnvelope(envelope, [replayRecord]);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsFalse(result.ShouldPersistRejectedRequest);
        StringAssert.Contains(result.Message, "nonce");
    }

    [TestMethod]
    public void ValidateIncomingEnvelope_Rejects_Untrusted_Sender()
    {
        var signer = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false,
            LocalKeyId = "sender-key",
            LocalSharedSecret = "sender-secret",
            RequestExpiryMinutes = 10
        });
        var receiver = CreateService(new RemotePinSecurityOptions
        {
            CompatibilityModeEnabled = false
        });

        var envelope = signer.PrepareOutgoingEnvelope(CreateEnvelope());
        var result = receiver.ValidateIncomingEnvelope(envelope, []);

        Assert.IsFalse(result.IsAccepted);
        StringAssert.Contains(result.Message, "not trusted");
    }

    private static RemotePinRequestSecurityService CreateService(RemotePinSecurityOptions options)
        => new(Options.Create(options));

    private static RemotePinRequestEnvelope CreateEnvelope(DateTimeOffset? requestedAtUtc = null)
        => new()
        {
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = requestedAtUtc ?? DateTimeOffset.UtcNow,
            Note = "security proof",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092/",
                "http://127.0.0.1:5001/",
                "12D3KooWSender",
                ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
            Content = new RemotePinContentSnapshot(
                "/ipfs/bafy-security",
                "bafy-security",
                "security-proof.txt",
                IsDirectory: false,
                Size: 128,
                ChildCount: 0)
        };
}
