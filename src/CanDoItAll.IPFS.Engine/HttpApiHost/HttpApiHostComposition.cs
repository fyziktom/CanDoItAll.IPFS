using System;
using System.IO;
using Ipfs.CoreApi;
using Ipfs.Engine;
using Ipfs.Server.HttpApi.V0;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Ipfs.Server
{
    public static class HttpApiHostComposition
    {
        public static IServiceCollection AddIpfsHttpApiHost(
            this IServiceCollection services,
            IConfiguration configuration,
            Func<IServiceProvider, IpfsEngine> ipfsEngineFactory)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(ipfsEngineFactory);

            services.AddOptions<HttpApiHostOptions>()
                .Bind(configuration.GetSection(HttpApiHostOptions.SectionName))
                .Validate(
                    options => options.RequireAuthentication != true || !string.IsNullOrWhiteSpace(options.AdminAccessKey),
                    $"'{HttpApiHostOptions.SectionName}:AdminAccessKey' is required when authentication is enabled.")
                .ValidateOnStart();
            services.AddSingleton<IPostConfigureOptions<HttpApiHostOptions>, HttpApiHostOptionsSetup>();
            services.AddSingleton(ipfsEngineFactory);
            services.AddSingleton<ICoreApi>(serviceProvider => serviceProvider.GetRequiredService<IpfsEngine>());
            services.AddHostedService<IpfsEngineHostedService>();

            services.AddAuthentication(HttpApiHostAuthenticationSchemes.ApiKey)
                .AddScheme<AuthenticationSchemeOptions, HttpApiHostApiKeyAuthenticationHandler>(
                    HttpApiHostAuthenticationSchemes.ApiKey,
                    static _ => { });

            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder(HttpApiHostAuthenticationSchemes.ApiKey)
                    .AddRequirements(new HttpApiHostAccessRequirement())
                    .Build());
            services.AddSingleton<IAuthorizationHandler, HttpApiHostAccessHandler>();

            var hostOptions = BindHostOptions(configuration);
            services.AddCors(options =>
            {
                options.AddPolicy(HttpApiHostCorsPolicyNames.Default, policy =>
                {
                    policy.AllowAnyHeader()
                        .AllowAnyMethod();

                    if (hostOptions.ExposedHeaders.Length > 0)
                    {
                        policy.WithExposedHeaders(hostOptions.ExposedHeaders);
                    }

                    if (hostOptions.AllowAnyOrigin == true)
                    {
                        policy.AllowAnyOrigin();
                    }
                    else if (hostOptions.AllowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(hostOptions.AllowedOrigins);
                    }
                });
            });

            services.AddControllers()
                .AddApplicationPart(typeof(GenericController).Assembly)
                .AddNewtonsoftJson();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v0", new OpenApiInfo
                {
                    Title = "IPFS HTTP API",
                    Description = "The API for interacting with IPFS nodes.",
                    Version = "v0"
                });

                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                path = Path.ChangeExtension(path, ".xml");
                if (File.Exists(path))
                {
                    c.IncludeXmlComments(path);
                }
            });

            return services;
        }

        public static IApplicationBuilder UseIpfsHttpApiHost(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(env);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseCors(HttpApiHostCorsPolicyNames.Default);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v0/swagger.json", "IPFS HTTP API");
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            return app;
        }

        private static HttpApiHostOptions BindHostOptions(IConfiguration configuration)
        {
            var options = new HttpApiHostOptions();
            configuration.GetSection(HttpApiHostOptions.SectionName).Bind(options);
            new HttpApiHostOptionsSetup().PostConfigure(name: null, options);
            return options;
        }
    }
}
