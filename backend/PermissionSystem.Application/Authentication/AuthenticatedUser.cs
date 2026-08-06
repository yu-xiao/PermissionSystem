namespace PermissionSystem.Application.Authentication;

public sealed record AuthenticatedUser(
    Guid UserId,
    string Username,
    Guid TenantId,
    Guid? DepartmentId,
    Guid SecurityStamp,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> PermissionCodes);
