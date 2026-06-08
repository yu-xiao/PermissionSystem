using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DemoApprovalOrders;
using PermissionSystem.Application.NumberRules;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Tests;

public sealed class DemoApprovalOrderPlatformIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CreateAsync_ShouldGenerateOrderNoByNumberRule()
    {
        var orderRepository = new InMemoryRepository<DemoApprovalOrder>();
        var numberGenerator = new TestNumberGenerator("DAO202606080001");
        var service = new DemoApprovalOrderService(
            orderRepository,
            new TestWorkflowEngine(),
            numberGenerator,
            new TestStateTransitionExecutor(),
            new TestCurrentUserService(["demo-approval-order:create"]),
            new TestUnitOfWork());

        var response = await service.CreateAsync(new CreateDemoApprovalOrderRequest
        {
            TenantId = TenantId,
            Title = "测试审批单",
            Amount = 128.50m
        });

        Assert.Equal("DAO202606080001", response.OrderNo);
        Assert.Equal(DemoApprovalOrderConstants.NumberRuleCode, numberGenerator.RuleCodes.Single());
        Assert.Equal("DAO202606080001", orderRepository.Items.Single().OrderNo);
    }

    [Fact]
    public async Task SubmitAsync_ShouldValidateStateMachineBeforeStartingWorkflow()
    {
        var order = CreateOrder(ApprovalStatus.Draft);
        var workflowEngine = new TestWorkflowEngine();
        var stateExecutor = new TestStateTransitionExecutor();
        var service = new DemoApprovalOrderService(
            new InMemoryRepository<DemoApprovalOrder>(order),
            workflowEngine,
            new TestNumberGenerator("DAO202606080001"),
            stateExecutor,
            new TestCurrentUserService(["demo-approval-order:submit"]),
            new TestUnitOfWork());

        await service.SubmitAsync(order.Id, new SubmitDemoApprovalOrderRequest { Remark = "提交审批" });

        Assert.Equal("Submit", stateExecutor.Validations.Single().ActionCode);
        Assert.Equal("Draft", stateExecutor.Validations.Single().CurrentState);
        Assert.NotNull(workflowEngine.StartRequest);
        Assert.Equal(DemoApprovalOrderConstants.BusinessType, workflowEngine.StartRequest.BusinessType);
        Assert.StartsWith(order.OrderNo, workflowEngine.StartRequest.BusinessTitle, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ApprovalStatus.Draft, "Submit", ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Pending, "Approve", ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Pending, "Reject", ApprovalStatus.Rejected)]
    [InlineData(ApprovalStatus.Pending, "Withdraw", ApprovalStatus.Withdrawn)]
    [InlineData(ApprovalStatus.Draft, "Cancel", ApprovalStatus.Cancelled)]
    public async Task StateTransitionExecutor_ShouldKeepDemoApprovalOrderStatusAligned(
        ApprovalStatus initialStatus,
        string actionCode,
        ApprovalStatus expectedStatus)
    {
        var order = CreateOrder(initialStatus);
        var machine = new StateMachineDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BusinessType = DemoApprovalOrderConstants.BusinessType,
            Name = "Demo 审批单状态机",
            IsEnabled = true
        };
        var transition = new StateTransition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            MachineId = machine.Id,
            FromState = initialStatus.ToString(),
            ToState = expectedStatus.ToString(),
            ActionCode = actionCode,
            ActionName = actionCode,
            RequiredPermission = ResolvePermission(actionCode),
            IsEnabled = true,
            Sort = 1
        };
        var orderRepository = new InMemoryRepository<DemoApprovalOrder>(order);
        var logRepository = new InMemoryRepository<StateTransitionLog>();
        var handler = new DemoApprovalOrderStateTransitionHandler(orderRepository);
        var executor = new StateTransitionExecutor(
            new InMemoryRepository<StateMachineDefinition>(machine),
            new InMemoryRepository<StateTransition>(transition),
            logRepository,
            new StateTransitionHandlerResolver([handler]),
            new TestCurrentUserService([ResolvePermission(actionCode)]),
            new TestUnitOfWork());

        await executor.ExecuteTransitionAsync(
            DemoApprovalOrderConstants.BusinessType,
            order.Id.ToString(),
            actionCode,
            actionCode);

        Assert.Equal(expectedStatus, order.ApprovalStatus);
        var log = Assert.Single(logRepository.Items);
        Assert.Equal(actionCode, log.ActionCode);
        Assert.Equal(initialStatus.ToString(), log.FromState);
        Assert.Equal(expectedStatus.ToString(), log.ToState);
    }

    [Fact]
    public async Task CancelAsync_ShouldExecuteStateMachineCancelAction()
    {
        var order = CreateOrder(ApprovalStatus.Draft);
        var stateExecutor = new TestStateTransitionExecutor((businessId, actionCode) =>
        {
            if (businessId == order.Id.ToString() && actionCode == "Cancel")
            {
                order.ApprovalStatus = ApprovalStatus.Cancelled;
            }
        });
        var service = new DemoApprovalOrderService(
            new InMemoryRepository<DemoApprovalOrder>(order),
            new TestWorkflowEngine(),
            new TestNumberGenerator("DAO202606080001"),
            stateExecutor,
            new TestCurrentUserService(["demo-approval-order:cancel"]),
            new TestUnitOfWork());

        var response = await service.CancelAsync(order.Id, new WorkflowTaskActionRequest { Comment = "取消" });

        Assert.Equal(ApprovalStatus.Cancelled, response.ApprovalStatus);
        Assert.Equal("Cancel", stateExecutor.Executions.Single().ActionCode);
    }

    private static DemoApprovalOrder CreateOrder(ApprovalStatus status)
    {
        return new DemoApprovalOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            OrderNo = "DAO202606080001",
            Title = "测试审批单",
            Amount = 100,
            ApplicantUserId = UserId,
            ApplicantUserName = "tester",
            ApprovalStatus = status
        };
    }

    private static string ResolvePermission(string actionCode)
    {
        return actionCode switch
        {
            "Submit" => "demo-approval-order:submit",
            "Approve" => "workflow:task:approve",
            "Reject" => "workflow:task:reject",
            "Withdraw" => "demo-approval-order:withdraw",
            "Cancel" => "demo-approval-order:cancel",
            _ => actionCode
        };
    }

    private sealed class TestNumberGenerator : INumberGenerator
    {
        private readonly string _number;

        public TestNumberGenerator(string number)
        {
            _number = number;
        }

        public List<string> RuleCodes { get; } = [];

        public Task<string> GenerateAsync(string ruleCode, CancellationToken cancellationToken = default)
        {
            RuleCodes.Add(ruleCode);
            return Task.FromResult(_number);
        }

        public Task<string> GenerateAsync(
            string ruleCode,
            Dictionary<string, object> variables,
            CancellationToken cancellationToken = default)
        {
            RuleCodes.Add(ruleCode);
            return Task.FromResult(_number);
        }
    }

    private sealed class TestStateTransitionExecutor : IStateTransitionExecutor
    {
        private readonly Action<string, string>? _onExecute;

        public TestStateTransitionExecutor(Action<string, string>? onExecute = null)
        {
            _onExecute = onExecute;
        }

        public List<(string BusinessId, string CurrentState, string ActionCode)> Validations { get; } = [];

        public List<(string BusinessId, string ActionCode)> Executions { get; } = [];

        public Task<IReadOnlyList<AvailableStateActionResponse>> GetAvailableActionsAsync(
            string businessType,
            string businessId,
            string currentState,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AvailableStateActionResponse>>([]);
        }

        public Task<StateTransitionExecutionResponse> ExecuteTransitionAsync(
            string businessType,
            string businessId,
            string actionCode,
            string? comment = null,
            CancellationToken cancellationToken = default)
        {
            Executions.Add((businessId, actionCode));
            _onExecute?.Invoke(businessId, actionCode);
            return Task.FromResult(new StateTransitionExecutionResponse
            {
                BusinessType = businessType,
                BusinessId = businessId,
                ActionCode = actionCode
            });
        }

        public Task ValidateTransitionAsync(
            string businessType,
            string businessId,
            string currentState,
            string actionCode,
            CancellationToken cancellationToken = default)
        {
            Validations.Add((businessId, currentState, actionCode));
            return Task.CompletedTask;
        }
    }

    private sealed class TestWorkflowEngine : IWorkflowEngine
    {
        public StartWorkflowInstanceRequest? StartRequest { get; private set; }

        public Task<WorkflowInstanceDetailResponse> StartAsync(
            StartWorkflowInstanceRequest request,
            CancellationToken cancellationToken = default)
        {
            StartRequest = request;
            return Task.FromResult(new WorkflowInstanceDetailResponse
            {
                Id = Guid.NewGuid(),
                BusinessType = request.BusinessType,
                BusinessId = request.BusinessId,
                BusinessTitle = request.BusinessTitle
            });
        }

        public Task ApproveAsync(Guid taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RejectAsync(Guid taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task WithdrawAsync(Guid instanceId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TransferAsync(Guid taskId, TransferWorkflowTaskRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddSignAsync(Guid taskId, AddSignWorkflowTaskRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
        where TEntity : BaseEntity
    {
        private readonly List<TEntity> _items;

        public InMemoryRepository(params TEntity[] items)
        {
            _items = items.ToList();
        }

        public IReadOnlyList<TEntity> Items => _items;

        public IQueryable<TEntity> Query(bool ignoreQueryFilters = false)
        {
            return _items.Where(entity => !entity.IsDeleted).ToList().AsQueryable();
        }

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(entity => entity.Id == id && !entity.IsDeleted));
        }

        public Task<IReadOnlyList<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TEntity>>(
                _items.Where(entity => !entity.IsDeleted).AsQueryable().Where(predicate).ToList());
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            entity.TenantId = entity.TenantId == Guid.Empty ? TenantId : entity.TenantId;
            entity.CreatedAt = DateTimeOffset.UtcNow;
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity)
        {
            entity.IsDeleted = true;
        }
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            return action(cancellationToken);
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        private readonly IReadOnlyCollection<string> _permissions;

        public TestCurrentUserService(IReadOnlyCollection<string> permissions)
        {
            _permissions = permissions;
        }

        public bool IsAuthenticated => true;

        public Guid? UserId => DemoApprovalOrderPlatformIntegrationTests.UserId;

        public Guid? TenantId => DemoApprovalOrderPlatformIntegrationTests.TenantId;

        public Guid? DepartmentId => null;

        public string? SessionId => "test";

        public string? Username => "tester";

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlyCollection<string> PermissionCodes => _permissions;

        public bool IsSuperAdmin => false;

        public bool IsCurrentUserSuperAdmin()
        {
            return false;
        }

        public bool IsCurrentUserAdmin()
        {
            return false;
        }

        public bool CanManageBuiltinResources()
        {
            return false;
        }

        public bool HasPermission(string permissionCode)
        {
            return _permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
        }
    }
}
