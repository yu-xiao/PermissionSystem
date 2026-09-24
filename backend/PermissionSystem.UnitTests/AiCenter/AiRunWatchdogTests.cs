using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiRunWatchdogTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReclaimAsync_KeepsUnknownUsageAndSettlesRunTogether(bool priced)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ISystemTenantScope, SystemTenantScope>();
        services.AddScoped<IDistributedLock, TestDistributedLock>();
        services.AddScoped<IAuditContext, EmptyAuditContext>();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var runId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ISystemTenantScope>().Begin("test-seed");
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiRuns.Add(new AiRun
            {
                Id = runId, TenantId = TestIds.TenantId, Status = AiRunStatus.Running,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10), DeadlineAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
            db.AiUsageLogs.Add(new AiUsageLog
            {
                TenantId = TestIds.TenantId, RunId = runId, ModelName = "test", Status = AiInvocationStatus.Running,
                EstimatedInputTokens = 1000, EstimatedOutputTokens = 2000,
                ReservedCost = priced ? 0.005m : null,
                InputTokenPricePerMillion = priced ? 1m : null, OutputTokenPricePerMillion = priced ? 2m : null,
                PricingCurrency = priced ? "CNY" : null, ReservationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }
        var watchdog = new AiRunWatchdogHostedService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AiCenterOptions()), NullLogger<AiRunWatchdogHostedService>.Instance);
        await watchdog.ReclaimAsync(CancellationToken.None);
        await watchdog.ReclaimAsync(CancellationToken.None);

        await using var verification = provider.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(AiRunStatus.Failed, (await context.AiRuns.IgnoreQueryFilters().SingleAsync()).Status);
        var usage = await context.AiUsageLogs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(AiInvocationStatus.Failed, usage.Status);
        Assert.Equal(priced ? 0.005m : (decimal?)null, usage.EstimatedCost);
        Assert.Null(usage.ReservedCost);
        Assert.Null(usage.InputTokens);
        Assert.Equal(1000, usage.EstimatedInputTokens);
    }

    private sealed class EmptyAuditContext : IAuditContext
    {
        public Guid? UserId => null;
    }
}
