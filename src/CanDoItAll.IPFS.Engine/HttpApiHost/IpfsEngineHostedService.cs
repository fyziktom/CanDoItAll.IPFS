using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ipfs.Server
{
    public sealed class IpfsEngineHostedService : IHostedService
    {
        private readonly IpfsEngine ipfsEngine;
        private readonly ILogger<IpfsEngineHostedService> logger;

        public IpfsEngineHostedService(IpfsEngine ipfsEngine, ILogger<IpfsEngineHostedService> logger)
        {
            this.ipfsEngine = ipfsEngine;
            this.logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (ipfsEngine.IsStarted)
            {
                logger.LogInformation("Skipping engine startup because the node is already started.");
                return;
            }

            logger.LogInformation("Starting the embedded IPFS engine.");
            await ipfsEngine.StartAsync().ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!ipfsEngine.IsStarted)
            {
                logger.LogInformation("Skipping engine shutdown because the node is already stopped.");
                return;
            }

            logger.LogInformation("Stopping the embedded IPFS engine.");
            await ipfsEngine.StopAsync().ConfigureAwait(false);
        }
    }
}
