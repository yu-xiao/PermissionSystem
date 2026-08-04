using System.Collections.Concurrent;
using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.NumberRules;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Tests;

public sealed class NumberGeneratorTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public void BuildSequenceKey_ShouldUseDailyPeriod()
    {
        var rule = CreateRule(NumberRuleResetCycle.Daily);
        var key = NumberGenerator.BuildSequenceKey(rule, new DateTimeOffset(2026, 5, 26, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal("PurchaseOrder:20260526", key);
    }

    [Fact]
    public void BuildSequenceKey_ShouldUseMonthlyPeriod()
    {
        var rule = CreateRule(NumberRuleResetCycle.Monthly);
        var key = NumberGenerator.BuildSequenceKey(rule, new DateTimeOffset(2026, 5, 26, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal("PurchaseOrder:202605", key);
    }

    [Fact]
    public async Task GenerateAsync_ShouldPadSequence()
    {
        var generator = CreateGenerator(CreateRule(NumberRuleResetCycle.Daily, sequenceLength: 4));

        var number = await generator.GenerateAsync("PurchaseOrder");

        Assert.EndsWith("0001", number);
        Assert.StartsWith("PO", number);
    }

    [Fact]
    public async Task GenerateAsync_ShouldNotDuplicateWhenConcurrent()
    {
        var generator = CreateGenerator(CreateRule(NumberRuleResetCycle.Daily, sequenceLength: 4));
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => generator.GenerateAsync("PurchaseOrder"))
            .ToArray();

        var numbers = await Task.WhenAll(tasks);

        Assert.Equal(50, numbers.Distinct(StringComparer.Ordinal).Count());
    }

    private static NumberGenerator CreateGenerator(NumberRule rule)
    {
        var ruleRepository = new InMemoryRepository<NumberRule>(rule);
        var segmentRepository = new InMemoryRepository<NumberRuleSegment>();
        var sequenceRepository = new InMemoryRepository<NumberSequence>();

        return new NumberGenerator(
            ruleRepository,
            segmentRepository,
            sequenceRepository,
            new TestDistributedLock(),
            new TestUnitOfWork(),
            new TestTenantContext(TenantId));
    }

    private static NumberRule CreateRule(NumberRuleResetCycle resetCycle, int sequenceLength = 4)
    {
        return new NumberRule
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RuleCode = "PurchaseOrder",
            RuleName = "Purchase Order",
            BusinessType = "PurchaseOrder",
            Prefix = "PO",
            DateFormat = "yyyyMMdd",
            SequenceLength = sequenceLength,
            ResetCycle = resetCycle,
            Separator = string.Empty,
            IsEnabled = true
        };
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
        where TEntity : BaseEntity
    {
        private readonly List<TEntity> _items;

        public InMemoryRepository(params TEntity[] items)
        {
            _items = items.ToList();
        }

        public IQueryable<TEntity> Query()
        {
            lock (_items)
            {
                return _items.Where(entity => !entity.IsDeleted).ToList().AsQueryable();
            }
        }

        public IQueryable<TEntity> QueryForTenant(Guid tenantId)
        {
            lock (_items)
            {
                return _items.Where(entity => !entity.IsDeleted && entity.TenantId == tenantId).ToList().AsQueryable();
            }
        }

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                return Task.FromResult(_items.FirstOrDefault(entity => entity.Id == id && !entity.IsDeleted));
            }
        }

        public Task<IReadOnlyList<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                return Task.FromResult<IReadOnlyList<TEntity>>(
                    _items.Where(entity => !entity.IsDeleted).AsQueryable().Where(predicate).ToList());
            }
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                if (entity.Id == Guid.Empty)
                {
                    entity.Id = Guid.NewGuid();
                }

                _items.Add(entity);
            }

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

    private sealed class TestDistributedLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public async Task<DistributedLockHandle?> TryAcquireAsync(
            string key,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            return await semaphore.WaitAsync(0, cancellationToken)
                ? new DistributedLockHandle(key, Guid.NewGuid().ToString("N"), expiry ?? TimeSpan.FromSeconds(30))
                : null;
        }

        public async Task<DistributedLockHandle> AcquireAsync(
            string key,
            TimeSpan? expiry = null,
            TimeSpan? waitTime = null,
            CancellationToken cancellationToken = default)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            return new DistributedLockHandle(key, Guid.NewGuid().ToString("N"), expiry ?? TimeSpan.FromSeconds(30));
        }

        public Task<bool> ReleaseAsync(DistributedLockHandle handle, CancellationToken cancellationToken = default)
        {
            if (_locks.TryGetValue(handle.Key, out var semaphore))
            {
                semaphore.Release();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task ExecuteWithLockAsync(
            string key,
            Func<CancellationToken, Task> action,
            TimeSpan? expiry = null,
            TimeSpan? waitTime = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteWithLockAsync<object?>(
                key,
                async token =>
                {
                    await action(token);
                    return null;
                },
                expiry,
                waitTime,
                cancellationToken);
        }

        public async Task<TResult> ExecuteWithLockAsync<TResult>(
            string key,
            Func<CancellationToken, Task<TResult>> action,
            TimeSpan? expiry = null,
            TimeSpan? waitTime = null,
            CancellationToken cancellationToken = default)
        {
            var handle = await AcquireAsync(key, expiry, waitTime, cancellationToken);
            try
            {
                return await action(cancellationToken);
            }
            finally
            {
                await ReleaseAsync(handle, CancellationToken.None);
            }
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; private set; }

        public string? Source { get; private set; } = "test";

        public bool IsResolved => TenantId.HasValue;

        public bool IsSuperAdmin { get; private set; }

        public bool IsSystemScopeActive { get; private set; }

        public bool IsHttpRequest { get; private set; }

        public void SetTenant(Guid tenantId, string source)
        {
            TenantId = tenantId;
            Source = source;
        }

        public void MarkAsSuperAdmin(bool isSuperAdmin)
        {
            IsSuperAdmin = isSuperAdmin;
        }

        public void MarkAsHttpRequest()
        {
            IsHttpRequest = true;
        }
    }
}
