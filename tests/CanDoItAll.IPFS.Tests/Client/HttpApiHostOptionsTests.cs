#nullable enable

using System.Collections.Generic;
using Ipfs.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ipfs.Engine.ClientTests
{
    [TestClass]
    public sealed class HttpApiHostOptionsTests
    {
        [TestMethod]
        public void Light_Mode_Defaults_Preserve_Anonymous_Access_And_Permissive_Cors()
        {
            var options = BindOptions(new Dictionary<string, string?>());

            Assert.AreEqual(HttpApiHostProfileMode.Light, options.Mode);
            Assert.AreEqual(false, options.RequireAuthentication);
            Assert.AreEqual(true, options.AllowAnyOrigin);
            CollectionAssert.AreEqual(
                new[]
                {
                    "X-Stream-Output",
                    "X-Chunked-Output",
                    "X-Content-Length"
                },
                options.ExposedHeaders);
        }

        [TestMethod]
        public void Pro_Mode_Defaults_Require_Admin_Key_And_Origin_Allowlist()
        {
            var options = BindOptions(new Dictionary<string, string?>
            {
                [$"{HttpApiHostOptions.SectionName}:Mode"] = "Pro"
            });

            Assert.AreEqual(HttpApiHostProfileMode.Pro, options.Mode);
            Assert.AreEqual(true, options.RequireAuthentication);
            Assert.AreEqual(false, options.AllowAnyOrigin);
            Assert.AreEqual(0, options.AllowedOrigins.Length);
        }

        private static HttpApiHostOptions BindOptions(IReadOnlyDictionary<string, string?> values)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var options = new HttpApiHostOptions();
            configuration.GetSection(HttpApiHostOptions.SectionName).Bind(options);
            new HttpApiHostOptionsSetup().PostConfigure(name: null, options);
            return options;
        }
    }
}
