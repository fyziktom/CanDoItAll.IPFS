using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.CoreApi;
using Ipfs.Engine.Client.Mapping;
using Ipfs.Engine.Client.Models;
using Ipfs.Engine.Client.Transport;

namespace Ipfs.Engine.Client.Operations
{
    public sealed class FileSystemClient : ApiClientBase, IFileSystemApi
    {
        internal FileSystemClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public async Task<IFileSystemNode> AddAsync(Stream stream, string name, AddFileOptions? options = null, CancellationToken cancel = default)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            options ??= new AddFileOptions();
            var query = BuildAddQuery(options);
            FileSystemNodeDto? finalNode = null;

            if (options.Progress == null)
            {
                QueryStringBuilder.Add(query, "progress", false);
                finalNode = await Transport.PostMultipartJsonAsync<FileSystemNodeDto>("add", query, stream, string.IsNullOrWhiteSpace(name) ? "file.bin" : name, "application/octet-stream", cancel).ConfigureAwait(false);
            }
            else
            {
                await Transport.ReadNdjsonAsync<FileSystemNodeDto>("add", query, dto =>
                {
                    if (!string.IsNullOrWhiteSpace(dto.Hash))
                    {
                        finalNode = dto;
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.Name))
                    {
                        var progress = new TransferProgress
                        {
                            Name = dto.Name,
                            Bytes = ParseUnsignedLong(dto.Size)
                        };
                        options.Progress.Report(progress);
                    }

                    return Task.CompletedTask;
                }, cancel).ConfigureAwait(false);
            }

            if (finalNode == null || string.IsNullOrWhiteSpace(finalNode.Hash))
            {
                throw new IpfsSerializationException("add", "The add route completed without returning a final CID.");
            }

            var hash = finalNode.Hash!;
            return await ListFileAsync(hash, cancel).ConfigureAwait(false);
        }

        public Task<IFileSystemNode> AddDirectoryAsync(string path, bool recursive = true, AddFileOptions? options = null, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
            }

            return AddDirectoryCoreAsync(path, recursive, options, cancel);
        }

        public async Task<IFileSystemNode> AddFileAsync(string path, AddFileOptions? options = null, CancellationToken cancel = default)
        {
            using (var stream = File.OpenRead(path))
            {
                return await AddAsync(stream, Path.GetFileName(path), options, cancel).ConfigureAwait(false);
            }
        }

        public Task<IFileSystemNode> AddTextAsync(string text, AddFileOptions? options = null, CancellationToken cancel = default)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var stream = new MemoryStream(bytes, writable: false);
            return AddAsync(stream, "text.txt", options, cancel);
        }

        public Task<Stream> GetAsync(string path, bool compress = false, CancellationToken cancel = default)
        {
            var query = BuildArgQuery(path);
            QueryStringBuilder.Add(query, "compress", compress);
            return Transport.PostStreamAsync("get", query, cancel);
        }

        public async Task<IFileSystemNode> ListFileAsync(string path, CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<FileSystemDetailsDto>("file/ls", BuildArgQuery(path), cancel).ConfigureAwait(false);
            if (dto.Arguments == null || dto.Objects == null)
            {
                throw new IpfsSerializationException("file/ls", "The file listing response was missing its object map.");
            }

            var hash = dto.Arguments.TryGetValue(path, out var mappedHash)
                ? mappedHash
                : dto.Arguments.Values.FirstOrDefault();
            if (hash == null || !dto.Objects.TryGetValue(hash, out var detail))
            {
                throw new IpfsSerializationException("file/ls", "The file listing response did not contain the requested object.");
            }

            return DtoMapper.ToFileSystemNode(detail);
        }

        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancel = default)
        {
            using (var stream = await ReadFileAsync(path, cancel).ConfigureAwait(false))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        public Task<Stream> ReadFileAsync(string path, CancellationToken cancel = default)
        {
            return Transport.PostStreamAsync("cat", BuildArgQuery(path), cancel);
        }

        public Task<Stream> ReadFileAsync(string path, long offset, long count, CancellationToken cancel = default)
        {
            var query = BuildArgQuery(path);
            QueryStringBuilder.Add(query, "offset", offset);
            QueryStringBuilder.Add(query, "length", count);
            return Transport.PostStreamAsync("cat", query, cancel);
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }

        private static List<KeyValuePair<string, string>> BuildAddQuery(AddFileOptions options)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "hash", options.Hash ?? MultiHash.DefaultAlgorithmName);
            QueryStringBuilder.Add(query, "cid-base", options.Encoding ?? MultiBase.DefaultAlgorithmName);
            QueryStringBuilder.Add(query, "only-hash", options.OnlyHash);
            QueryStringBuilder.Add(query, "pin", options.Pin);
            QueryStringBuilder.Add(query, "raw-leaves", options.RawLeaves);
            QueryStringBuilder.Add(query, "trickle", options.Trickle);
            QueryStringBuilder.Add(query, "wrap-with-directory", options.Wrap);
            QueryStringBuilder.Add(query, "protect", options.ProtectionKey);
            QueryStringBuilder.Add(query, "progress", options.Progress != null);
            if (options.ChunkSize > 0)
            {
                QueryStringBuilder.Add(query, "chunker", $"size-{options.ChunkSize.ToString(CultureInfo.InvariantCulture)}");
            }
            return query;
        }

        private static ulong ParseUnsignedLong(string? value)
        {
            return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0UL;
        }

        private async Task<IFileSystemNode> AddDirectoryCoreAsync(string path, bool recursive, AddFileOptions? options, CancellationToken cancel)
        {
            options ??= new AddFileOptions();

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var rootDirectory = Path.GetFullPath(path);
            var rootName = Path.GetFileName(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(rootName))
            {
                rootName = "upload";
            }

            var formValues = new List<KeyValuePair<string, string>>
            {
                new("root-name", rootName)
            };

            var fileStreams = new List<FileStream>();
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(rootDirectory, "*", searchOption).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    var relativeDirectory = ToMultipartPath(rootDirectory, directory);
                    if (!string.IsNullOrWhiteSpace(relativeDirectory))
                    {
                        formValues.Add(new KeyValuePair<string, string>("dir", relativeDirectory));
                    }
                }

                var fileParts = new List<MultipartFilePart>();
                foreach (var filePath in Directory.EnumerateFiles(rootDirectory, "*", searchOption).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    var relativePath = ToMultipartPath(rootDirectory, filePath);
                    var stream = File.OpenRead(filePath);
                    fileStreams.Add(stream);
                    fileParts.Add(new MultipartFilePart(
                        stream,
                        "file",
                        string.IsNullOrWhiteSpace(relativePath) ? Path.GetFileName(filePath) : relativePath,
                        contentType: "application/octet-stream"));
                }

                var query = BuildAddQuery(options);
                using var content = MultipartRequestFactory.CreateFiles(fileParts, formValues);
                var finalNode = await Transport.PostMultipartFormJsonAsync<FileSystemNodeDto>("add", query, content, cancel).ConfigureAwait(false);
                if (finalNode == null || string.IsNullOrWhiteSpace(finalNode.Hash))
                {
                    throw new IpfsSerializationException("add", "The add route completed without returning a final CID.");
                }

                return await ListFileAsync(finalNode.Hash!, cancel).ConfigureAwait(false);
            }
            finally
            {
                foreach (var stream in fileStreams)
                {
                    stream.Dispose();
                }
            }
        }

        private static string ToMultipartPath(string rootDirectory, string path)
        {
            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootDirectory));
            var normalizedPath = Path.GetFullPath(path);
            var relativePath = normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(normalizedRoot.Length)
                : normalizedPath
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            relativePath = relativePath
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            return relativePath == "."
                ? string.Empty
                : relativePath.Trim('/');
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Path.DirectorySeparatorChar.ToString();
            }

            var lastChar = path[path.Length - 1];
            if (lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar)
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }

    public sealed class KeyClient : ApiClientBase, IKeyApi
    {
        internal KeyClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public async Task<IKey> CreateAsync(string name, string keyType = "rsa", int size = 2048, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", name);
            QueryStringBuilder.Add(query, "type", keyType);
            QueryStringBuilder.Add(query, "size", size);
            var dto = await Transport.PostJsonAsync<CryptoKeyDto>("key/gen", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToKey(dto);
        }

        public Task<string> ExportAsync(string name, char[] password, CancellationToken cancel = default)
        {
            throw MissingServerCapability(nameof(ExportAsync), "The server does not expose key export.");
        }

        public Task<IKey> ImportAsync(string name, string pem, char[] password, CancellationToken cancel = default)
        {
            throw MissingServerCapability(nameof(ImportAsync), "The server does not expose key import.");
        }

        public async Task<IEnumerable<IKey>> ListAsync(CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<CryptoKeysDto>("key/list", query: null, cancel).ConfigureAwait(false);
            return (dto.Keys ?? Array.Empty<CryptoKeyDto>()).Select(DtoMapper.ToKey).Cast<IKey>().ToArray();
        }

        public async Task<IKey?> RemoveAsync(string name, CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<CryptoKeysDto>("key/rm", BuildArgQuery(name), cancel).ConfigureAwait(false);
            var key = dto.Keys?.FirstOrDefault();
            return key == null ? null : DtoMapper.ToKey(key);
        }

        public async Task<IKey> RenameAsync(string oldName, string newName, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.AddRepeated(query, "arg", new[] { oldName, newName });
            var dto = await Transport.PostJsonAsync<CryptoKeyRenameDto>("key/rename", query, cancel).ConfigureAwait(false);
            return new ClientKey(dto.Now ?? newName, dto.Id ?? string.Empty);
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }

    public sealed class NameClient : ApiClientBase, INameApi
    {
        internal NameClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public async Task<NamedContent> PublishAsync(string path, bool resolve = true, string key = "self", TimeSpan? lifetime = null, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", path);
            QueryStringBuilder.Add(query, "resolve", resolve);
            QueryStringBuilder.Add(query, "key", key);
            QueryStringBuilder.Add(query, "lifetime", lifetime ?? TimeSpan.FromHours(24));
            var dto = await Transport.PostJsonAsync<NamedContentDto>("name/publish", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToNamedContent(dto);
        }

        public Task<NamedContent> PublishAsync(Cid id, string key = "self", TimeSpan? lifetime = null, CancellationToken cancel = default)
        {
            return PublishAsync(id.ToString(), resolve: true, key: key, lifetime: lifetime, cancel: cancel);
        }

        public async Task<string> ResolveAsync(string name, bool recursive = false, bool nocache = false, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", name);
            QueryStringBuilder.Add(query, "recursive", recursive);
            QueryStringBuilder.Add(query, "nocache", nocache);
            var dto = await Transport.PostJsonAsync<PathDto>("name/resolve", query, cancel).ConfigureAwait(false);
            return dto.Path ?? string.Empty;
        }
    }

    public sealed class ObjectClient : ApiClientBase, IObjectApi
    {
        internal ObjectClient(IpfsHttpTransport transport)
            : base(transport)
        {
        }

        public Task<Stream> DataAsync(Cid id, CancellationToken cancel = default)
        {
            return Transport.PostStreamAsync("object/data", BuildArgQuery(id.ToString()), cancel);
        }

        public async Task<DagNode> GetAsync(Cid id, CancellationToken cancel = default)
        {
            var query = BuildArgQuery(id.ToString());
            QueryStringBuilder.Add(query, "data-encoding", "base64");
            var dto = await Transport.PostJsonAsync<ObjectDataDetailDto>("object/get", query, cancel).ConfigureAwait(false);
            return DtoMapper.ToDagNode(dto, dataIsBase64: true);
        }

        public async Task<IEnumerable<IMerkleLink>> LinksAsync(Cid id, CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<ObjectLinkDetailDto>("object/links", BuildArgQuery(id.ToString()), cancel).ConfigureAwait(false);
            return DtoMapper.ToMerkleLinks(dto.Links);
        }

        public async Task<DagNode> NewAsync(string? template = null, CancellationToken cancel = default)
        {
            List<KeyValuePair<string, string>>? query = null;
            if (template != null)
            {
                query = BuildArgQuery(template);
            }

            var dto = await Transport.PostJsonAsync<ObjectLinkDetailDto>("object/new", query, cancel).ConfigureAwait(false);
            return await GetAsync(Cid.Decode(dto.Hash!), cancel).ConfigureAwait(false);
        }

        public Task<DagNode> NewDirectoryAsync(CancellationToken cancel = default)
        {
            return NewAsync("unixfs-dir", cancel);
        }

        public async Task<DagNode> PutAsync(byte[] data, IEnumerable<IMerkleLink>? links = null, CancellationToken cancel = default)
        {
            var node = new DagNode(data ?? Array.Empty<byte>(), links);
            return await PutAsync(node, cancel).ConfigureAwait(false);
        }

        public async Task<DagNode> PutAsync(DagNode node, CancellationToken cancel = default)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "inputenc", "protobuf");
            QueryStringBuilder.Add(query, "datafieldenc", "text");
            QueryStringBuilder.Add(query, "pin", false);

            using (var stream = new MemoryStream(node.ToArray(), writable: false))
            {
                var dto = await Transport.PostMultipartJsonAsync<ObjectLinkDetailDto>("object/put", query, stream, "object.bin", "application/octet-stream", cancel).ConfigureAwait(false);
                node.Id = Cid.Decode(dto.Hash!);
                return node;
            }
        }

        public async Task<ObjectStat> StatAsync(Cid id, CancellationToken cancel = default)
        {
            var dto = await Transport.PostJsonAsync<ObjectStatDto>("object/stat", BuildArgQuery(id.ToString()), cancel).ConfigureAwait(false);
            return DtoMapper.ToObjectStat(dto);
        }

        private static List<KeyValuePair<string, string>> BuildArgQuery(string value)
        {
            var query = new List<KeyValuePair<string, string>>();
            QueryStringBuilder.Add(query, "arg", value);
            return query;
        }
    }
}
