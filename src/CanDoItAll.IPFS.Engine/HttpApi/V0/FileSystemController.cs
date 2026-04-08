using Ipfs.CoreApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using Newtonsoft.Json;
using Microsoft.Net.Http.Headers;

namespace Ipfs.Server.HttpApi.V0
{
    /// <summary>
    ///  A created file.
    /// </summary>
    public class FileSystemNodeDto
    {
        /// <summary>
        ///   The file name.
        /// </summary>
        public string Name;

        /// <summary>
        ///   The CID of the file.
        /// </summary>
        public string Hash;

        /// <summary>
        ///   The file size.
        /// </summary>
        public string Size;
    }

    /// <summary>
    ///  A link to a file.
    /// </summary>
    public class FileSystemLinkDto
    {
        /// <summary>
        ///   The file name.
        /// </summary>
        public string Name;

        /// <summary>
        ///   The CID of the file.
        /// </summary>
        public string Hash;

        /// <summary>
        ///   The file size.
        /// </summary>
        public long Size;

        /// <summary>
        ///   The cumulative content size for the linked node.
        /// </summary>
        public long ContentSize;

        /// <summary>
        ///   "File" or "Directory"
        /// </summary>
        public string Type;

        /// <summary>
        ///   The number of direct children when the linked node is a directory.
        /// </summary>
        public int ChildCount;
    }

    /// <summary>
    ///   Details on a files.
    /// </summary>
    public class FileSystemDetailDto
    {
        /// <summary>
        ///   The CID of the file.
        /// </summary>
        public string Hash;

        /// <summary>
        ///   The file size.
        /// </summary>
        public long Size;

        /// <summary>
        ///   "File" or "Directory"
        /// </summary>
        public string Type;

        /// <summary>
        ///   Links to other files.
        /// </summary>
        public FileSystemLinkDto[] Links;
    }

    /// <summary>
    ///   A map of files.
    /// </summary>
    public class FileSystemDetailsDto
    {
        /// <summary>
        ///  A path and its CID.
        /// </summary>
        public Dictionary<string, string> Arguments;

        /// <summary>
        ///   The pins.
        /// </summary>
        public Dictionary<string, FileSystemDetailDto> Objects;
    }

    /// <summary>
    ///   DNS mapping to IPFS.
    /// </summary>
    /// <remarks>
    ///   Multihashes are hard to remember, but domain names are usually easy to
    ///   remember. To create memorable aliases for multihashes, DNS TXT
    ///   records can point to other DNS links, IPFS objects, IPNS keys, etc.
    /// </remarks>
    public class FileSystemController : IpfsController
    {
        /// <summary>
        ///   Creates a new controller.
        /// </summary>
        public FileSystemController(ICoreApi ipfs) : base(ipfs) { }

        /// <summary>
        ///   Get the contents of a file or directory.
        /// </summary>
        /// <param name="arg">
        ///   A path to an existing file, such as "QmXarR6rgkQ2fDSHjSY5nM2kuCXKYGViky5nohtwgF65Ec/about"
        ///   or "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
        /// </param>
        /// <param name="offset">
        ///   Offset into the file.
        /// </param>
        /// <param name="length">
        ///   Number of bytes to read.
        /// </param>
        [HttpGet, HttpPost, Route("cat")]
        [Produces("application/octet-stream")]
        public async Task<IActionResult> Cat(
            string arg,
            long offset = 0,
            long length = 0)
        {
            EntityTagHeaderValue etag = null;
            var path = await IpfsCore.Generic.ResolveAsync(arg, true, Cancel);
            var cid = Cid.Decode(path.Substring(6)); // remove leading "/ipfs/"

            // Use an etag if the path is IPFS or CID.
            if (arg.StartsWith("/ipfs/") || arg[0] != '/')
            {
                etag = ETag(cid);
                Immutable();
            }

            // Use the last part of the path as the download filename
            var filename = arg.Split('/').Last();
            var stream = await IpfsCore.FileSystem.ReadFileAsync(cid, offset, length, Cancel);
            return File(stream, "application/octet-stream", filename, null, etag);
        }

        /// <summary>
        ///   Get the object as a TAR file.
        /// </summary>
        /// <param name="arg">
        ///   A path to an existing file or directory.
        /// </param>
        /// <param name="compress">
        ///   If <b>true</b>, generate gzipped TAR.
        /// </param>
        [HttpGet, HttpPost, Route("get")]
        [Produces("application/tar")]
        public async Task Get(string arg, bool compress = false)
        {
            var tar = await IpfsCore.FileSystem.GetAsync(arg, compress, Cancel);
            Response.ContentType = "application/tar";
            Response.Headers.Add("X-Stream-Output", "1");
            Response.Headers.Add("X-Content-Length", "4");
            Response.StatusCode = 200;

            await tar.CopyToAsync(Response.Body);
            await Response.Body.FlushAsync();
        }

        /// <summary>
        ///   Get information on the file or directory.
        /// </summary>
        /// <param name="arg">
        ///   A path to an existing file, such as "QmXarR6rgkQ2fDSHjSY5nM2kuCXKYGViky5nohtwgF65Ec/about"
        ///   or "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
        /// </param>
        [HttpGet, HttpPost, Route("file/ls")]
        public async Task<FileSystemDetailsDto> Stat(
            string arg)
        {
            var node = await IpfsCore.FileSystem.ListFileAsync(arg, Cancel);
            var dto = new FileSystemDetailsDto
            {
                Arguments = new Dictionary<string, string>(),
                Objects = new Dictionary<string, FileSystemDetailDto>()
            };
            dto.Arguments[arg] = node.Id;
            dto.Objects[node.Id] = new FileSystemDetailDto
            {
                Hash = node.Id,
                Size = node.Size,
                Type = node.IsDirectory ? "Directory" : "File",
                Links = node.Links
                    .Select(link => new FileSystemLinkDto
                    {
                        Hash = link.Id,
                        Name = link.Name,
                        Size = link.Size,
                        ContentSize = link.ContentSize,
                        Type = link.IsDirectory ? "Directory" : "File",
                        ChildCount = link.ChildCount
                    })
                    .ToArray()
            };
            return dto;
        }

        /// <summary>
        ///   Add a file or directory tree.
        /// </summary>
        [HttpGet, HttpPost, Route("add")]
        public async Task Add(
            string hash = MultiHash.DefaultAlgorithmName,
            [ModelBinder(Name = "cid-base")] string cidBase = MultiBase.DefaultAlgorithmName,
            [ModelBinder(Name = "only-hash")] bool onlyHash = false,
            string chunker = null,
            bool pin = false,
            [ModelBinder(Name = "raw-leaves")] bool rawLeaves = false,
            bool trickle = false,
            [ModelBinder(Name = "wrap-with-directory")] bool wrap = false,
            string protect = null,
            bool progress = true
            )
        {
            var options = new AddFileOptions
            {
                Encoding = cidBase,
                Hash = hash,
                OnlyHash = onlyHash,
                Pin = pin,
                RawLeaves = rawLeaves,
                Trickle = trickle,
                Wrap = wrap,
                ProtectionKey = protect,
            };
            if (chunker != null)
            {
                if (chunker.StartsWith("size-"))
                {
                    options.ChunkSize = int.Parse(chunker.Substring(5), CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("chunker");
                }
            }

            if (progress)
            {
                options.Progress = new Progress<TransferProgress>(StreamJson);
            }

            var form = await Request.ReadFormAsync(Cancel);
            var files = form.Files;
            var rootName = form["root-name"].FirstOrDefault();
            var directoryHints = form["dir"].Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            if (files.Count == 0 && string.IsNullOrWhiteSpace(rootName) && directoryHints.Length == 0)
            {
                throw new ArgumentNullException("file");
            }

            var shouldAddDirectory = !string.IsNullOrWhiteSpace(rootName)
                || directoryHints.Length > 0
                || files.Count != 1
                || files.Any(file => HasNestedPath(file.FileName));

            if (!shouldAddDirectory)
            {
                var file = files[0];
                var safeName = Path.GetFileName(NormalizeUploadPath(file.FileName));
                using (var stream = file.OpenReadStream())
                {
                    var node = await IpfsCore.FileSystem.AddAsync(stream, safeName, options, Cancel);
                    StreamJson(new FileSystemNodeDto
                    {
                        Name = node.Id,
                        Hash = node.Id,
                        Size = node.Size.ToString(CultureInfo.InvariantCulture)
                    });
                }

                return;
            }

            var stagingRoot = Path.Combine(Path.GetTempPath(), $"ipfs-upload-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingRoot);

            try
            {
                var uploadRootName = SanitizeRootName(rootName);
                var uploadRoot = string.IsNullOrWhiteSpace(uploadRootName)
                    ? stagingRoot
                    : Path.Combine(stagingRoot, uploadRootName);
                Directory.CreateDirectory(uploadRoot);

                foreach (var directoryHint in directoryHints)
                {
                    var directoryPath = CombineWithinRoot(uploadRoot, directoryHint);
                    Directory.CreateDirectory(directoryPath);
                }

                foreach (var file in files)
                {
                    var relativePath = NormalizeUploadPath(file.FileName);
                    if (string.IsNullOrWhiteSpace(relativePath))
                    {
                        throw new InvalidOperationException("Each uploaded file must include a file name.");
                    }

                    var fullPath = CombineWithinRoot(uploadRoot, relativePath);
                    var parent = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    using var input = file.OpenReadStream();
                    using var output = System.IO.File.Create(fullPath);
                    await input.CopyToAsync(output, Cancel);
                }

                var node = await IpfsCore.FileSystem.AddDirectoryAsync(uploadRoot, recursive: true, options, Cancel);
                StreamJson(new FileSystemNodeDto
                {
                    Name = node.Id,
                    Hash = node.Id,
                    Size = node.Size.ToString(CultureInfo.InvariantCulture)
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingRoot))
                    {
                        Directory.Delete(stagingRoot, recursive: true);
                    }
                }
                catch
                {
                    // Ignore cleanup failures for transient staging content.
                }
            }
        }

        private static bool HasNestedPath(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            return fileName.Contains('/') || fileName.Contains('\\');
        }

        private static string SanitizeRootName(string? rootName)
        {
            var normalized = NormalizeUploadPath(rootName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            var segments = normalized.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 0 ? string.Empty : segments[segments.Length - 1];
        }

        private static string NormalizeUploadPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var trimmed = path.Trim().Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var segments = trimmed.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment == "." || segment == ".."))
            {
                throw new InvalidOperationException("Uploaded paths must stay within the selected folder.");
            }

            return string.Join(Path.DirectorySeparatorChar, segments);
        }

        private static string CombineWithinRoot(string root, string relativePath)
        {
            var normalized = NormalizeUploadPath(relativePath);
            var combined = Path.GetFullPath(Path.Combine(root, normalized));
            var normalizedRoot = Path.GetFullPath(root);

            if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Uploaded paths must stay within the selected folder.");
            }

            return combined;
        }
    }
}
