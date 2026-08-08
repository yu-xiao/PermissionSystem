using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.Workflows;

public sealed class EA017SqlServerConcurrencyTests
{
    private const string ConnectionEnvName = "PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION";
    private const int ConcurrentRequestCount = 20;

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentApproveRequests_ShouldCommitOnlyOneRecord()
    {
        await AssertConcurrentWorkflowActionsAsync(_ => WorkflowTaskStatus.Approved);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentApproveAndRejectRequests_ShouldCommitOnlyOneRecord()
    {
        await AssertConcurrentWorkflowActionsAsync(index =>
            index % 2 == 0 ? WorkflowTaskStatus.Approved : WorkflowTaskStatus.Rejected);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentWorkflowStarts_ShouldCreateOnlyOneRunningInstance()
    {
        var tenantId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var businessId = $"EA017-{Guid.NewGuid():N}";

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.WorkflowDefinitions.Add(CreateDefinition(tenantId, definitionId));
            await setup.SaveChangesAsync();
        }

        try
        {
            var results = await RunConcurrentAsync(async (index, reachBarrier) =>
            {
                await using var context = CreateContext(tenantId);
                await using var transaction = await context.Database.BeginTransactionAsync();
                context.WorkflowInstances.Add(new WorkflowInstance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = definitionId,
                    DefinitionCode = "EA017",
                    DefinitionName = "EA-017 concurrency",
                    BusinessType = "EA017",
                    BusinessId = businessId,
                    BusinessTitle = $"Concurrent start {index}",
                    StarterUserId = Guid.NewGuid(),
                    StarterUserName = $"starter-{index}",
                    Status = WorkflowInstanceStatus.Running,
                    StartedAt = DateTimeOffset.UtcNow
                });

                await reachBarrier();
                return await SaveAttemptAsync(context, transaction);
            });

            AssertSingleSuccessAndConflicts(results);
            await using var verification = CreateContext(tenantId);
            Assert.Equal(1, await verification.WorkflowInstances.CountAsync(entity =>
                entity.BusinessType == "EA017" &&
                entity.BusinessId == businessId &&
                entity.Status == WorkflowInstanceStatus.Running));
        }
        finally
        {
            await CleanupWorkflowAsync(tenantId, definitionId);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentStateTransitions_ShouldCommitOnlyOneLog()
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.DemoApprovalOrders.Add(new DemoApprovalOrder
            {
                Id = orderId,
                TenantId = tenantId,
                OrderNo = $"EA017-{Guid.NewGuid():N}",
                Title = "EA-017 state transition concurrency",
                Amount = 1,
                ApplicantUserId = Guid.NewGuid(),
                ApplicantUserName = "tester",
                ApprovalStatus = ApprovalStatus.Pending
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            var results = await RunConcurrentAsync(async (index, reachBarrier) =>
            {
                await using var context = CreateContext(tenantId);
                await using var transaction = await context.Database.BeginTransactionAsync();
                var order = await context.DemoApprovalOrders.SingleAsync(entity => entity.Id == orderId);
                var targetState = index % 2 == 0 ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

                await reachBarrier();
                order.ApprovalStatus = targetState;
                context.StateTransitionLogs.Add(new StateTransitionLog
                {
                    TenantId = tenantId,
                    BusinessType = "DemoApprovalOrder",
                    BusinessId = orderId.ToString(),
                    FromState = ApprovalStatus.Pending.ToString(),
                    ToState = targetState.ToString(),
                    ActionCode = targetState.ToString(),
                    ActionName = targetState.ToString(),
                    OperatorUserId = Guid.NewGuid(),
                    OperatorUserName = $"operator-{index}"
                });
                return await SaveAttemptAsync(context, transaction);
            });

            AssertSingleSuccessAndConflicts(results);
            await using var verification = CreateContext(tenantId);
            var order = await verification.DemoApprovalOrders.SingleAsync(entity => entity.Id == orderId);
            Assert.Contains(order.ApprovalStatus, new[] { ApprovalStatus.Approved, ApprovalStatus.Rejected });
            Assert.Equal(1, await verification.StateTransitionLogs.CountAsync(entity =>
                entity.BusinessType == "DemoApprovalOrder" && entity.BusinessId == orderId.ToString()));
        }
        finally
        {
            await using var cleanup = CreateContext(tenantId);
            await cleanup.StateTransitionLogs.IgnoreQueryFilters()
                .Where(entity => entity.TenantId == tenantId && entity.BusinessId == orderId.ToString())
                .ExecuteDeleteAsync();
            await cleanup.DemoApprovalOrders.IgnoreQueryFilters()
                .Where(entity => entity.TenantId == tenantId && entity.Id == orderId)
                .ExecuteDeleteAsync();
        }
    }

    private static async Task AssertConcurrentWorkflowActionsAsync(
        Func<int, WorkflowTaskStatus> statusSelector)
    {
        var tenantId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.WorkflowDefinitions.Add(CreateDefinition(tenantId, definitionId));
            setup.WorkflowInstances.Add(new WorkflowInstance
            {
                Id = instanceId,
                TenantId = tenantId,
                DefinitionId = definitionId,
                DefinitionCode = "EA017",
                DefinitionName = "EA-017 concurrency",
                BusinessType = "EA017",
                BusinessId = $"EA017-{Guid.NewGuid():N}",
                BusinessTitle = "Concurrent approval",
                StarterUserId = Guid.NewGuid(),
                StarterUserName = "starter",
                Status = WorkflowInstanceStatus.Running,
                StartedAt = DateTimeOffset.UtcNow
            });
            setup.WorkflowTasks.Add(new WorkflowTask
            {
                Id = taskId,
                TenantId = tenantId,
                InstanceId = instanceId,
                NodeKey = "approve",
                NodeName = "Approve",
                ApproverUserId = Guid.NewGuid(),
                ApproverUserName = "approver",
                Status = WorkflowTaskStatus.Pending,
                AssignedAt = DateTimeOffset.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            var results = await RunConcurrentAsync(async (index, reachBarrier) =>
            {
                await using var context = CreateContext(tenantId);
                await using var transaction = await context.Database.BeginTransactionAsync();
                var task = await context.WorkflowTasks.SingleAsync(entity => entity.Id == taskId);
                var instance = await context.WorkflowInstances.SingleAsync(entity => entity.Id == instanceId);
                var taskStatus = statusSelector(index);
                var action = taskStatus == WorkflowTaskStatus.Approved
                    ? WorkflowActionType.Approve
                    : WorkflowActionType.Reject;

                await reachBarrier();
                task.Status = taskStatus;
                task.CompletedAt = DateTimeOffset.UtcNow;
                instance.Status = taskStatus == WorkflowTaskStatus.Approved
                    ? WorkflowInstanceStatus.Approved
                    : WorkflowInstanceStatus.Rejected;
                instance.CompletedAt = DateTimeOffset.UtcNow;
                context.WorkflowRecords.Add(new WorkflowRecord
                {
                    TenantId = tenantId,
                    InstanceId = instanceId,
                    TaskId = taskId,
                    NodeKey = task.NodeKey,
                    NodeName = task.NodeName,
                    OperatorUserId = Guid.NewGuid(),
                    OperatorUserName = $"operator-{index}",
                    Action = action,
                    OperatedAt = DateTimeOffset.UtcNow
                });
                return await SaveAttemptAsync(context, transaction);
            });

            AssertSingleSuccessAndConflicts(results);
            await using var verification = CreateContext(tenantId);
            var task = await verification.WorkflowTasks.SingleAsync(entity => entity.Id == taskId);
            var instance = await verification.WorkflowInstances.SingleAsync(entity => entity.Id == instanceId);
            Assert.NotEqual(WorkflowTaskStatus.Pending, task.Status);
            Assert.NotEqual(WorkflowInstanceStatus.Running, instance.Status);
            Assert.Equal(1, await verification.WorkflowRecords.CountAsync(entity => entity.TaskId == taskId));
        }
        finally
        {
            await CleanupWorkflowAsync(tenantId, definitionId);
        }
    }

    private static WorkflowDefinition CreateDefinition(Guid tenantId, Guid definitionId)
    {
        return new WorkflowDefinition
        {
            Id = definitionId,
            TenantId = tenantId,
            Code = $"EA017-{Guid.NewGuid():N}",
            Name = "EA-017 concurrency",
            Version = 1,
            Status = WorkflowDefinitionStatus.Published,
            IsPublished = true,
            PublishedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<IReadOnlyList<ConcurrencyAttempt>> RunConcurrentAsync(
        Func<int, Func<Task>, Task<ConcurrencyAttempt>> action)
    {
        var readyCount = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ReachBarrierAsync()
        {
            if (Interlocked.Increment(ref readyCount) == ConcurrentRequestCount)
            {
                release.SetResult();
            }

            await release.Task;
        }

        var tasks = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(index => Task.Run(() => action(index, ReachBarrierAsync)))
            .ToArray();

        return await Task.WhenAll(tasks);
    }

    private static async Task<ConcurrencyAttempt> SaveAttemptAsync(
        AppDbContext context,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new ConcurrencyAttempt(true, null);
        }
        catch (BusinessException exception) when (exception.ErrorCode == ErrorCode.Conflict)
        {
            return new ConcurrencyAttempt(false, exception.ErrorCode);
        }
    }

    private static void AssertSingleSuccessAndConflicts(IReadOnlyCollection<ConcurrencyAttempt> results)
    {
        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(ConcurrentRequestCount - 1, results.Count(result => result.ErrorCode == ErrorCode.Conflict));
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable(ConnectionEnvName)!)
            .Options;
        return new AppDbContext(options, new TestTenantContext(tenantId), new NullAuditContext());
    }

    private static async Task CleanupWorkflowAsync(Guid tenantId, Guid definitionId)
    {
        await using var cleanup = CreateContext(tenantId);
        await cleanup.WorkflowRecords.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await cleanup.WorkflowTasks.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await cleanup.WorkflowInstances.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await cleanup.WorkflowDefinitions.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId && entity.Id == definitionId)
            .ExecuteDeleteAsync();
    }

    private sealed record ConcurrencyAttempt(bool Succeeded, ErrorCode? ErrorCode);

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvName)))
            {
                Skip = $"Set {ConnectionEnvName} to run SQL Server integration tests.";
            }
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; }

        public string? Source => "Test";

        public bool IsResolved => true;

        public bool IsSuperAdmin => false;

        public bool IsSystemScopeActive => false;

        public bool IsHttpRequest => false;

        public void SetTenant(Guid tenantId, string source)
        {
        }

        public void MarkAsSuperAdmin(bool isSuperAdmin)
        {
        }

        public void MarkAsHttpRequest()
        {
        }
    }
}
