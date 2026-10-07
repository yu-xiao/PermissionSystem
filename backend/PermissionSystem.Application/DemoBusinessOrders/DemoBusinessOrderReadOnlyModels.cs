using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoBusinessOrders;

public static class DemoBusinessOrderReadOnlyContract
{
    public const string DatasetCode = "demo-business-orders-readonly";
    public const string Version = "1.0";
    public const string ViewPermission = "demo-business-order:view";
    public const string EvaluationBasis = "当前租户非删除 Demo 业务单据；当前授权范围与筛选取交集，一条单据 ID 计一单；" +
        "本人按创建人 CreatedBy，部门按 DepartmentId；关键词仅匹配单号或标题，审批状态为当前值。";
    public const string Limitation = "Demo 验证数据，非真实 ERP／WMS 业务；实时读取当前状态，无独立数据水位或历史状态快照；" +
        "普通读隔离下计数与明细读取之间可能变化；不支持时间筛选、金额或状态分组统计。";

    public static DemoBusinessOrderReadOnlyQuery Normalize(DemoBusinessOrderReadOnlyQuery query, int maximumRows = 200)
    {
        var keyword = query.Keyword?.Trim();
        var scope = query.DepartmentScope ?? "Authorized";
        var maximum = Math.Min(maximumRows, 200);
        var limit = query.Limit ?? Math.Min(20, maximum);
        if (keyword?.Length > 100 || query.DepartmentId == Guid.Empty ||
            scope is not ("Authorized" or "CurrentDepartment") || limit < 1 || limit > maximum ||
            (query.ApprovalStatus is not null && (!Enum.TryParse<ApprovalStatus>(query.ApprovalStatus, out var status) ||
                !Enum.IsDefined(status) || status.ToString() != query.ApprovalStatus)))
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid Demo read-only query filters or limit.");
        return new() { Keyword = string.IsNullOrEmpty(keyword) ? null : keyword, ApprovalStatus = query.ApprovalStatus,
            DepartmentId = query.DepartmentId, DepartmentScope = scope, Limit = limit };
    }
}

public sealed class DemoBusinessOrderReadOnlyQuery
{
    public string? Keyword { get; init; }
    public string? ApprovalStatus { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? DepartmentScope { get; init; }
    public int? Limit { get; init; }
}

public sealed record DemoBusinessOrderReadOnlyRow(Guid Id, string OrderNo, string Title,
    string ApprovalStatus, Guid? DepartmentId, DateTimeOffset CreatedAt);

public sealed class DemoBusinessOrderTableData
{
    public long TotalCount { get; init; }
    public List<DemoBusinessOrderReadOnlyRow> Items { get; init; } = [];
    public int DisplayedRowCount => Items.Count;
}

public sealed record DemoBusinessOrderReadOnlyResult(DemoBusinessOrderReadOnlyQuery EffectiveQuery,
    DemoBusinessOrderTableData Data, DataScopeContext Scope, DateTimeOffset QueriedAt);

public interface IDemoBusinessOrderReadOnlyQueryService
{
    Task<DemoBusinessOrderReadOnlyResult> QueryAsync(DemoBusinessOrderReadOnlyQuery query,
        CancellationToken cancellationToken = default);
    Task<bool> CanReadAsync(DemoBusinessOrderReadOnlyQuery query, DemoBusinessOrderTableData data,
        CancellationToken cancellationToken = default);
}
