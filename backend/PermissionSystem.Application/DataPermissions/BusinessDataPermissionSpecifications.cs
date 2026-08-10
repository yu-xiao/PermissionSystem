using System.Linq.Expressions;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.DataPermissions;

public sealed class DemoBusinessOrderDataPermissionSpecification
    : IDataPermissionSpecification<DemoBusinessOrder>
{
    public Expression<Func<DemoBusinessOrder, Guid?>> UserIdSelector => entity => entity.CreatedBy;

    public Expression<Func<DemoBusinessOrder, Guid?>> DepartmentIdSelector => entity => entity.DepartmentId;
}

public sealed class DemoApprovalOrderDataPermissionSpecification
    : IDataPermissionSpecification<DemoApprovalOrder>
{
    public Expression<Func<DemoApprovalOrder, Guid?>> UserIdSelector => entity => entity.ApplicantUserId;

    public Expression<Func<DemoApprovalOrder, Guid?>> DepartmentIdSelector => entity => entity.DepartmentId;
}
