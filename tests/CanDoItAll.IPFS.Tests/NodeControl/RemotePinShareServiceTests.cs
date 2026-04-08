using System;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class RemotePinShareServiceTests
{
    [TestMethod]
    public void ShouldAllowPrivateNetworkCertificateBypass_ReturnsTrue_ForPrivateHttpsTargets()
    {
        var endpoints = new[]
        {
            "https://192.168.0.10:9443/",
            "https://10.0.0.12:9443/",
            "https://172.16.5.2:9443/",
            "https://localhost:9443/",
            "https://receiver.local:9443/"
        };

        foreach (var endpoint in endpoints)
        {
            var result = RemotePinShareService.ShouldAllowPrivateNetworkCertificateBypass(new Uri(endpoint, UriKind.Absolute));
            Assert.IsTrue(result, endpoint);
        }
    }

    [TestMethod]
    public void ShouldAllowPrivateNetworkCertificateBypass_ReturnsFalse_ForPublicOrNonHttpsTargets()
    {
        var endpoints = new[]
        {
            "http://192.168.0.10:5092/",
            "https://8.8.8.8:9443/",
            "https://example.com/"
        };

        foreach (var endpoint in endpoints)
        {
            var result = RemotePinShareService.ShouldAllowPrivateNetworkCertificateBypass(new Uri(endpoint, UriKind.Absolute));
            Assert.IsFalse(result, endpoint);
        }
    }
}
