using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.Users;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Performance;

public sealed class EA021QueryPerformanceTests
{
    [Fact]
    public async Task UserPagedQuery_ShouldUseFixedQueryCountAndBatchRoles()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "Operator",
            Name = "Operator"
        };
        var users = Enumerable.Range(1, 25)
            .Select(index => new User
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                UserName = $"user-{index:00}",
                NormalizedUserName = $"USER-{index:00}",
                DisplayName = $"User {index:00}",
                PasswordHash = "large-sensitive-value",
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
            })
            .ToArray();
        var userRoles = users.Select(user => new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            UserId = user.Id,
            RoleId = role.Id
        }).ToArray();
        var executor = new InMemoryAsyncQueryExecutor();
        var service = new UserService(
            new InMemoryRepository<User>(users),
            new InMemoryRepository<Role>(role),
            new InMemoryRepository<UserRole>(userRoles),
            new InMemoryRepository<Department>(),
            new TestPasswordHashService(),
            new TestExcelService(),
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            new TestUserSessionService(),
            new TestTokenRevocationService(),
            NullLogger<UserService>.Instance,
            new TestUnitOfWork(),
            executor);
        using var cancellationSource = new CancellationTokenSource();

        var result = await service.GetPagedAsync(
            new UserQueryRequest { PageIndex = 2, PageSize = 10 },
            cancellationSource.Token);

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal([role.Id], item.RoleIds));
        Assert.Equal(3, executor.ExecutionCount);
        Assert.Equal([10, 10], executor.MaterializedItemCounts);
        Assert.Equal(cancellationSource.Token, executor.LastCancellationToken);
    }

    [Fact]
    public async Task WorkflowTodoQuery_ShouldFilterAndPageBeforeMaterialization()
    {
        var instances = Enumerable.Range(1, 100)
            .Select(index => new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionCode = "expense",
                DefinitionName = "Expense Approval",
                BusinessType = "Expense",
                BusinessId = $"EXP-{index:000}",
                BusinessTitle = $"Expense Order {index:000}",
                StarterUserId = TestIds.NormalUserId,
                StarterUserName = "starter",
                FormDataJson = new string('x', 4096),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-index),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
            })
            .ToArray();
        var tasks = instances.Select((instance, index) => new WorkflowTask
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            InstanceId = instance.Id,
            NodeKey = "approve",
            NodeName = "Manager Approval",
            ApproverUserId = TestIds.ApproverUserId,
            ApproverUserName = "approver",
            Status = WorkflowTaskStatus.Pending,
            AssignedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
        }).ToArray();
        var executor = new InMemoryAsyncQueryExecutor();
        var service = new WorkflowTaskService(
            new InMemoryRepository<WorkflowInstance>(instances),
            new InMemoryRepository<WorkflowTask>(tasks),
            new InMemoryRepository<WorkflowRecord>(),
            new InMemoryRepository<WorkflowCc>(),
            new TestCurrentUserService(TestIds.ApproverUserId),
            new TestUnitOfWork(),
            executor);

        var result = await service.GetTodoAsync(new WorkflowTaskQueryRequest
        {
            PageIndex = 3,
            PageSize = 10,
            Keyword = "Expense Order"
        });

        Assert.Equal(100, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(2, executor.ExecutionCount);
        Assert.Equal([10], executor.MaterializedItemCounts);
        Assert.All(result.Items, item => Assert.StartsWith("Expense Order", item.BusinessTitle));
    }

    [Fact]
    public async Task RolePagedQuery_ShouldPreserveSuperAdminFlag()
    {
        var executor = new InMemoryAsyncQueryExecutor();
        var service = new RoleService(
            new InMemoryRepository<Role>(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                Code = "SuperAdmin",
                Name = "Super Admin",
                IsBuiltin = true
            }),
            new InMemoryRepository<RoleMenu>(),
            new InMemoryRepository<RolePermission>(),
            new InMemoryRepository<Menu>(),
            new InMemoryRepository<PermissionSystem.Domain.Entities.Permission>(),
            new InMemoryRepository<User>(),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<RoleDataScope>(),
            new InMemoryRepository<Department>(),
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            NullLogger<RoleService>.Instance,
            new TestUnitOfWork(),
            executor);

        var result = await service.GetPagedAsync(new RoleQueryRequest());

        Assert.True(Assert.Single(result.Items).IsSuperAdminRole);
        Assert.Equal(2, executor.ExecutionCount);
    }

    [Fact]
    public async Task ReportPagedQuery_ShouldReturnLightweightRowsWithoutNPlusOne()
    {
        var reports = Enumerable.Range(1, 30)
            .Select(index => new ReportDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                ReportCode = $"REPORT-{index:00}",
                ReportName = $"Report {index:00}",
                Category = "System",
                DataSourceType = "Dataset",
                DatasetKey = "system-users",
                ColumnsJson = new string('c', 4096),
                ParamsJson = new string('p', 4096),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
            })
            .ToArray();
        var parameters = reports.Select(report => new ReportQueryParam
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ReportId = report.Id,
            ParamCode = "keyword",
            ParamName = "Keyword"
        }).ToArray();
        var executor = new InMemoryAsyncQueryExecutor();
        var service = new ReportService(
            new InMemoryRepository<ReportDefinition>(reports),
            new InMemoryRepository<ReportQueryParam>(parameters),
            new InMemoryRepository<ReportExecutionLog>(),
            new TestReportQueryExecutor(),
            new TestExcelService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            new TestReportDatasetCatalog(),
            executor);

        var result = await service.GetPagedAsync(new ReportDefinitionQueryRequest
        {
            PageIndex = 1,
            PageSize = 10
        });

        Assert.Equal(30, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Null(item.ColumnsJson);
            Assert.Null(item.ParamsJson);
            Assert.Empty(item.QueryParams);
        });
        Assert.Equal(2, executor.ExecutionCount);
        Assert.Equal([10], executor.MaterializedItemCounts);
    }

    [Fact]
    public async Task OutboxPagedQuery_ShouldNotMaterializePayloadOrHeaders()
    {
        var messages = Enumerable.Range(1, 40)
            .Select(index => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                MessageId = $"message-{index:00}",
                Exchange = "events",
                RoutingKey = "user.changed",
                MessageType = "UserChanged",
                Payload = new string('x', 8192),
                Headers = new string('h', 4096),
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
            })
            .ToArray();
        var executor = new InMemoryAsyncQueryExecutor();
        var service = new OutboxService(
            new InMemoryRepository<OutboxMessage>(messages),
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            new TraceContextAccessor(),
            new TestUnitOfWork(),
            executor);

        var result = await service.GetPagedAsync(new OutboxMessageQueryRequest
        {
            PageIndex = 2,
            PageSize = 10,
            TenantId = TestIds.TenantId
        });

        Assert.Equal(40, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.All(result.Items, item => Assert.Null(item.Headers));
        Assert.Equal(2, executor.ExecutionCount);
        Assert.Equal([10], executor.MaterializedItemCounts);
    }

    private sealed class TestReportQueryExecutor : IReportQueryExecutor
    {
        public Task<ReportExecutionResult> ExecuteAsync(
            ReportExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ReportExecutionResult());
        }
    }

    private sealed class TestReportDatasetCatalog : IReportDatasetCatalog
    {
        public IReadOnlyList<ReportDatasetResponse> GetAvailable() => [];

        public ReportDatasetDefinition GetRequired(string datasetKey)
        {
            return new ReportDatasetDefinition { Key = datasetKey };
        }
    }
}
