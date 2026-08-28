using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiReadOnlyToolRegistryTests
{
    [Fact]
    public void GetAvailableTools_RequiresAiAndOriginalBusinessPermissions()
    {
        var currentUser = new TestCurrentUserService(permissions:
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.UserQueryPermission
        ]);
        var registry = CreateRegistry(currentUser);

        var tools = registry.GetAvailableTools();

        Assert.DoesNotContain(tools, tool => tool.ToolCode == "permission.users.search");
        Assert.DoesNotContain(tools, tool => tool.ToolCode == "permission.reports.query_dataset");
    }

    [Fact]
    public async Task UserSearch_AppliesCurrentUserDataScopeBeforeReturningRows()
    {
        var currentUser = new TestCurrentUserService(
            userId: TestIds.NormalUserId,
            permissions:
            [
                AiCenterConstants.ToolQueryPermission,
                AiCenterConstants.UserQueryPermission,
                "system:user:view"
            ]);
        var users = new InMemoryRepository<User>(
            new User
            {
                Id = TestIds.NormalUserId,
                TenantId = TestIds.TenantId,
                UserName = "visible-user",
                DisplayName = "Visible User",
                IsEnabled = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                UserName = "hidden-user",
                DisplayName = "Hidden User",
                Email = "hidden@example.test",
                PhoneNumber = "13800000000",
                IsEnabled = true
            });
        var registry = CreateRegistry(
            currentUser,
            users: users,
            dataScope: new DataScopeContext
            {
                ScopeType = DataScopeType.CurrentUser,
                CurrentUserId = TestIds.NormalUserId
            });

        var result = await registry.ExecuteAsync("permission.users.search", "{\"limit\":20}");

        Assert.Equal(1, result.RowCount);
        Assert.Contains("visible-user", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden-user", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden@example.test", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("13800000000", result.ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationLogSummary_DoesNotReturnSensitiveLogPayloads()
    {
        var currentUser = new TestCurrentUserService(permissions:
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.OperationLogQueryPermission,
            "system:operation-log:view"
        ]);
        var logs = new InMemoryRepository<OperationLog>(new OperationLog
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            UserName = "tester",
            Module = "Users",
            Action = "Create",
            Method = "Users.Create",
            RequestMethod = "POST",
            RequestBody = "{\"apiKey\":\"secret-value\"}",
            ResponseBody = "sensitive-response",
            IpAddress = "192.0.2.10",
            UserAgent = "sensitive-user-agent",
            StatusCode = 200,
            CreatedAt = DateTimeOffset.UtcNow
        });
        var registry = CreateRegistry(currentUser, operationLogs: logs);

        var result = await registry.ExecuteAsync("permission.operation_logs.summary", "{}");

        using var document = JsonDocument.Parse(result.ContentJson);
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt64());
        Assert.DoesNotContain("secret-value", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-response", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.10", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-user-agent", result.ContentJson, StringComparison.Ordinal);
    }

    private static AiReadOnlyToolRegistry CreateRegistry(
        TestCurrentUserService currentUser,
        InMemoryRepository<User>? users = null,
        InMemoryRepository<OperationLog>? operationLogs = null,
        DataScopeContext? dataScope = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Claims");
        return new AiReadOnlyToolRegistry(
            currentUser,
            tenantContext,
            new TestDataScopeService(dataScope ?? new DataScopeContext { ScopeType = DataScopeType.All }),
            new DataPermissionFilter(),
            new TestDepartmentService(),
            new TestReportService(),
            users ?? new InMemoryRepository<User>(),
            new InMemoryRepository<Role>(),
            new InMemoryRepository<LoginLog>(),
            operationLogs ?? new InMemoryRepository<OperationLog>(),
            new InMemoryRepository<ReportDefinition>(),
            new InMemoryAsyncQueryExecutor());
    }

    private sealed class TestDataScopeService : IDataScopeService
    {
        private readonly DataScopeContext _scope;

        public TestDataScopeService(DataScopeContext scope)
        {
            _scope = scope;
        }

        public Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_scope);
        }

        public Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetRoleDataScopeAsync(Guid roleId, SetRoleDataScopeRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestDepartmentService : IDepartmentService
    {
        public Task<IReadOnlyList<DepartmentTreeResponse>> GetTreeAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DepartmentTreeResponse>>([]);
        }

        public Task<DepartmentTreeResponse> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DepartmentTreeResponse> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetEnabledAsync(Guid id, SetDepartmentEnabledRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestReportService : IReportService
    {
        public Task<PagedResult<ReportDefinitionResponse>> GetPagedAsync(ReportDefinitionQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ReportDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ReportDefinitionResponse> CreateAsync(CreateReportDefinitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ReportDefinitionResponse> UpdateAsync(Guid id, UpdateReportDefinitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ReportDatasetResponse>> GetDatasetsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ReportQueryResponse> QueryAsync(Guid id, ReportQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<byte[]> ExportAsync(Guid id, ReportQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PagedResult<ReportExecutionLogResponse>> GetExecutionLogsAsync(ReportExecutionLogQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
