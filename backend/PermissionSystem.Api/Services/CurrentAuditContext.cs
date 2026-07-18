using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Api.Services;

public sealed class CurrentAuditContext : IAuditContext
{
    private readonly ICurrentUserService _currentUserService;

    public CurrentAuditContext(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Guid? UserId => _currentUserService.UserId;
}
