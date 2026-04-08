namespace CanDoItAll.IPFS.NodeControl.Security;

public static class ControlAppAuthenticationSchemes
{
    public const string ApiKey = "IpfsNodeControl.ControlAppApiKey";
}

public static class ControlAppAuthorizationPolicyNames
{
    public const string AdminApi = "IpfsNodeControl.Authorization.AdminApi";
    public const string RemotePinIngress = "IpfsNodeControl.Authorization.RemotePinIngress";
}

public static class ControlAppRateLimitPolicyNames
{
    public const string AdminApi = "IpfsNodeControl.RateLimit.AdminApi";
    public const string RemotePinIngress = "IpfsNodeControl.RateLimit.RemotePinIngress";
}

public static class ControlAppSecurityHeaders
{
    public const string AdminAccessKey = "X-Ipfs-Admin-Key";
    public const string RemotePinAccessKey = "X-Ipfs-Remote-Pin-Key";
}

internal static class ControlAppSecurityClaims
{
    public const string Permission = "ipfs-node-control.permission";
    public const string Admin = "admin";
    public const string RemotePin = "remote-pin";
}
