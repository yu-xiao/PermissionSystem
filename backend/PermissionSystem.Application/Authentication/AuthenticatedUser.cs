namespace PermissionSystem.Application.Authentication;

public sealed record AuthenticatedUser(
    Guid UserId,
    string Username,
    Guid TenantId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> PermissionCodes);
