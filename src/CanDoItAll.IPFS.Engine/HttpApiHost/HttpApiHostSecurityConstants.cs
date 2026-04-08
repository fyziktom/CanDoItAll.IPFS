namespace Ipfs.Server
{
    public static class HttpApiHostAuthenticationSchemes
    {
        public const string ApiKey = "IpfsHttpHost.ApiKey";
    }

    public static class HttpApiHostCorsPolicyNames
    {
        public const string Default = "IpfsHttpHost.Cors.Default";
    }

    public static class HttpApiHostSecurityHeaders
    {
        public const string AdminAccessKey = "X-Ipfs-Admin-Key";
    }

    internal static class HttpApiHostSecurityClaims
    {
        public const string Permission = "ipfs-http-host.permission";
        public const string Admin = "admin";
    }
}
