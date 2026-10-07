using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiRunExecutionIdentity : ICurrentUserService, IAuditContext
{
    private AuthenticatedUser? _actor;
    public bool IsAuthenticated => _actor is not null;
    public Guid? UserId => _actor?.UserId;
    public Guid? TenantId => _actor?.TenantId;
    public Guid? DepartmentId => _actor?.DepartmentId;
    public string? SessionId { get; private set; }
    public Guid? SecurityStamp => _actor?.SecurityStamp;
    public string? Username => _actor?.Username;
    public IReadOnlyCollection<string> Roles => _actor?.Roles ?? [];
    public IReadOnlyCollection<string> PermissionCodes => _actor?.PermissionCodes ?? [];
    public bool IsSuperAdmin => PermissionEvaluation.IsSuperAdmin(Roles);
    public bool IsCurrentUserSuperAdmin() => IsSuperAdmin;
    public bool IsCurrentUserAdmin() => IsSuperAdmin;
    public bool CanManageBuiltinResources() => IsSuperAdmin;
    public bool HasPermission(string code) => PermissionEvaluation.HasPermission(IsAuthenticated, IsSuperAdmin, PermissionCodes, code);
    public void Set(AuthenticatedUser actor, string sessionId) { _actor = actor; SessionId = sessionId; }
}

public sealed class AiRunIdentityValidator(IUserCredentialValidator identities, IUserSessionStatusChecker sessions,
    IAiCenterConfiguration configuration)
{
    public async Task<AuthenticatedUser> ValidateAsync(AiRun run, CancellationToken cancellationToken = default)
    {
        if (!configuration.Enabled || !configuration.AllowedTenantIds.Contains(run.TenantId) ||
            string.IsNullOrWhiteSpace(run.ActorSessionId) || !run.ActorSecurityStamp.HasValue)
            throw new BusinessException(ErrorCode.Forbidden, "AI background identity is unavailable.");
        var actor = await identities.GetAuthenticationStateAsync(run.TenantId, run.ActorUserId, cancellationToken);
        if (actor is null || actor.SecurityStamp != run.ActorSecurityStamp ||
            await sessions.ValidateAccessAsync(run.TenantId, run.ActorUserId, run.ActorSessionId,
                run.ActorSecurityStamp.Value, cancellationToken) != UserAccessValidationStatus.Valid ||
            !PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(actor.Roles),
                actor.PermissionCodes, AiCenterConstants.ChatUsePermission))
            throw new BusinessException(ErrorCode.Unauthorized, "AI background authorization is no longer valid.");
        return actor;
    }
}
