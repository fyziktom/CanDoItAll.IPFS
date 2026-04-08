using System;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Ipfs.Server
{
    public sealed class HttpApiHostOptionsSetup : IPostConfigureOptions<HttpApiHostOptions>
    {
        internal static readonly string[] DefaultExposedHeaders =
        [
            "X-Stream-Output",
            "X-Chunked-Output",
            "X-Content-Length"
        ];

        public void PostConfigure(string? name, HttpApiHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var isPro = options.Mode == HttpApiHostProfileMode.Pro;
            options.Passphrase = HttpApiHostPassphraseResolver.NormalizeSecret(options.Passphrase);
            options.AdminAccessKey = HttpApiHostPassphraseResolver.NormalizeSecret(options.AdminAccessKey);
            options.RequireAuthentication ??= isPro;
            options.AllowAnyOrigin ??= !isPro;
            options.AllowedOrigins = (options.AllowedOrigins ?? Array.Empty<string>())
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            options.ExposedHeaders = (options.ExposedHeaders ?? Array.Empty<string>())
                .Where(header => !string.IsNullOrWhiteSpace(header))
                .Select(header => header.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (options.ExposedHeaders.Length == 0)
            {
                options.ExposedHeaders = DefaultExposedHeaders.ToArray();
            }
        }
    }
}
