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
public sealed class GatewayPublishingOptionsTests
{
    [TestMethod]
    public void AddIpfsNodeControlApplication_Defaults_To_Preview_Mode_In_Light_Profile()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<GatewayPublishingOptions>>().Value;

        Assert.AreEqual(GatewayPublishingMode.Preview, options.Mode);
        Assert.AreEqual(true, options.EnableDirectoryListings);
        Assert.AreEqual(31536000, options.ImmutableFileMaxAgeSeconds);
        Assert.AreEqual(60, options.MutableContentMaxAgeSeconds);
    }

    [TestMethod]
    public void AddIpfsNodeControlApplication_Defaults_To_Publish_Mode_In_Pro_Profile()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["OperatingProfile:Mode"] = "Pro",
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<GatewayPublishingOptions>>().Value;

        Assert.AreEqual(GatewayPublishingMode.Publish, options.Mode);
        Assert.AreEqual(false, options.EnableDirectoryListings);
    }

    [TestMethod]
    public void AddIpfsNodeControlApplication_Allows_Explicit_Gateway_Mode_Overrides()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["OperatingProfile:Mode"] = "Pro",
            ["GatewayPublishing:Mode"] = "Preview",
            ["GatewayPublishing:EnableDirectoryListings"] = "true",
            ["GatewayPublishing:MutableContentMaxAgeSeconds"] = "120",
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<GatewayPublishingOptions>>().Value;

        Assert.AreEqual(GatewayPublishingMode.Preview, options.Mode);
        Assert.AreEqual(true, options.EnableDirectoryListings);
        Assert.AreEqual(120, options.MutableContentMaxAgeSeconds);
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
