using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiModelRouteServiceTests
{
    [Fact]
    public async Task ResolveAsync_WithNoPolicyUsesEligibleDefaultProvider()
    {
        var provider = Provider("default", isDefault: true);
        var service = CreateService([], [provider]);

        var candidates = await service.ResolveAsync("permission-platform-agent", Guid.NewGuid());

        var candidate = Assert.Single(candidates);
        Assert.Equal(provider.Id, candidate.Provider.Id);
        Assert.Equal(AiModelRouteRole.Primary, candidate.Role);
    }

    [Fact]
    public async Task ResolveAsync_WithFullCanaryAndFallbackReturnsStableOrder()
    {
        var primary = Provider("primary");
        var canary = Provider("canary");
        var fallback = Provider("fallback");
        var policy = new AiModelRoutePolicy
        {
            TenantId = TestIds.TenantId,
            AgentCode = "permission-platform-agent",
            PrimaryProviderConfigId = primary.Id,
            CanaryProviderConfigId = canary.Id,
            CanaryPercentage = 100,
            FallbackProviderConfigId = fallback.Id,
            IsEnabled = true
        };
        var service = CreateService([policy], [primary, canary, fallback]);

        var candidates = await service.ResolveAsync("permission-platform-agent", Guid.NewGuid());

        Assert.Collection(
            candidates,
            item =>
            {
                Assert.Equal(canary.Id, item.Provider.Id);
                Assert.Equal(AiModelRouteRole.Canary, item.Role);
            },
            item =>
            {
                Assert.Equal(fallback.Id, item.Provider.Id);
                Assert.Equal(AiModelRouteRole.Fallback, item.Role);
            });
    }

    [Fact]
    public async Task SavePolicyAsync_WithResidencyMismatchIsRejected()
    {
        var primary = Provider("primary");
        var fallback = Provider("fallback");
        fallback.DataResidency = "EU";
        var service = CreateService([], [primary, fallback]);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SavePolicyAsync(
            new SaveAiModelRoutePolicyRequest
            {
                AgentCode = "permission-platform-agent",
                PrimaryProviderConfigId = primary.Id,
                FallbackProviderConfigId = fallback.Id
            }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_WhenFallbackIsDisabledKeepsEligiblePrimary()
    {
        var primary = Provider("primary");
        var fallback = Provider("fallback");
        fallback.IsEnabled = false;
        var policy = new AiModelRoutePolicy
        {
            TenantId = TestIds.TenantId,
            AgentCode = "permission-platform-agent",
            PrimaryProviderConfigId = primary.Id,
            FallbackProviderConfigId = fallback.Id,
            IsEnabled = true
        };
        var service = CreateService([policy], [primary, fallback]);

        var candidate = Assert.Single(await service.ResolveAsync(
            "permission-platform-agent",
            Guid.NewGuid()));

        Assert.Equal(primary.Id, candidate.Provider.Id);
    }

    private static AiModelRouteService CreateService(
        AiModelRoutePolicy[] policies,
        AiProviderConfig[] providers)
    {
        return new AiModelRouteService(
            new InMemoryRepository<AiModelRoutePolicy>(policies),
            new InMemoryRepository<AiProviderConfig>(providers),
            new InMemoryAsyncQueryExecutor(),
            new TestTenantWriteResolver(),
            new TestUnitOfWork());
    }

    private static AiProviderConfig Provider(string code, bool isDefault = false)
    {
        return new AiProviderConfig
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ProviderCode = code,
            ProviderName = code,
            ModelName = code,
            IsDefault = isDefault,
            IsEnabled = true,
            SupportsTools = true,
            DataResidency = "CN",
            ComplianceConfirmedAt = DateTimeOffset.UtcNow
        };
    }
}
