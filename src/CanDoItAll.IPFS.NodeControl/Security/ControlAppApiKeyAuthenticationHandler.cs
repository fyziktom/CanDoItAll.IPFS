using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Linq;
using CanDoItAll.IPFS.NodeControl.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.IPFS.NodeControl.Security;

public sealed class ControlAppApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<ControlAppSecurityOptions> securityOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredOptions = securityOptions.CurrentValue;
        if (TryCreateAdminPrincipal(configuredOptions, out var adminPrincipal, out var adminFailure))
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(adminPrincipal, Scheme.Name)));
        }

        if (adminFailure is not null)
        {
            return Task.FromResult(adminFailure);
        }

        if (TryCreateRemotePinPrincipal(configuredOptions, out var remotePinPrincipal, out var remotePinFailure))
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(remotePinPrincipal, Scheme.Name)));
        }

        if (remotePinFailure is not null)
        {
            return Task.FromResult(remotePinFailure);
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }

    private bool TryCreateAdminPrincipal(
        ControlAppSecurityOptions configuredOptions,
        out ClaimsPrincipal principal,
        out AuthenticateResult? failure)
    {
        principal = null!;
        failure = null;

        if (!TryGetSingleHeaderValue(ControlAppSecurityHeaders.AdminAccessKey, out var providedValue))
        {
            return false;
        }

        if (!SecretMatches(providedValue, configuredOptions.AdminAccessKey))
        {
            failure = AuthenticateResult.Fail("The admin access key is invalid.");
            return false;
        }

        principal = BuildPrincipal(
            ControlAppSecurityClaims.Admin,
            ControlAppSecurityClaims.RemotePin);
        return true;
    }

    private bool TryCreateRemotePinPrincipal(
        ControlAppSecurityOptions configuredOptions,
        out ClaimsPrincipal principal,
        out AuthenticateResult? failure)
    {
        principal = null!;
        failure = null;

        if (!TryGetSingleHeaderValue(ControlAppSecurityHeaders.RemotePinAccessKey, out var providedValue))
        {
            return false;
        }

        var expectedValue = configuredOptions.RemotePinAccessKey ?? configuredOptions.AdminAccessKey;
        if (!SecretMatches(providedValue, expectedValue))
        {
            failure = AuthenticateResult.Fail("The remote pin access key is invalid.");
            return false;
        }

        principal = BuildPrincipal(ControlAppSecurityClaims.RemotePin);
        return true;
    }

    private bool TryGetSingleHeaderValue(string headerName, out string value)
    {
        if (!Request.Headers.TryGetValue(headerName, out StringValues values)
            || values.Count == 0)
        {
            value = string.Empty;
            return false;
        }

        var firstValue = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstValue))
        {
            value = string.Empty;
            return false;
        }

        value = firstValue.Trim();
        return true;
    }

    private static bool SecretMatches(string providedValue, string? expectedValue)
        => !string.IsNullOrWhiteSpace(expectedValue)
           && string.Equals(providedValue, expectedValue, StringComparison.Ordinal);

    private ClaimsPrincipal BuildPrincipal(params string[] permissions)
    {
        var claims = permissions
            .Distinct(StringComparer.Ordinal)
            .Select(permission => new Claim(ControlAppSecurityClaims.Permission, permission))
            .ToList();

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return new ClaimsPrincipal(identity);
    }
}
