using System;
using Ipfs.Engine;

namespace Ipfs.Server
{
    public static class HttpApiHostPassphraseResolver
    {
        public const string EnvironmentVariableName = "IPFS_PASS";

        public static IpfsEngine CreateEngine(HttpApiHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var resolvedPassphrase = ResolveRequiredPassphrase(options);
            var chars = resolvedPassphrase.ToCharArray();
            try
            {
                return new IpfsEngine(chars);
            }
            finally
            {
                Array.Clear(chars, 0, chars.Length);
            }
        }

        public static string ResolveRequiredPassphrase(HttpApiHostOptions options, string? environmentPassphrase = null)
        {
            var resolved = ResolvePassphrase(options, environmentPassphrase);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            throw new InvalidOperationException(
                $"No IPFS host passphrase is configured. Set '{HttpApiHostOptions.SectionName}:Passphrase' or the '{EnvironmentVariableName}' environment variable.");
        }

        public static string? ResolvePassphrase(HttpApiHostOptions options, string? environmentPassphrase = null)
        {
            ArgumentNullException.ThrowIfNull(options);

            var configuredPassphrase = NormalizeSecret(options.Passphrase);
            if (!string.IsNullOrWhiteSpace(configuredPassphrase))
            {
                return configuredPassphrase;
            }

            return NormalizeSecret(environmentPassphrase ?? Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        internal static string? NormalizeSecret(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
