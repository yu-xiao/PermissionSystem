using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiReadOnlyToolRegistryTests
{
    [Fact]
    public void AddAiCenterCore_RegistersEachReadOnlyHandlerOnce()
    {
        var services = new ServiceCollection();

        services.AddAiCenterCore();
        services.AddAiCenterCore();

        Assert.Equal(
            6,
            services.Count(descriptor =>
                descriptor.ServiceType == typeof(IAiReadOnlyToolHandler)));
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IAiReadOnlyToolRegistry));
    }

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
            },
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                UserName = "cross-tenant-user",
                DisplayName = "Cross Tenant User",
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
        Assert.DoesNotContain("cross-tenant-user", result.ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserSearch_EnforcesExecutionContextTenantWithAllDataScope()
    {
        var currentUser = new TestCurrentUserService(permissions:
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.UserQueryPermission,
            "system:user:view"
        ]);
        var users = new InMemoryRepository<User>(
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                UserName = "current-tenant-user",
                DisplayName = "Current Tenant User"
            },
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                UserName = "cross-tenant-user",
                DisplayName = "Cross Tenant User"
            });
        var registry = CreateRegistry(currentUser, users: users);

        var result = await registry.ExecuteAsync("permission.users.search", "{}");

        Assert.Equal(1, result.RowCount);
        Assert.Contains("current-tenant-user", result.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("cross-tenant-user", result.ContentJson, StringComparison.Ordinal);
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

    [Fact]
    public void GetAvailableTools_ProvidesSelfContainedModelAndGovernanceMetadata()
    {
        var currentUser = new TestCurrentUserService(permissions:
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.UserQueryPermission,
            "system:user:view"
        ]);
        var registry = CreateRegistry(currentUser);

        var tool = Assert.Single(
            registry.GetAvailableTools(),
            item => item.ToolCode == "permission.users.search");

        Assert.Equal("search_users", tool.FunctionName);
        Assert.Equal(AiToolDataScopePolicies.CurrentUserDataScope, tool.DataScopePolicy);
        Assert.Contains("system:user:view", tool.RequiredPermissions);
        Assert.False(string.IsNullOrWhiteSpace(tool.OutputSchemaJson));
        Assert.Equal(200, tool.MaxRows);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsArgumentsOutsideThePublishedSchema()
    {
        var currentUser = new TestCurrentUserService(permissions:
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.UserQueryPermission,
            "system:user:view"
        ]);
        var registry = CreateRegistry(currentUser);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            registry.ExecuteAsync(
                "permission.users.search",
                "{\"limit\":20,\"tenantId\":\"00000000-0000-0000-0000-000000000000\"}"));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public void Constructor_RejectsDuplicateFunctionNames()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Claims");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AiReadOnlyToolRegistry(
                [
                    new TestReadOnlyToolHandler("test.first", "duplicate_function"),
                    new TestReadOnlyToolHandler("test.second", "duplicate_function")
                ],
                new TestCurrentUserService(),
                tenantContext,
                new TraceContextAccessor()));

        Assert.Contains("function name", exception.Message, StringComparison.Ordinal);
    }

    private static AiReadOnlyToolRegistry CreateRegistry(
        TestCurrentUserService currentUser,
        InMemoryRepository<User>? users = null,
        InMemoryRepository<OperationLog>? operationLogs = null,
        DataScopeContext? dataScope = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Claims");
        var queryExecutor = new InMemoryAsyncQueryExecutor();
        var dataScopeService = new TestDataScopeService(
            dataScope ?? new DataScopeContext { ScopeType = DataScopeType.All });
        var departmentService = new TestDepartmentService();
        var reportService = new TestReportService();
        var handlers = new IAiReadOnlyToolHandler[]
        {
            new UserSearchAiToolHandler(
                dataScopeService,
                new DataPermissionFilter(),
                users ?? new InMemoryRepository<User>(),
                queryExecutor),
            new DepartmentSearchAiToolHandler(departmentService),
            new RoleSummaryAiToolHandler(new InMemoryRepository<Role>(), queryExecutor),
            new LoginLogSummaryAiToolHandler(new InMemoryRepository<LoginLog>(), queryExecutor),
            new OperationLogSummaryAiToolHandler(
                operationLogs ?? new InMemoryRepository<OperationLog>(),
                queryExecutor),
            new ReportDatasetQueryAiToolHandler(
                dataScopeService,
                reportService,
                new InMemoryRepository<ReportDefinition>())
        };
        return new AiReadOnlyToolRegistry(
            handlers,
            currentUser,
            tenantContext,
            new TraceContextAccessor());
    }

    private sealed class TestReadOnlyToolHandler : IAiReadOnlyToolHandler
    {
        public TestReadOnlyToolHandler(string toolCode, string functionName)
        {
            Definition = new AiToolDefinition
            {
                ToolCode = toolCode,
                FunctionName = functionName,
                Version = "1.0",
                Description = "Test tool.",
                InputSchemaJson = "{\"type\":\"object\"}",
                OutputSchemaJson = "{\"type\":\"object\"}",
                DataClassification = "Internal",
                DataScopePolicy = AiToolDataScopePolicies.CurrentTenant,
                RequiredPermissions = [AiCenterConstants.ToolQueryPermission]
            };
        }

        public AiToolDefinition Definition { get; }

        public bool IsEnabled => true;

        public Task<AiToolExecutionResult> ExecuteAsync(
            AiToolExecutionContext context,
            string argumentsJson,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
