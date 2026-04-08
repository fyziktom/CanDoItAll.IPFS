using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Ipfs.Server
{
    public sealed class HttpApiHostApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IOptionsMonitor<HttpApiHostOptions> hostOptions;

        public HttpApiHostApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IOptionsMonitor<HttpApiHostOptions> hostOptions)
            : base(options, logger, encoder)
        {
            this.hostOptions = hostOptions;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var configuredOptions = hostOptions.CurrentValue;
            if (configuredOptions.RequireAuthentication != true)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!TryGetSingleHeaderValue(HttpApiHostSecurityHeaders.AdminAccessKey, out var providedValue))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!string.Equals(providedValue, configuredOptions.AdminAccessKey, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("The IPFS host admin access key is invalid."));
            }

            var identity = new ClaimsIdentity(
            [
                new Claim(HttpApiHostSecurityClaims.Permission, HttpApiHostSecurityClaims.Admin)
            ], Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
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
    }
}
