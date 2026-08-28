using OpenIddict.Abstractions;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PermissionSystem.Infrastructure.Mcp;

public sealed class McpOAuthClientProvisioner : IMcpOAuthClientProvisioner
{
    private readonly IOpenIddictApplicationManager _applicationManager;

    public McpOAuthClientProvisioner(IOpenIddictApplicationManager applicationManager)
    {
        _applicationManager = applicationManager;
    }

    public async Task CreateAsync(
        McpOAuthClientRegistration registration,
        CancellationToken cancellationToken = default)
    {
        if (await _applicationManager.FindByClientIdAsync(registration.ClientId, cancellationToken) is not null)
        {
            throw new BusinessException(ErrorCode.Conflict, "The MCP OAuth client identifier already exists.");
        }

        await _applicationManager.CreateAsync(CreateDescriptor(registration), cancellationToken);
    }

    public async Task RotateSecretAsync(
        McpOAuthClientRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationManager.FindByClientIdAsync(registration.ClientId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "The MCP OAuth client was not found.");

        await _applicationManager.UpdateAsync(application, CreateDescriptor(registration), cancellationToken);
    }

    private static OpenIddictApplicationDescriptor CreateDescriptor(McpOAuthClientRegistration registration)
    {
        return new OpenIddictApplicationDescriptor
        {
            ClientId = registration.ClientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = registration.ClientSecret,
            DisplayName = registration.DisplayName,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + AiCenterConstants.McpScope
            }
        };
    }
}
