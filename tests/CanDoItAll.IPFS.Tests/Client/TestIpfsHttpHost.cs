#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Ipfs;
using Ipfs.Engine;
using Ipfs.Engine.Client;
using Ipfs.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ipfs.Engine.ClientTests
{
    internal sealed class TestIpfsHttpHost : IAsyncDisposable
    {
        private TestIpfsHttpHost(TempNode node, IHost host, HttpClient httpClient, IpfsEngineClient client, Uri baseAddress)
        {
            Node = node;
            Host = host;
            HttpClient = httpClient;
            Client = client;
            BaseAddress = baseAddress;
        }

        public TempNode Node { get; }

        public IHost Host { get; }

        public HttpClient HttpClient { get; }

        public IpfsEngineClient Client { get; }

        public Uri BaseAddress { get; }

        public static async Task<TestIpfsHttpHost> StartAsync(
            IReadOnlyDictionary<string, string?>? overrides = null,
            params (string Name, string Value)[] defaultHeaders)
        {
            var node = new TempNode();
            node.Options.Discovery.DisableMdns = true;
            node.Options.Discovery.DisableRandomWalk = true;
            await node.Bootstrap.RemoveAllAsync().ConfigureAwait(false);
            await node.StartAsync().ConfigureAwait(false);

            var port = GetUnusedPort();
            var baseAddress = new Uri($"http://127.0.0.1:{port}/");

            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        options.AllowSynchronousIO = true;
                    });
                    webBuilder.UseUrls(baseAddress.ToString());
                    webBuilder.ConfigureAppConfiguration((_, config) =>
                    {
                        if (overrides?.Count > 0)
                        {
                            config.AddInMemoryCollection(overrides);
                        }
                    });
                    webBuilder.ConfigureServices((context, services) =>
                    {
                        services.AddIpfsHttpApiHost(context.Configuration, _ => node);
                    });
                    webBuilder.Configure(app =>
                    {
                        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                        app.UseIpfsHttpApiHost(env);
                    });
                })
                .Build();

            await host.StartAsync().ConfigureAwait(false);

            var httpClient = new HttpClient
            {
                BaseAddress = baseAddress,
                Timeout = Timeout.InfiniteTimeSpan
            };
            foreach (var (name, value) in defaultHeaders)
            {
                httpClient.DefaultRequestHeaders.Remove(name);
                httpClient.DefaultRequestHeaders.Add(name, value);
            }

            var client = new IpfsEngineClient(httpClient);
            return new TestIpfsHttpHost(node, host, httpClient, client, baseAddress);
        }

        public async ValueTask DisposeAsync()
        {
            HttpClient.Dispose();
            await Host.StopAsync().ConfigureAwait(false);
            Host.Dispose();
            await Node.StopAsync().ConfigureAwait(false);
            Node.Dispose();
        }

        public static async Task<MultiAddress> GetDialAddressAsync(IpfsEngine node)
        {
            var peer = await node.Generic.IdAsync().ConfigureAwait(false);
            var address = peer.Addresses.FirstOrDefault(a => a.ToString().Contains("/127.0.0.1/", StringComparison.Ordinal))
                ?? peer.Addresses.First();

            return address.HasPeerId
                ? address
                : address.WithPeerId(peer.Id);
        }

        private static int GetUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
