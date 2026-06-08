using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Tests;

public sealed class StateTransitionExecutorTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ExecuteTransitionAsync_ShouldSucceed_WhenTransitionIsValid()
    {
        var fixture = CreateFixture(["demo:submit"]);

        var result = await fixture.Executor.ExecuteTransitionAsync("Demo", "B001", "Submit", "submit");

        Assert.Equal("Draft", result.FromState);
        Assert.Equal("Pending", result.ToState);
        Assert.Equal("Pending", fixture.Handler.CurrentState);
    }

    [Fact]
    public async Task ExecuteTransitionAsync_ShouldFail_WhenTransitionIsInvalid()
    {
        var fixture = CreateFixture(["demo:submit"]);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Executor.ExecuteTransitionAsync("Demo", "B001", "Approve", "approve"));

        Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
    }

    [Fact]
    public async Task ExecuteTransitionAsync_ShouldFail_WhenPermissionIsMissing()
    {
        var fixture = CreateFixture([]);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Executor.ExecuteTransitionAsync("Demo", "B001", "Submit", "submit"));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task ExecuteTransitionAsync_ShouldWriteTransitionLog()
    {
        var fixture = CreateFixture(["demo:submit"]);

        await fixture.Executor.ExecuteTransitionAsync("Demo", "B001", "Submit", "submit");

        var log = Assert.Single(fixture.LogRepository.Items);
        Assert.Equal("Demo", log.BusinessType);
        Assert.Equal("B001", log.BusinessId);
        Assert.Equal("Submit", log.ActionCode);
        Assert.Equal("Draft", log.FromState);
        Assert.Equal("Pending", log.ToState);
    }

    private static TestFixture CreateFixture(IReadOnlyCollection<string> permissions)
    {
        var machine = new StateMachineDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BusinessType = "Demo",
            Name = "Demo",
            IsEnabled = true
        };

        var transition = new StateTransition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            MachineId = machine.Id,
            FromState = "Draft",
            ToState = "Pending",
            ActionCode = "Submit",
            ActionName = "Submit",
            RequiredPermission = "demo:submit",
            IsEnabled = true,
            Sort = 1
        };

        var logRepository = new InMemoryRepository<StateTransitionLog>();
        var handler = new TestStateTransitionHandler();
        var executor = new StateTransitionExecutor(
            new InMemoryRepository<StateMachineDefinition>(machine),
            new InMemoryRepository<StateTransition>(transition),
            logRepository,
            new StateTransitionHandlerResolver([handler]),
            new TestCurrentUserService(permissions),
            new TestUnitOfWork());

        return new TestFixture(executor, handler, logRepository);
    }

    private sealed record TestFixture(
        StateTransitionExecutor Executor,
        TestStateTransitionHandler Handler,
        InMemoryRepository<StateTransitionLog> LogRepository);

    private sealed class TestStateTransitionHandler : IStateTransitionHandler
    {
        public string BusinessType => "Demo";

        public string CurrentState { get; private set; } = "Draft";

        public Task<string> GetCurrentStateAsync(string businessId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentState);
        }

        public Task ValidateTransitionAsync(StateTransitionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OnTransitionAsync(StateTransitionContext context, CancellationToken cancellationToken = default)
        {
            CurrentState = context.ToState;
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

        public Guid? UserId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");

        public Guid? TenantId => StateTransitionExecutorTests.TenantId;

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
