using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipfs.Server
{
    /// <summary>
    ///   Startup steps for the reusable HTTP API host.
    /// </summary>
    internal class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddIpfsHttpApiHost(
                Configuration,
                _ => Program.IpfsEngine ?? throw new InvalidOperationException("The IPFS engine must be initialized before the HTTP host starts."));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseIpfsHttpApiHost(env);
        }
    }
}
