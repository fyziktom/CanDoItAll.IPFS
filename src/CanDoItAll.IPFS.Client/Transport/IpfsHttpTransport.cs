using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.Engine.Client.Models;
using Newtonsoft.Json;

namespace Ipfs.Engine.Client.Transport
{
    internal sealed class IpfsHttpTransport
    {
        private readonly HttpClient httpClient;
        private readonly Uri baseAddress;
        private readonly string apiPath;
        private readonly JsonSerializer serializer = JsonSerializer.CreateDefault();

        public IpfsHttpTransport(HttpClient httpClient, IpfsNodeClientOptions options)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            baseAddress = options.BaseAddress ?? httpClient.BaseAddress ?? throw new ArgumentException("Either IpfsNodeClientOptions.BaseAddress or HttpClient.BaseAddress must be specified.", nameof(options));
            apiPath = (options.ApiPath ?? string.Empty).Trim('/');
        }

        public Task<T> PostJsonAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken)
        {
            return SendJsonAsync<T>(HttpMethod.Post, route, query, content: null, allowEmptyBody: false, cancellationToken: cancellationToken);
        }

        public Task<T?> PostJsonOrDefaultAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken)
        {
            return SendJsonAsync<T?>(HttpMethod.Post, route, query, content: null, allowEmptyBody: true, cancellationToken: cancellationToken);
        }

        public Task SendAsync(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken)
        {
            return SendNoContentAsync(HttpMethod.Post, route, query, content: null, cancellationToken: cancellationToken);
        }

        public async Task<Stream> PostStreamAsync(string route, IEnumerable<KeyValuePair<string, string>>? query, CancellationToken cancellationToken)
        {
            var response = await SendCoreAsync(HttpMethod.Post, route, query, content: null, responseHeadersRead: true, cancellationToken).ConfigureAwait(false);
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return new ResponseStream(stream, response);
        }

        public Task<T> PostMultipartJsonAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, Stream body, string fileName, string? contentType, CancellationToken cancellationToken)
        {
            var content = MultipartRequestFactory.CreateFile(body, "file", fileName, contentType);
            return SendJsonAsync<T>(HttpMethod.Post, route, query, content, allowEmptyBody: false, cancellationToken);
        }

        public Task<T> PostMultipartFormJsonAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, MultipartFormDataContent content, CancellationToken cancellationToken)
        {
            return SendJsonAsync<T>(HttpMethod.Post, route, query, content, allowEmptyBody: false, cancellationToken);
        }

        public Task<T?> PostMultipartJsonOrDefaultAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, Stream body, string fileName, string? contentType, CancellationToken cancellationToken)
        {
            var content = MultipartRequestFactory.CreateFile(body, "file", fileName, contentType);
            return SendJsonAsync<T?>(HttpMethod.Post, route, query, content, allowEmptyBody: true, cancellationToken);
        }

        public async Task SendMultipartAsync(string route, IEnumerable<KeyValuePair<string, string>>? query, Stream body, string fileName, string? contentType, CancellationToken cancellationToken)
        {
            var content = MultipartRequestFactory.CreateFile(body, "file", fileName, contentType);
            await SendNoContentAsync(HttpMethod.Post, route, query, content, cancellationToken).ConfigureAwait(false);
        }

        public async Task ReadNdjsonAsync<T>(string route, IEnumerable<KeyValuePair<string, string>>? query, Func<T, Task> onItem, CancellationToken cancellationToken)
        {
            if (onItem == null)
            {
                throw new ArgumentNullException(nameof(onItem));
            }

            try
            {
                using (var response = await SendCoreAsync(HttpMethod.Post, route, query, content: null, responseHeadersRead: true, cancellationToken).ConfigureAwait(false))
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await NdjsonReader.ReadAsync(stream, async line =>
                    {
                        T item;
                        try
                        {
                            item = JsonConvert.DeserializeObject<T>(line)!;
                        }
                        catch (Exception ex)
                        {
                            throw new IpfsSerializationException(route, "Unable to parse line-delimited JSON returned by the IPFS API.", line, ex);
                        }

                        await onItem(item).ConfigureAwait(false);
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested && IsExpectedCancellation(ex))
            {
                return;
            }
        }

        private async Task<T> SendJsonAsync<T>(HttpMethod method, string route, IEnumerable<KeyValuePair<string, string>>? query, HttpContent? content, bool allowEmptyBody, CancellationToken cancellationToken)
        {
            using (var response = await SendCoreAsync(method, route, query, content, responseHeadersRead: false, cancellationToken).ConfigureAwait(false))
            {
                var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    if (allowEmptyBody)
                    {
                        return default!;
                    }

                    throw new IpfsSerializationException(route, "The IPFS API returned an empty response where JSON was expected.");
                }

                try
                {
                    using (var stringReader = new StringReader(payload))
                    using (var jsonReader = new JsonTextReader(stringReader))
                    {
                        return serializer.Deserialize<T>(jsonReader)!;
                    }
                }
                catch (Exception ex)
                {
                    throw new IpfsSerializationException(route, "Unable to parse JSON returned by the IPFS API.", payload, ex);
                }
            }
        }

        private async Task SendNoContentAsync(HttpMethod method, string route, IEnumerable<KeyValuePair<string, string>>? query, HttpContent? content, CancellationToken cancellationToken)
        {
            using (var response = await SendCoreAsync(method, route, query, content, responseHeadersRead: false, cancellationToken).ConfigureAwait(false))
            {
                await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
            }
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string route, IEnumerable<KeyValuePair<string, string>>? query, HttpContent? content, bool responseHeadersRead, CancellationToken cancellationToken)
        {
            var requestUri = BuildUri(route, query);
            using (var request = new HttpRequestMessage(method, requestUri))
            {
                request.Content = content;

                try
                {
                    var completionOption = responseHeadersRead ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
                    var response = await httpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }

                    await ThrowApiExceptionAsync(route, response).ConfigureAwait(false);
                    throw new InvalidOperationException("The API exception thrower returned unexpectedly.");
                }
                catch (IpfsApiException)
                {
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    throw new IpfsTransportException(route, $"Unable to send request to '{requestUri}'.", ex);
                }
            }
        }

        private Uri BuildUri(string route, IEnumerable<KeyValuePair<string, string>>? query)
        {
            var trimmedRoute = (route ?? string.Empty).Trim('/');
            var path = string.IsNullOrWhiteSpace(apiPath) ? trimmedRoute : $"{apiPath}/{trimmedRoute}";
            var builder = new UriBuilder(new Uri(baseAddress, path))
            {
                Query = QueryStringBuilder.Build(query)
            };
            return builder.Uri;
        }

        private static async Task ThrowApiExceptionAsync(string route, HttpResponseMessage response)
        {
            var body = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var error = JsonConvert.DeserializeObject<IpfsApiError>(body!);
                    if (error != null)
                    {
                        throw new IpfsApiException(
                            response.StatusCode,
                            route,
                            error.Message ?? $"The IPFS API returned HTTP {(int)response.StatusCode}.",
                            error.Code,
                            body,
                            error.Details);
                    }
                }
                catch (JsonException)
                {
                }
            }

            throw new IpfsApiException(
                response.StatusCode,
                route,
                $"The IPFS API returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                rawBody: body);
        }

        private static bool IsExpectedCancellation(Exception exception)
        {
            if (exception is OperationCanceledException || exception is ObjectDisposedException)
            {
                return true;
            }

            if (exception is IOException ioException && ioException.InnerException != null)
            {
                return IsExpectedCancellation(ioException.InnerException);
            }

            return false;
        }
    }
}
