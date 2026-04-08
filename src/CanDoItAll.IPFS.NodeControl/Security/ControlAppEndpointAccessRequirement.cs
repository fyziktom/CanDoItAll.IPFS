using Microsoft.AspNetCore.Authorization;

namespace CanDoItAll.IPFS.NodeControl.Security;

public sealed class ControlAppEndpointAccessRequirement(
    string requiredPermission) : IAuthorizationRequirement
{
    public string RequiredPermission { get; } = requiredPermission;
}
