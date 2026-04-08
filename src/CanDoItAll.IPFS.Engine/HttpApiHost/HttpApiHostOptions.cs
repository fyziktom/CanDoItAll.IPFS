using System;

namespace Ipfs.Server
{
    public enum HttpApiHostProfileMode
    {
        Light = 0,
        Pro = 1
    }

    public sealed class HttpApiHostOptions
    {
        public const string SectionName = "IpfsHttpHost";

        public string? AdminAccessKey { get; set; }

        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

        public bool? AllowAnyOrigin { get; set; }

        public string[] ExposedHeaders { get; set; } = Array.Empty<string>();

        public HttpApiHostProfileMode Mode { get; set; } = HttpApiHostProfileMode.Light;

        public string? Passphrase { get; set; }

        public bool? RequireAuthentication { get; set; }
    }
}
