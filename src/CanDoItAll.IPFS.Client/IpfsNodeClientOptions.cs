using System;

namespace Ipfs.Engine.Client
{
    /// <summary>
    ///   Options for <see cref="IpfsNodeClient"/>.
    /// </summary>
    public sealed class IpfsNodeClientOptions
    {
        /// <summary>
        ///   The base address of the IPFS node.
        /// </summary>
        public Uri? BaseAddress { get; init; }

        /// <summary>
        ///   The relative HTTP API path under the node root.
        /// </summary>
        public string ApiPath { get; init; } = "api/v0";

        internal IpfsNodeClientOptions Normalize(Uri? fallbackBaseAddress)
        {
            var resolvedBaseAddress = BaseAddress ?? fallbackBaseAddress;
            if (resolvedBaseAddress == null)
            {
                throw new ArgumentException(
                    "Either IpfsNodeClientOptions.BaseAddress or HttpClient.BaseAddress must be specified.",
                    nameof(BaseAddress));
            }

            if (!resolvedBaseAddress.IsAbsoluteUri
                || (resolvedBaseAddress.Scheme != Uri.UriSchemeHttp
                    && resolvedBaseAddress.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    "The IPFS node base address must be an absolute HTTP or HTTPS URI.",
                    nameof(BaseAddress));
            }

            var normalizedApiPath = (ApiPath ?? string.Empty).Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedApiPath)
                || normalizedApiPath.Contains('\\')
                || normalizedApiPath.Contains('?')
                || normalizedApiPath.Contains('#')
                || Uri.TryCreate(normalizedApiPath, UriKind.Absolute, out _))
            {
                throw new ArgumentException(
                    "The API path must be a non-empty relative URI path without a query or fragment.",
                    nameof(ApiPath));
            }

            return new IpfsNodeClientOptions
            {
                BaseAddress = new Uri($"{resolvedBaseAddress.AbsoluteUri.TrimEnd('/')}/", UriKind.Absolute),
                ApiPath = normalizedApiPath
            };
        }
    }
}
