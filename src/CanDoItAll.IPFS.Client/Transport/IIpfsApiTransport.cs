using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Engine.Client.Transport
{
    /// <summary>
    ///   Defines the protocol transport used by operation clients.
    /// </summary>
    internal interface IIpfsApiTransport
    {
        Task<T> PostJsonAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken);

        Task<T?> PostJsonOrDefaultAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken);

        Task SendAsync(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken);

        Task<Stream> PostStreamAsync(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken);

        Task<T> PostMultipartJsonAsync<T>(
            string route,
            IEnumerable<KeyValuePair<string, string>>? query,
            Stream body,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken);

        Task<T> PostMultipartFormJsonAsync<T>(
            string route,
            IEnumerable<KeyValuePair<string, string>>? query,
            MultipartFormDataContent content,
            CancellationToken cancellationToken);

        Task<T?> PostMultipartJsonOrDefaultAsync<T>(
            string route,
            IEnumerable<KeyValuePair<string, string>>? query,
            Stream body,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken);

        Task SendMultipartAsync(
            string route,
            IEnumerable<KeyValuePair<string, string>>? query,
            Stream body,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken);

        Task ReadNdjsonAsync<T>(
            string route,
            IEnumerable<KeyValuePair<string, string>>? query,
            Func<T, Task> onItem,
            CancellationToken cancellationToken);
    }
}
