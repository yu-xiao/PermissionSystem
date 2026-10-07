using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

internal sealed class TestBusinessActionHandler(AiBusinessActionDefinition definition) : IAiBusinessActionHandler
{
    public AiBusinessActionDefinition Definition { get; } = definition;

    public int PreparationCount { get; private set; }

    public Task<AiActionToolExecutionResult> PrepareDraftAsync(
        AiActionDraftContext context, string argumentsJson, CancellationToken cancellationToken = default)
    {
        PreparationCount++;
        return Task.FromResult(new AiActionToolExecutionResult { ContentJson = "{}" });
    }
}

internal static class Aic009ActionTestSupport
{
    public static AiBusinessActionAccessPolicy Policy(ICurrentUserService user, IAiCenterConfiguration configuration)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TestIds.TenantId, "synthetic-test");
        return new AiBusinessActionAccessPolicy(user, tenant, configuration);
    }
}
