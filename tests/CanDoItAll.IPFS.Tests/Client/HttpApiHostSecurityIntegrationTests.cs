#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ipfs.Server;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ipfs.Engine.ClientTests
{
    [TestClass]
    public sealed class HttpApiHostSecurityIntegrationTests
    {
        [TestMethod]
        public async Task Pro_Mode_Requires_An_Admin_Key_During_Host_Startup()
        {
            await ThrowsAsync<OptionsValidationException>(() =>
                RealStartupIpfsHttpHost.StartAsync(new Dictionary<string, string?>
                {
                    [$"{HttpApiHostOptions.SectionName}:Mode"] = "Pro"
                })).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task Pro_Mode_Rejects_Anonymous_Core_Api_Requests()
        {
            await using var host = await RealStartupIpfsHttpHost.StartAsync(CreateProOverrides()).ConfigureAwait(false);

            using var response = await host.HttpClient.GetAsync("api/v0/version").ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Pro_Mode_Admin_Key_Preserves_Core_Client_Operations()
        {
            await using var host = await RealStartupIpfsHttpHost.StartAsync(
                CreateProOverrides(),
                (HttpApiHostSecurityHeaders.AdminAccessKey, "admin-secret")).ConfigureAwait(false);

            var version = await host.Client.Generic.VersionAsync().ConfigureAwait(false);
            var peer = await host.Client.Generic.IdAsync().ConfigureAwait(false);

            Assert.IsTrue(version.Count > 0);
            Assert.IsNotNull(peer.Id);
            Assert.IsTrue(peer.Addresses.Any());
        }

        [TestMethod]
        public async Task Light_Mode_Allows_Anonymous_Cors_Preflight_From_Any_Origin()
        {
            await using var host = await RealStartupIpfsHttpHost.StartAsync().ConfigureAwait(false);

            using var response = await host.HttpClient.SendAsync(CreatePreflightRequest("https://browser.example")).ConfigureAwait(false);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }

        [TestMethod]
        public async Task Pro_Mode_Cors_Allows_Configured_Origins_And_Omits_Others()
        {
            await using var host = await RealStartupIpfsHttpHost.StartAsync(new Dictionary<string, string?>
            {
                [$"{HttpApiHostOptions.SectionName}:Mode"] = "Pro",
                [$"{HttpApiHostOptions.SectionName}:AdminAccessKey"] = "admin-secret",
                [$"{HttpApiHostOptions.SectionName}:AllowedOrigins:0"] = "https://allowed.example"
            }).ConfigureAwait(false);

            using var allowed = await host.HttpClient.SendAsync(CreatePreflightRequest("https://allowed.example")).ConfigureAwait(false);
            using var blocked = await host.HttpClient.SendAsync(CreatePreflightRequest("https://blocked.example")).ConfigureAwait(false);

            Assert.IsTrue(allowed.IsSuccessStatusCode);
            Assert.AreEqual("https://allowed.example", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.IsFalse(blocked.Headers.Contains("Access-Control-Allow-Origin"));
        }

        private static IReadOnlyDictionary<string, string?> CreateProOverrides()
            => new Dictionary<string, string?>
            {
                [$"{HttpApiHostOptions.SectionName}:Mode"] = "Pro",
                [$"{HttpApiHostOptions.SectionName}:AdminAccessKey"] = "admin-secret"
            };

        private static HttpRequestMessage CreatePreflightRequest(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "api/v0/version");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", HttpApiHostSecurityHeaders.AdminAccessKey);
            return request;
        }

        private static async Task<T> ThrowsAsync<T>(Func<Task> action)
            where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T error)
            {
                return error;
            }

            Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
            return null!;
        }
    }
}
