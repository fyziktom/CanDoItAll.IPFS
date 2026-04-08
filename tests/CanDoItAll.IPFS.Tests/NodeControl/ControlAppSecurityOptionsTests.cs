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
public sealed class ControlAppSecurityOptionsTests
{
    [TestMethod]
    public void AddIpfsNodeControlApplication_Defaults_Control_App_Security_For_Light_Mode()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<ControlAppSecurityOptions>>().Value;

        Assert.IsNull(options.AdminAccessKey);
        Assert.IsNull(options.RemotePinAccessKey);
        Assert.AreEqual(true, options.AllowAnonymousLocalAdmin);
        Assert.AreEqual(true, options.AllowAnonymousLocalRemotePin);
        Assert.AreEqual(60, options.AdminPermitLimit);
        Assert.AreEqual(12, options.RemotePinPermitLimit);
        Assert.AreEqual(60, options.RateLimitWindowSeconds);
    }

    [TestMethod]
    public void AddIpfsNodeControlApplication_Binds_Pro_Mode_Security_Defaults()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["OperatingProfile:Mode"] = "Pro",
            ["NodeSettingsDefaults:BaseUrl"] = "http://127.0.0.1:5001/",
            ["NodeSettingsDefaults:ApiPath"] = "api/v0",
            ["NodeSettingsDefaults:TimeoutSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<ControlAppSecurityOptions>>().Value;

        Assert.AreEqual(false, options.AllowAnonymousLocalAdmin);
        Assert.AreEqual(false, options.AllowAnonymousLocalRemotePin);
        Assert.AreEqual(60, options.AdminPermitLimit);
        Assert.AreEqual(12, options.RemotePinPermitLimit);
        Assert.AreEqual(60, options.RateLimitWindowSeconds);
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
