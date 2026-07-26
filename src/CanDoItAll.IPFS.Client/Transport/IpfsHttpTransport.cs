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
    internal sealed class IpfsHttpTransport : IIpfsApiTransport
    {
        private readonly HttpClient httpClient;
        private readonly Uri baseAddress;
        private readonly string apiPath;
        public IpfsHttpTransport(HttpClient httpClient, IpfsNodeClientOptions options)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var normalizedOptions = options.Normalize(httpClient.BaseAddress);
            baseAddress = normalizedOptions.BaseAddress!;
            apiPath = normalizedOptions.ApiPath;
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
            try
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseStream(stream, response);
            }
            catch (Exception ex)
            {
                response.Dispose();

                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                if (cancellationToken.IsCancellationRequested && IsExpectedCancellation(ex))
                {
                    throw new OperationCanceledException(
                        "The response stream was canceled by the caller.",
                        ex,
                        cancellationToken);
                }

                if (ex is OperationCanceledException)
                {
                    throw new IpfsTransportException(route, "The response stream timed out.", ex);
                }

                if (ex is HttpRequestException)
                {
                    throw new IpfsTransportException(route, "Unable to open the response stream.", ex);
                }

                throw;
            }
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

            Exception? callbackException = null;
            try
            {
                using (var response = await SendCoreAsync(HttpMethod.Post, route, query, content: null, responseHeadersRead: true, cancellationToken).ConfigureAwait(false))
                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
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

                        if (item is null)
                        {
                            throw new IpfsSerializationException(
                                route,
                                "The IPFS API returned null where a line-delimited JSON item was expected.",
                                line);
                        }

                        try
                        {
                            await onItem(item).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            callbackException = ex;
                            throw;
                        }
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ReferenceEquals(ex, callbackException))
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested && IsExpectedCancellation(ex))
            {
                throw new OperationCanceledException(
                    "The line-delimited response was canceled by the caller.",
                    ex,
                    cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                throw new IpfsTransportException(
                    route,
                    $"The line-delimited IPFS API response for route '{route}' timed out.",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                throw new IpfsTransportException(
                    route,
                    $"Unable to read the line-delimited IPFS API response for route '{route}'.",
                    ex);
            }
            catch (IOException ex)
            {
                throw new IpfsTransportException(
                    route,
                    $"Unable to read the line-delimited IPFS API response for route '{route}'.",
                    ex);
            }
        }

        private async Task<T> SendJsonAsync<T>(HttpMethod method, string route, IEnumerable<KeyValuePair<string, string>>? query, HttpContent? content, bool allowEmptyBody, CancellationToken cancellationToken)
        {
            using (var response = await SendCoreAsync(method, route, query, content, responseHeadersRead: false, cancellationToken).ConfigureAwait(false))
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    if (allowEmptyBody)
                    {
                        return default!;
                    }

                    throw new IpfsSerializationException(route, "The IPFS API returned an empty response where JSON was expected.");
                }

                T result;
                try
                {
                    result = JsonConvert.DeserializeObject<T>(payload)!;
                }
                catch (Exception ex)
                {
                    throw new IpfsSerializationException(route, "Unable to parse JSON returned by the IPFS API.", payload, ex);
                }

                if (result is null)
                {
                    throw new IpfsSerializationException(
                        route,
                        "The IPFS API returned null where a JSON result was expected.",
                        payload);
                }

                return result;
            }
        }

        private async Task SendNoContentAsync(HttpMethod method, string route, IEnumerable<KeyValuePair<string, string>>? query, HttpContent? content, CancellationToken cancellationToken)
        {
            using var response = await SendCoreAsync(
                method,
                route,
                query,
                content,
                responseHeadersRead: false,
                cancellationToken).ConfigureAwait(false);
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

                    using (response)
                    {
                        await ThrowApiExceptionAsync(route, response, cancellationToken).ConfigureAwait(false);
                        throw new InvalidOperationException("The API exception thrower returned unexpectedly.");
                    }
                }
                catch (IpfsApiException)
                {
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    throw new IpfsTransportException(
                        route,
                        $"Unable to send the IPFS API request for route '{route}'.",
                        ex);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new IpfsTransportException(
                        route,
                        $"The IPFS API request for route '{route}' timed out.",
                        ex);
                }
            }
        }

        private Uri BuildUri(string route, IEnumerable<KeyValuePair<string, string>>? query)
        {
            var trimmedRoute = (route ?? string.Empty).Trim('/');
            var path = string.IsNullOrWhiteSpace(apiPath) ? trimmedRoute : $"{apiPath}/{trimmedRoute}";
            var target = new Uri(baseAddress, path);
            var builder = new UriBuilder(target)
            {
                Query = QueryStringBuilder.Build(query)
            };
            if (target.IsDefaultPort)
            {
                builder.Port = -1;
            }

            return builder.Uri;
        }

        private static async Task ThrowApiExceptionAsync(string route, HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var body = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

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
