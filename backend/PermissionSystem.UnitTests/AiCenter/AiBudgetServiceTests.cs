using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiBudgetServiceTests
{
    [Fact]
    public async Task ReserveInvocationAsync_WhenHardLimitWouldBeExceededIsRejected()
    {
        var policy = new AiBudgetPolicy
        {
            TenantId = TestIds.TenantId,
            PolicyCode = "tenant-cny",
            PolicyName = "Tenant CNY",
            ScopeType = AiBudgetScopeType.Tenant,
            MonthlyLimit = 0.5m,
            Currency = "CNY",
            IsHardLimit = true,
            IsEnabled = true
        };
        var usages = new InMemoryRepository<AiUsageLog>();
        var service = CreateService([policy], usages);
        var provider = PricedProvider();
        var usage = NewUsage();

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.ReserveInvocationAsync(
            usage,
            provider,
            TestIds.NormalUserId,
            500_000,
            500_000));

        Assert.Equal(ErrorCode.TooManyRequests, exception.ErrorCode);
        Assert.Empty(usages.Items);
    }

    [Fact]
    public async Task SettleInvocationAsync_UsesPriceSnapshotAndClearsReservation()
    {
        var usages = new InMemoryRepository<AiUsageLog>();
        var service = CreateService([], usages);
        var usage = NewUsage();
        await service.ReserveInvocationAsync(
            usage,
            PricedProvider(),
            TestIds.NormalUserId,
            100,
            100);
        usage.InputTokens = 1000;
        usage.OutputTokens = 2000;
        usage.Status = AiInvocationStatus.Completed;

        await service.SettleInvocationAsync(usage);

        Assert.Equal(0.005m, usage.EstimatedCost);
        Assert.Null(usage.ReservedCost);
        Assert.Null(usage.ReservationExpiresAt);
    }

    [Fact]
    public async Task ReserveInvocationAsync_WithHardBudgetRejectsUnpricedProvider()
    {
        var policy = new AiBudgetPolicy
        {
            TenantId = TestIds.TenantId,
            PolicyCode = "tenant-cny",
            PolicyName = "Tenant CNY",
            ScopeType = AiBudgetScopeType.Tenant,
            MonthlyLimit = 100m,
            Currency = "CNY",
            IsHardLimit = true,
            IsEnabled = true
        };
        var usages = new InMemoryRepository<AiUsageLog>();
        var service = CreateService([policy], usages);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.ReserveInvocationAsync(
            NewUsage(),
            new AiProviderConfig { TenantId = TestIds.TenantId },
            TestIds.NormalUserId,
            100,
            100));

        Assert.Equal(ErrorCode.TooManyRequests, exception.ErrorCode);
        Assert.Empty(usages.Items);
    }

    private static AiBudgetService CreateService(
        AiBudgetPolicy[] policies,
        InMemoryRepository<AiUsageLog> usages)
    {
        return new AiBudgetService(
            new InMemoryRepository<AiBudgetPolicy>(policies),
            usages,
            new InMemoryRepository<AiRun>(),
            new InMemoryRepository<User>(),
            new InMemoryAsyncQueryExecutor(),
            new TestTenantWriteResolver(),
            new TestDistributedLock(),
            new TestUnitOfWork());
    }

    private static AiProviderConfig PricedProvider()
    {
        return new AiProviderConfig
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            InputTokenPricePerMillion = 1m,
            OutputTokenPricePerMillion = 2m,
            PricingCurrency = "CNY"
        };
    }

    private static AiUsageLog NewUsage()
    {
        return new AiUsageLog
        {
            TenantId = TestIds.TenantId,
            RunId = Guid.NewGuid(),
            ProviderConfigId = Guid.NewGuid(),
            ModelName = "test",
            Status = AiInvocationStatus.Running
        };
    }
}
