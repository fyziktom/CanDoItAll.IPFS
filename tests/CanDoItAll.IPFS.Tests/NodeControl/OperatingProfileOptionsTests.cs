#nullable enable

using System.Collections.Generic;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class OperatingProfileOptionsTests
{
    [TestMethod]
    public void AddIpfsNodeControlApplication_Defaults_To_Light_Profile_With_Local_First_Compatibility()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<OperatingProfileOptions>>().Value;

        Assert.AreEqual(OperatingProfileMode.Light, options.Mode);
        Assert.AreEqual(true, options.PreferLocalNodeBootstrap);
        Assert.AreEqual(false, options.RequireAdminAuthentication);
        Assert.AreEqual(false, options.EnableRateLimiting);
        Assert.AreEqual(false, options.EnableStrictCertificateValidation);
        Assert.AreEqual(false, options.EnablePublishingHardening);
        Assert.AreEqual(false, options.EnableStructuredTelemetry);
        Assert.AreEqual(true, options.AllowLegacyRemotePinCompatibility);
    }

    [TestMethod]
    public void AddIpfsNodeControlApplication_Binds_Pro_Profile_Defaults()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["OperatingProfile:Mode"] = "Pro",
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<OperatingProfileOptions>>().Value;

        Assert.AreEqual(OperatingProfileMode.Pro, options.Mode);
        Assert.AreEqual(true, options.PreferLocalNodeBootstrap);
        Assert.AreEqual(true, options.RequireAdminAuthentication);
        Assert.AreEqual(true, options.EnableRateLimiting);
        Assert.AreEqual(true, options.EnableStrictCertificateValidation);
        Assert.AreEqual(true, options.EnablePublishingHardening);
        Assert.AreEqual(true, options.EnableStructuredTelemetry);
        Assert.AreEqual(false, options.AllowLegacyRemotePinCompatibility);
    }

    private static ServiceProvider CreateProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIpfsNodeControlApplication(configuration);
        return services.BuildServiceProvider();
    }
}
