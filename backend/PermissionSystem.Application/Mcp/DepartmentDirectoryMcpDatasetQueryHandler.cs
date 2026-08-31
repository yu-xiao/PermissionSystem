using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Application.Mcp;

public sealed class DepartmentDirectoryMcpDatasetQueryHandler : IMcpDatasetQueryHandler
{
    private readonly IRepository<Department> _departmentRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;

    public DepartmentDirectoryMcpDatasetQueryHandler(
        IRepository<Department> departmentRepository,
        IAsyncQueryExecutor queryExecutor)
    {
        _departmentRepository = departmentRepository;
        _queryExecutor = queryExecutor;
    }

    public string HandlerCode => McpDatasetCodes.DepartmentDirectory;

    public async Task<McpDatasetQueryResponse> QueryAsync(
        McpDatasetQueryContext context,
        CancellationToken cancellationToken = default)
    {
        var query = _departmentRepository.QueryForTenant(context.TenantId);
        if (context.TryGetStringFilter("code", out var code))
        {
            query = query.Where(entity => entity.Code.Contains(code));
        }

        if (context.TryGetStringFilter("name", out var name))
        {
            query = query.Where(entity => entity.Name.Contains(name));
        }

        if (context.TryGetBooleanFilter("isEnabled", out var isEnabled))
        {
            query = query.Where(entity => entity.IsEnabled == isEnabled);
        }

        var rows = await _queryExecutor.ToListAsync(
            query.OrderBy(entity => entity.Code)
                .Take(context.Limit + 1)
                .Select(entity => new DepartmentRow(
                    entity.Code,
                    entity.Name,
                    entity.Parent == null ? null : entity.Parent.Code,
                    entity.IsEnabled)),
            cancellationToken);
        return context.CreateResponse(
            rows.Take(context.Limit).Select(row => context.ProjectRow(field => field switch
            {
                "code" => row.Code,
                "name" => row.Name,
                "parentCode" => row.ParentCode,
                "isEnabled" => row.IsEnabled,
                _ => null
            })).ToList(),
            rows.Count > context.Limit);
    }

    private sealed record DepartmentRow(
        string Code,
        string Name,
        string? ParentCode,
        bool IsEnabled);
}
