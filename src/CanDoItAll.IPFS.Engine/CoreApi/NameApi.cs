using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.CoreApi;

namespace Ipfs.Engine.CoreApi
{
    internal class NameApi : INameApi
    {
        private readonly IpfsEngine ipfs;
        private readonly FileStore<string, PublishedNameRecord> publishedNames;

        public NameApi(IpfsEngine ipfs)
        {
            this.ipfs = ipfs;
            publishedNames = new FileStore<string, PublishedNameRecord>(ipfs.Options.Repository.Folder, "names", FileStore<string, PublishedNameRecord>.InitSerialize.Json)
            {
                KeyToFileName = key => Encoding.UTF8.GetBytes(key).ToBase32(),
                FileNameToKey = fileName => Encoding.UTF8.GetString(Base32.Decode(fileName))
            };
        }

        public async Task<NamedContent> PublishAsync(string path, bool resolve = true, string key = "self", TimeSpan? lifetime = null, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path), "The content path is required.");
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key), "The key name is required.");

            var keyChain = await ipfs.KeyChainAsync().ConfigureAwait(false);
            var namedKey = await keyChain.FindKeyByNameAsync(key, cancel).ConfigureAwait(false);
            if (namedKey == null)
                throw new KeyNotFoundException($"The key '{key}' does not exist.");

            var contentPath = await NormalizePublishedPathAsync(path, resolve, cancel).ConfigureAwait(false);
            var nameId = namedKey.Id.ToString();
            var expiresUtc = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromHours(24));
            await publishedNames.PutAsync(nameId, new PublishedNameRecord
            {
                NameId = nameId,
                ContentPath = contentPath,
                ExpiresUtc = expiresUtc
            }, cancel).ConfigureAwait(false);

            return new NamedContent
            {
                NamePath = $"/ipns/{nameId}",
                ContentPath = contentPath
            };
        }

        public Task<NamedContent> PublishAsync(Cid id, string key = "self", TimeSpan? lifetime = null, CancellationToken cancel = default)
        {
            return PublishAsync($"/ipfs/{id}", resolve: true, key: key, lifetime: lifetime, cancel: cancel);
        }

        public async Task<string> ResolveAsync(string name, bool recursive = false, bool nocache = false, CancellationToken cancel = default)
        {
            do
            {
                if (name.StartsWith("/ipns/"))
                {
                    name = name[6..];
                }
                var parts = name.Split('/').Where(p => p.Length > 0).ToArray();
                if (parts.Length == 0)
                    throw new ArgumentException($"Cannot resolve '{name}'.");
                if (IsDomainName(parts[0]))
                {
                    name = await ipfs.Dns.ResolveAsync(parts[0], recursive, cancel).ConfigureAwait(false);
                }
                else
                {
                    var record = await publishedNames.TryGetAsync(parts[0], cancel).ConfigureAwait(false);
                    if (record == null)
                        throw new KeyNotFoundException($"The IPNS record '{parts[0]}' does not exist on this node.");
                    if (record.ExpiresUtc <= DateTimeOffset.UtcNow)
                    {
                        await publishedNames.RemoveAsync(parts[0], cancel).ConfigureAwait(false);
                        throw new KeyNotFoundException($"The IPNS record '{parts[0]}' has expired.");
                    }

                    name = record.ContentPath;
                }
                if (parts.Length > 1)
                {
                    name = name + "/" + string.Join("/", parts, 1, parts.Length - 1);
                }
            } while (recursive && !name.StartsWith("/ipfs/"));

            return name;
        }

        /// <summary>
        ///   Determines if the supplied string is a valid domain name.
        /// </summary>
        /// <param name="name">
        ///   An domain name, such as "ipfs.io".
        /// </param>
        /// <returns>
        ///   <b>true</b> if <paramref name="name"/> is a domain name;
        ///   otherwise, <b>false</b>.
        /// </returns>
        /// <remarks>
        ///    A domain must contain at least one '.'.
        /// </remarks>
        public static bool IsDomainName(string name)
        {
            return name.IndexOf('.') > 0;
        }

        private async Task<string> NormalizePublishedPathAsync(string path, bool resolve, CancellationToken cancel)
        {
            var trimmedPath = path.Trim();
            if (!resolve)
            {
                return trimmedPath.StartsWith("/ipfs/") || trimmedPath.StartsWith("/ipns/")
                    ? trimmedPath
                    : $"/ipfs/{trimmedPath}";
            }

            return await ipfs.Generic.ResolveAsync(trimmedPath, recursive: true, cancel).ConfigureAwait(false);
        }

        private sealed class PublishedNameRecord
        {
            public string NameId { get; set; } = string.Empty;

            public string ContentPath { get; set; } = string.Empty;

            public DateTimeOffset ExpiresUtc { get; set; }
        }
    }
}
