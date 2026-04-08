using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ipfs.Engine;

namespace Ipfs.Server
{
    /// <summary>
    ///   Hosts the IPFS HTTP API using the engine library.
    /// </summary>
    public static class Program
    {
        private static readonly CancellationTokenSource cancel = new();

        /// <summary>
        ///   The IPFS Core API engine used by the HTTP host.
        /// </summary>
        public static IpfsEngine? IpfsEngine { get; private set; }

        /// <summary>
        ///   Main entry point for daemon-style hosting.
        /// </summary>
        public static async Task Main(string[] args)
        {
            try
            {
                var requestedApiUrl = GetRequestedApiUrl();
                IpfsEngine ??= HttpApiHostPassphraseResolver.CreateEngine(BuildHostOptions(args));
                var urls = await GetUrlsAsync(requestedApiUrl).ConfigureAwait(false);
                using var host = CreateHostBuilder(args, urls).Build();
                await host.RunAsync(cancel.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // expected on shutdown
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static async Task<string> GetUrlsAsync(string? requestedApiUrl)
        {
            if (!string.IsNullOrWhiteSpace(requestedApiUrl))
            {
                await IpfsEngine!.Config.SetAsync("Addresses.API", ToMultiAddress(requestedApiUrl)).ConfigureAwait(false);
                return requestedApiUrl;
            }

            var urls = "http://127.0.0.1:5001";
            var addrToken = await IpfsEngine!.Config.GetAsync("Addresses.API").ConfigureAwait(false);
            var addr = addrToken?.ToString();
            if (!string.IsNullOrWhiteSpace(addr))
            {
                urls = addr
                    .Replace("/ip4/", "http://")
                    .Replace("/ip6/", "http://")
                    .Replace("/tcp/", ":");
            }

            return urls;
        }

        private static string? GetRequestedApiUrl()
        {
            var candidate = Environment.GetEnvironmentVariable("IPFS_NODE_API_URL");
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
            {
                return null;
            }

            var builder = new UriBuilder(parsed)
            {
                Path = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty
            };

            return builder.Uri.GetLeftPart(UriPartial.Authority);
        }

        private static string ToMultiAddress(string url)
        {
            var uri = new Uri(url, UriKind.Absolute);
            var host = uri.Host;

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                host = "127.0.0.1";
            }

            if (!IPAddress.TryParse(host, out var address))
            {
                throw new InvalidOperationException($"The requested IPFS API URL host '{uri.Host}' is not a direct IP address.");
            }

            var hostSegment = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"/ip6/{address}"
                : $"/ip4/{address}";

            return $"{hostSegment}/tcp/{uri.Port}";
        }

        private static HttpApiHostOptions BuildHostOptions(string[] args)
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var options = new HttpApiHostOptions();
            configuration.GetSection(HttpApiHostOptions.SectionName).Bind(options);
            new HttpApiHostOptionsSetup().PostConfigure(name: null, options);
            return options;
        }

        private static IHostBuilder CreateHostBuilder(string[] args, string urls)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        options.AllowSynchronousIO = true;
                    });
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls(urls);
                });
        }

        /// <summary>
        ///   Stop the program.
        /// </summary>
        public static void Shutdown()
        {
            cancel.Cancel();
        }
    }
}
