#nullable enable

using System;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class RemotePinRequestContractSerializerTests
{
    [TestMethod]
    public void Serialize_And_Deserialize_Roundtrip_Version_Two_Metadata()
    {
        var envelope = new RemotePinRequestEnvelope
        {
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Version = RemotePinRequestSecurityService.SignedEnvelopeVersion,
            SenderId = "12D3KooWSender",
            KeyId = "sender-key",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15),
            Nonce = "ABC123",
            SignatureAlgorithm = RemotePinRequestSecurityService.HmacSha256Algorithm,
            Signature = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
            Note = "serializer proof",
            Sender = new RemotePinSenderSnapshot(
                "Sender node",
                "http://127.0.0.1:5092/",
                "http://127.0.0.1:5001/",
                "12D3KooWSender",
                ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
            Content = new RemotePinContentSnapshot(
                "/ipfs/bafy-serializer",
                "bafy-serializer",
                "serializer-proof.txt",
                IsDirectory: false,
                Size: 256,
                ChildCount: 0)
        };

        var json = RemotePinRequestContractSerializer.Serialize(envelope);
        var roundtrip = RemotePinRequestContractSerializer.Deserialize(json);

        Assert.AreEqual(envelope.Version, roundtrip.Version);
        Assert.AreEqual(envelope.SenderId, roundtrip.SenderId);
        Assert.AreEqual(envelope.KeyId, roundtrip.KeyId);
        Assert.AreEqual(envelope.ExpiresAtUtc, roundtrip.ExpiresAtUtc);
        Assert.AreEqual(envelope.Nonce, roundtrip.Nonce);
        Assert.AreEqual(envelope.SignatureAlgorithm, roundtrip.SignatureAlgorithm);
        Assert.AreEqual(envelope.Signature, roundtrip.Signature);
        Assert.AreEqual(envelope.Content.Cid, roundtrip.Content.Cid);
    }
}
