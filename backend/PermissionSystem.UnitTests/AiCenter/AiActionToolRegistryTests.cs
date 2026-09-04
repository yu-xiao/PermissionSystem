using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiActionToolRegistryTests
{
    [Fact]
    public void GetAvailableTools_RequiresAiDraftAndOriginalCreatePermissions()
    {
        var registry = CreateRegistry([AiCenterConstants.DocumentDraftPermission]);

        Assert.Empty(registry.GetAvailableTools());
    }

    [Fact]
    public void GetAvailableTools_WithBothPermissionsReturnsOnlyRegisteredAction()
    {
        var registry = CreateRegistry(
        [
            AiCenterConstants.DocumentDraftPermission,
            "demo-business-order:create"
        ]);

        var tool = Assert.Single(registry.GetAvailableTools());
        Assert.Equal(AiBusinessActionConstants.DemoBusinessOrderToolCode, tool.ToolCode);
    }

    private static AiActionToolRegistry CreateRegistry(IReadOnlyCollection<string> permissions)
    {
        return new AiActionToolRegistry(
            [new TestHandler()],
            new TestCurrentUserService(permissions: permissions),
            new TestConfiguration());
    }

    private sealed class TestHandler : IAiBusinessActionHandler
    {
        public string BusinessType => "DemoBusinessOrder";

        public string HandlerVersion => "1.0";

        public AiToolDefinition ToolDefinition { get; } = new()
        {
            ToolCode = AiBusinessActionConstants.DemoBusinessOrderToolCode,
            FunctionName = AiBusinessActionConstants.DemoBusinessOrderFunctionName,
            Version = "1.0"
        };

        public Task<AiActionToolExecutionResult> PrepareDraftAsync(
            AiActionDraftContext context,
            string argumentsJson,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestConfiguration : IAiCenterConfiguration
    {
        public bool Enabled => true;

        public IReadOnlyCollection<Guid> AllowedTenantIds => [TestIds.TenantId];

        public int ConversationRetentionDays => 30;

        public int AuditRetentionDays => 180;
    }
}
