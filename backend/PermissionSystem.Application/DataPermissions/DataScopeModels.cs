using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.DataPermissions;

public sealed class DataScopeContext
{
    public DataScopeType ScopeType { get; init; }

    public Guid? CurrentUserId { get; init; }

    public Guid? CurrentDepartmentId { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];

    public bool HasAllDataScope => ScopeType == DataScopeType.All;
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

public interface IDataScopeService
{
    Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default);

    Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task SetRoleDataScopeAsync(Guid roleId, SetRoleDataScopeRequest request, CancellationToken cancellationToken = default);
}
