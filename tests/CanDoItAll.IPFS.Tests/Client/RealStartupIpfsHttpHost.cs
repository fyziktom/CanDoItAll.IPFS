#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Ipfs;
using Ipfs.Engine;
using Ipfs.Engine.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ipfs.Engine.ClientTests
{
    internal sealed class RealStartupIpfsHttpHost : IAsyncDisposable
    {
        private static readonly Type ServerProgramType = typeof(Ipfs.Server.Program);
        private static readonly Type StartupType = ServerProgramType.Assembly.GetType("Ipfs.Server.Startup", throwOnError: true)!;
        private static readonly PropertyInfo IpfsEngineProperty = ServerProgramType.GetProperty("IpfsEngine", BindingFlags.Public | BindingFlags.Static)!;

        private readonly TempNode node;
        private readonly object? priorIpfsEngine;

        private RealStartupIpfsHttpHost(TempNode node, object? priorIpfsEngine, IHost host, HttpClient httpClient, IpfsEngineClient client, Uri baseAddress)
        {
            this.node = node;
            this.priorIpfsEngine = priorIpfsEngine;
            Host = host;
            HttpClient = httpClient;
            Client = client;
            BaseAddress = baseAddress;
        }

        public IHost Host { get; }

        public HttpClient HttpClient { get; }

        public IpfsEngineClient Client { get; }

        public Uri BaseAddress { get; }

        public static async Task<RealStartupIpfsHttpHost> StartAsync(
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
            var priorIpfsEngine = IpfsEngineProperty.GetValue(null);
            SetIpfsEngine(node);

            try
            {
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
                        webBuilder.UseStartup(StartupType);
                    })
                    .Build();

                await host.StartAsync().ConfigureAwait(false);

                var httpClient = new HttpClient
                {
                    BaseAddress = baseAddress,
                    Timeout = TimeSpan.FromSeconds(30)
                };
                foreach (var (name, value) in defaultHeaders)
                {
                    httpClient.DefaultRequestHeaders.Remove(name);
                    httpClient.DefaultRequestHeaders.Add(name, value);
                }

                var client = new IpfsEngineClient(httpClient);
                return new RealStartupIpfsHttpHost(node, priorIpfsEngine, host, httpClient, client, baseAddress);
            }
            catch
            {
                SetIpfsEngine(priorIpfsEngine);
                await node.StopAsync().ConfigureAwait(false);
                node.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            HttpClient.Dispose();
            await Host.StopAsync().ConfigureAwait(false);
            Host.Dispose();
            SetIpfsEngine(priorIpfsEngine);
            await node.StopAsync().ConfigureAwait(false);
            node.Dispose();
        }

        private static void SetIpfsEngine(object? value)
        {
            var setter = IpfsEngineProperty.SetMethod ?? throw new InvalidOperationException("Ipfs.Server.Program.IpfsEngine setter is unavailable.");
            setter.Invoke(null, new[] { value });
        }

        private static int GetUnusedPort()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
