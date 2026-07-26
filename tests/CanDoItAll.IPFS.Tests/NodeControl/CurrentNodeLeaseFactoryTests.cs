#nullable enable

using System;
using System.Threading.Tasks;
using Ipfs.Engine.ClientTests;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class CurrentNodeLeaseFactoryTests
{
    [TestMethod]
    public async Task CreateLease_Uses_A_Healthy_Local_Target_And_Disposes_Its_HttpClient()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var targetRegistry = CreateRegistry(host.BaseAddress.ToString(), timeoutSeconds: 1);
        var factory = CreateFactory(targetRegistry);

        var lease = await factory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi).ConfigureAwait(false);
        try
        {
            Assert.AreEqual(host.BaseAddress, lease.HttpClient.BaseAddress);
            Assert.AreEqual(5, (int)lease.HttpClient.Timeout.TotalSeconds);
            Assert.AreEqual("api/v0", lease.Settings.ApiPath);

            var version = await lease.Client.Generic.VersionAsync().ConfigureAwait(false);
            Assert.IsTrue(version.ContainsKey("Version"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(version["Version"]));
        }
        finally
        {
            lease.Dispose();
        }

        await ThrowsAsync<ObjectDisposedException>(() => lease.HttpClient.GetAsync(string.Empty)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CreateLease_Skips_Local_Bootstrap_For_Remote_Targets()
    {
        var targetRegistry = CreateRegistry("http://203.0.113.10:5001/", timeoutSeconds: 15);
        var factory = CreateFactory(targetRegistry);

        using var lease = await factory.CreateLeaseAsync(NodeConnectionRequestCategory.ReadOnlyUi).ConfigureAwait(false);

        Assert.AreEqual(new Uri("http://203.0.113.10:5001/"), lease.HttpClient.BaseAddress);
        Assert.AreEqual("http://203.0.113.10:5001/", lease.Settings.BaseUrl);
        Assert.AreEqual(15, lease.Settings.TimeoutSeconds);
    }

    [TestMethod]
    public async Task CreateLeaseWithMinimumTimeoutSeconds_Raises_The_Lease_Timeout_Without_Mutating_The_Registry()
    {
        await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
        var targetRegistry = CreateRegistry(host.BaseAddress.ToString(), timeoutSeconds: 15);
        var factory = CreateFactory(targetRegistry);

        using var lease = await factory.CreateLeaseWithMinimumTimeoutSecondsAsync(
            120,
            NodeConnectionRequestCategory.ReadOnlyUi).ConfigureAwait(false);

        Assert.AreEqual(120, lease.Settings.TimeoutSeconds);
        Assert.AreEqual(15, targetRegistry.Current.TimeoutSeconds);
    }

    private static CurrentNodeTargetRegistry CreateRegistry(string baseUrl, int timeoutSeconds)
    {
        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(new NodeConnectionSettings
        {
            Label = "Test node",
            BaseUrl = baseUrl,
            ApiPath = "api/v0",
            TimeoutSeconds = timeoutSeconds
        }, isHydrated: true);
        return targetRegistry;
    }

    private static CurrentNodeLeaseFactory CreateFactory(CurrentNodeTargetRegistry targetRegistry)
        => new(
            targetRegistry,
            new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance));

    private static async Task<T> ThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (T ex)
        {
            return ex;
        }

        Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
        return null!;
    }
}
