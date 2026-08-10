using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.DataPermissions;

public sealed class DataScopeContext
{
    public DataScopeType ScopeType { get; init; }

    public Guid? CurrentUserId { get; init; }

    public Guid? CurrentDepartmentId { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];

    public bool HasAllDataScope => ScopeType == DataScopeType.All;

    public bool? IncludeCurrentUser { get; init; }

    public bool IncludesCurrentUser => IncludeCurrentUser ?? ScopeType == DataScopeType.CurrentUser;
}

public sealed class RoleDataScopeResponse
{
    public Guid RoleId { get; init; }

    public DataScopeType ScopeType { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];
}

public sealed class SetRoleDataScopeRequest
{
    public DataScopeType ScopeType { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];
}

public sealed class UserDataScopeResponse
{
    public Guid UserId { get; init; }

    public bool HasOverride { get; init; }

    public DataScopeType ScopeType { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];
}

public sealed class SetUserDataScopeRequest
{
    public DataScopeType ScopeType { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];
}

public interface IDataScopeService
{
    Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default);

    Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task SetRoleDataScopeAsync(Guid roleId, SetRoleDataScopeRequest request, CancellationToken cancellationToken = default);
}

public interface IUserDataScopeService
{
    Task<UserDataScopeResponse> GetUserDataScopeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SetUserDataScopeAsync(
        Guid userId,
        SetUserDataScopeRequest request,
        CancellationToken cancellationToken = default);

    Task ClearUserDataScopeAsync(Guid userId, CancellationToken cancellationToken = default);
}
