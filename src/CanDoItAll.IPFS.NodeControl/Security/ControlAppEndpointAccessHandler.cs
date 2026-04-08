using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using CanDoItAll.IPFS.NodeControl.Options;
using Microsoft.Extensions.Options;

namespace CanDoItAll.IPFS.NodeControl.Security;

public sealed class ControlAppEndpointAccessHandler(IOptionsMonitor<ControlAppSecurityOptions> securityOptions)
    : AuthorizationHandler<ControlAppEndpointAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ControlAppEndpointAccessRequirement requirement)
    {
        if (context.User.HasClaim(ControlAppSecurityClaims.Permission, requirement.RequiredPermission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (AllowsAnonymousLocalRequests(requirement.RequiredPermission)
            && context.Resource is HttpContext httpContext
            && ControlAppRequestOriginEvaluator.IsTrustedLocal(httpContext))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private bool AllowsAnonymousLocalRequests(string requiredPermission)
    {
        var configuredOptions = securityOptions.CurrentValue;
        return string.Equals(requiredPermission, ControlAppSecurityClaims.Admin, StringComparison.Ordinal)
            ? configuredOptions.AllowAnonymousLocalAdmin == true
            : string.Equals(requiredPermission, ControlAppSecurityClaims.RemotePin, StringComparison.Ordinal)
                && configuredOptions.AllowAnonymousLocalRemotePin == true;
    }
}

internal static class ControlAppRequestOriginEvaluator
{
    public static bool IsTrustedLocal(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var address = httpContext.Connection.RemoteIpAddress;
        if (address is null)
        {
            return true;
        }

        address = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 0xfc || bytes[0] == 0xfd;
        }

        return false;
    }
}
