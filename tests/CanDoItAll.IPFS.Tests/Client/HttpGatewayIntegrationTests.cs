#nullable enable

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ipfs.Engine.ClientTests
{
    [TestClass]
    public sealed class HttpGatewayIntegrationTests
    {
        [TestMethod]
        public async Task Gateway_Serves_Content_By_Cid_Inline_With_Detected_Media_Type()
        {
            await using var host = await TestIpfsHttpHost.StartAsync().ConfigureAwait(false);
            byte[] png =
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
            ];
            await using var source = new MemoryStream(png);
            var node = await host.Node.FileSystem.AddAsync(source, "account-image.png").ConfigureAwait(false);

            using var response = await host.HttpClient
                .GetAsync($"ipfs/{node.Id}", HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.IsNull(response.Content.Headers.ContentDisposition);
            Assert.AreEqual($"\"{node.Id}\"", response.Headers.ETag?.Tag);
            Assert.IsTrue(response.Headers.CacheControl?.Public);
            Assert.IsTrue(response.Headers.CacheControl?.Extensions.Any(extension =>
                string.Equals(extension.Name, "immutable", System.StringComparison.OrdinalIgnoreCase)));
            CollectionAssert.AreEqual(png, await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
        }
    }
}
