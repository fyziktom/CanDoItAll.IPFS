using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Ipfs.Server
{
    public sealed class HttpApiHostAccessHandler : AuthorizationHandler<HttpApiHostAccessRequirement>
    {
        private readonly IOptionsMonitor<HttpApiHostOptions> hostOptions;

        public HttpApiHostAccessHandler(IOptionsMonitor<HttpApiHostOptions> hostOptions)
        {
            this.hostOptions = hostOptions;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            HttpApiHostAccessRequirement requirement)
        {
            var configuredOptions = hostOptions.CurrentValue;
            if (configuredOptions.RequireAuthentication != true
                || context.User.HasClaim(HttpApiHostSecurityClaims.Permission, HttpApiHostSecurityClaims.Admin))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
