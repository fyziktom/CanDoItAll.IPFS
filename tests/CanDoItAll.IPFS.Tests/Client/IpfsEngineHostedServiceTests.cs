#nullable enable

using System.Threading.Tasks;
using Ipfs.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ipfs.Engine.ClientTests
{
    [TestClass]
    public sealed class IpfsEngineHostedServiceTests
    {
        [TestMethod]
        public async Task Hosted_Service_Start_And_Stop_Are_Idempotent()
        {
            using var node = new TempNode();
            var service = new IpfsEngineHostedService(node, NullLogger<IpfsEngineHostedService>.Instance);

            await service.StartAsync(default).ConfigureAwait(false);
            await service.StartAsync(default).ConfigureAwait(false);

            Assert.IsTrue(node.IsStarted);

            await service.StopAsync(default).ConfigureAwait(false);
            await service.StopAsync(default).ConfigureAwait(false);

            Assert.IsFalse(node.IsStarted);
        }

        [TestMethod]
        public async Task Hosted_Service_Does_Not_Fail_When_Engine_Is_Already_Started()
        {
            using var node = new TempNode();
            await node.StartAsync().ConfigureAwait(false);

            var service = new IpfsEngineHostedService(node, NullLogger<IpfsEngineHostedService>.Instance);

            await service.StartAsync(default).ConfigureAwait(false);
            Assert.IsTrue(node.IsStarted);

            await service.StopAsync(default).ConfigureAwait(false);
            Assert.IsFalse(node.IsStarted);
        }
    }
}
