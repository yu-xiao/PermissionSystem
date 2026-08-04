using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Workflows;

public sealed class WorkflowRegressionTests
{
    [Fact]
    public async Task WorkflowDefinition_CanBePublished()
    {
        var fixture = CreateWorkflowFixture();
        var service = new WorkflowDefinitionService(
            fixture.Definitions,
            fixture.Nodes,
            fixture.Edges,
            fixture.Conditions,
            fixture.Instances,
            fixture.Bindings,
            fixture.CurrentUser,
            new TestTenantWriteResolver(),
            new TestUnitOfWork());

        var response = await service.PublishAsync(
            fixture.Definition.Id,
            new PublishWorkflowDefinitionRequest { Remark = "publish" });

        Assert.True(response.IsPublished);
        Assert.Equal(WorkflowDefinitionStatus.Published, response.Status);
        Assert.True(fixture.Bindings.Items.Single().IsEnabled);
    }

    [Fact]
    public async Task StartWorkflow_ShouldCreatePendingTask()
    {
        var fixture = CreateWorkflowFixture(published: true);
        var engine = CreateEngine(fixture);

        var response = await engine.StartAsync(new StartWorkflowInstanceRequest
        {
            BusinessType = "Demo",
            BusinessId = "B001",
            BusinessTitle = "Demo approval"
        });

        Assert.Equal(WorkflowInstanceStatus.Running, response.Status);
        var task = Assert.Single(fixture.Tasks.Items);
        Assert.Equal(TestIds.ApproverUserId, task.ApproverUserId);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
    }

    [Fact]
    public async Task ApproveWorkflow_ShouldCompleteInstance()
    {
        var fixture = CreateWorkflowFixture(published: true);
        var engine = CreateEngine(fixture);
        await engine.StartAsync(new StartWorkflowInstanceRequest
        {
            BusinessType = "Demo",
            BusinessId = "B001",
            BusinessTitle = "Demo approval"
        });
        var task = fixture.Tasks.Items.Single();
        fixture.CurrentUser.UserId = TestIds.ApproverUserId;
        fixture.CurrentUser.Username = "approver";

        await engine.ApproveAsync(task.Id, new WorkflowTaskActionRequest { Comment = "approved" });

        Assert.Equal(WorkflowTaskStatus.Approved, task.Status);
        Assert.Equal(WorkflowInstanceStatus.Approved, fixture.Instances.Items.Single().Status);
        Assert.Contains(WorkflowActionType.Approve, fixture.BusinessHandler.Actions);
    }

    [Fact]
    public async Task RejectWorkflow_ShouldRejectInstance()
    {
        var fixture = CreateWorkflowFixture(published: true);
        var engine = CreateEngine(fixture);
        await engine.StartAsync(new StartWorkflowInstanceRequest
        {
            BusinessType = "Demo",
            BusinessId = "B001",
            BusinessTitle = "Demo approval"
        });
        var task = fixture.Tasks.Items.Single();
        fixture.CurrentUser.UserId = TestIds.ApproverUserId;
        fixture.CurrentUser.Username = "approver";

        await engine.RejectAsync(task.Id, new WorkflowTaskActionRequest { Comment = "rejected" });

        Assert.Equal(WorkflowTaskStatus.Rejected, task.Status);
        Assert.Equal(WorkflowInstanceStatus.Rejected, fixture.Instances.Items.Single().Status);
        Assert.Contains(WorkflowActionType.Reject, fixture.BusinessHandler.Actions);
    }

    [Fact]
    public async Task NonApprover_CannotApproveTask()
    {
        var fixture = CreateWorkflowFixture(published: true);
        var engine = CreateEngine(fixture);
        await engine.StartAsync(new StartWorkflowInstanceRequest
        {
            BusinessType = "Demo",
            BusinessId = "B001",
            BusinessTitle = "Demo approval"
        });
        var task = fixture.Tasks.Items.Single();
        fixture.CurrentUser.UserId = Guid.Parse("30000000-0000-0000-0000-000000000099");

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            engine.ApproveAsync(task.Id, new WorkflowTaskActionRequest { Comment = "not mine" }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
    }

    private static WorkflowEngine CreateEngine(WorkflowFixture fixture)
    {
        return new WorkflowEngine(
            fixture.Definitions,
            fixture.Bindings,
            fixture.Nodes,
            fixture.Edges,
            fixture.Conditions,
            fixture.Instances,
            fixture.Tasks,
            fixture.Records,
            fixture.Ccs,
            fixture.Users,
            new WorkflowConditionEvaluator(),
            new WorkflowApproverResolver(fixture.Users, new InMemoryRepository<UserRole>()),
            new TestWorkflowBusinessHandlerResolver(fixture.BusinessHandler),
            new TestNotificationService(),
            fixture.CurrentUser,
            new TestUnitOfWork(),
            NullLogger<WorkflowEngine>.Instance);
    }

    private static WorkflowFixture CreateWorkflowFixture(bool published = false)
    {
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "DEMO",
            Name = "Demo workflow",
            Version = 1,
            IsPublished = published,
            Status = published ? WorkflowDefinitionStatus.Published : WorkflowDefinitionStatus.Draft
        };
        var binding = new WorkflowBusinessBinding
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            BusinessType = "Demo",
            BusinessName = "Demo",
            DefinitionId = definition.Id,
            DefinitionCode = definition.Code,
            DefinitionName = definition.Name,
            IsEnabled = published
        };
        var nodes = new[]
        {
            new WorkflowNode
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DefinitionId = definition.Id,
                NodeKey = "start",
                NodeName = "Start",
                NodeType = WorkflowNodeType.Start,
                Sort = 1
            },
            new WorkflowNode
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DefinitionId = definition.Id,
                NodeKey = "approve",
                NodeName = "Approve",
                NodeType = WorkflowNodeType.Approver,
                ApproverType = WorkflowApproverType.Users,
                ApproverIds = TestIds.ApproverUserId.ToString(),
                ApprovalMode = WorkflowApprovalMode.Single,
                Sort = 2
            },
            new WorkflowNode
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DefinitionId = definition.Id,
                NodeKey = "end",
                NodeName = "End",
                NodeType = WorkflowNodeType.End,
                Sort = 3
            }
        };
        var edges = new[]
        {
            new WorkflowEdge
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DefinitionId = definition.Id,
                FromNodeKey = "start",
                ToNodeKey = "approve",
                Sort = 1
            },
            new WorkflowEdge
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DefinitionId = definition.Id,
                FromNodeKey = "approve",
                ToNodeKey = "end",
                Sort = 2
            }
        };
        var users = new[]
        {
            new User
            {
                Id = TestIds.NormalUserId,
                TenantId = TestIds.TenantId,
                UserName = "starter",
                NormalizedUserName = "STARTER",
                DisplayName = "Starter",
                PasswordHash = "x",
                IsEnabled = true
            },
            new User
            {
                Id = TestIds.ApproverUserId,
                TenantId = TestIds.TenantId,
                UserName = "approver",
                NormalizedUserName = "APPROVER",
                DisplayName = "Approver",
                PasswordHash = "x",
                IsEnabled = true
            }
        };
        var currentUser = new TestCurrentUserService(TestIds.NormalUserId)
        {
            Username = "starter"
        };

        return new WorkflowFixture(
            definition,
            new InMemoryRepository<WorkflowDefinition>(definition),
            new InMemoryRepository<WorkflowBusinessBinding>(binding),
            new InMemoryRepository<WorkflowNode>(nodes),
            new InMemoryRepository<WorkflowEdge>(edges),
            new InMemoryRepository<WorkflowCondition>(),
            new InMemoryRepository<WorkflowInstance>(),
            new InMemoryRepository<WorkflowTask>(),
            new InMemoryRepository<WorkflowRecord>(),
            new InMemoryRepository<WorkflowCc>(),
            new InMemoryRepository<User>(users),
            currentUser,
            new TestWorkflowBusinessHandler());
    }

    private sealed record WorkflowFixture(
        WorkflowDefinition Definition,
        InMemoryRepository<WorkflowDefinition> Definitions,
        InMemoryRepository<WorkflowBusinessBinding> Bindings,
        InMemoryRepository<WorkflowNode> Nodes,
        InMemoryRepository<WorkflowEdge> Edges,
        InMemoryRepository<WorkflowCondition> Conditions,
        InMemoryRepository<WorkflowInstance> Instances,
        InMemoryRepository<WorkflowTask> Tasks,
        InMemoryRepository<WorkflowRecord> Records,
        InMemoryRepository<WorkflowCc> Ccs,
        InMemoryRepository<User> Users,
        TestCurrentUserService CurrentUser,
        TestWorkflowBusinessHandler BusinessHandler);
}
