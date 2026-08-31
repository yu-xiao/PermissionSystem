namespace PermissionSystem.Application.Authentication;

public interface IUserCredentialValidator
{
    Task<Guid?> ResolveActiveTenantIdAsync(
        string tenantCodeOrId,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> ValidateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> GetAuthenticationStateAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
