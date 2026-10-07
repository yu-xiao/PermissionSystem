using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic009ActionAuthorizationTests
{
    private const string OtherPermission = "synthetic-order:create";

    [Theory]
    [InlineData(false, false, false, 0)]
    [InlineData(false, true, true, 0)]
    [InlineData(true, false, false, 0)]
    [InlineData(true, true, false, 1)]
    [InlineData(true, false, true, 1)]
    [InlineData(true, true, true, 2)]
    public void Discovery_UsesEachHandlersBusinessPermission(bool draft, bool demo, bool other, int count)
    {
        var permissions = new List<string>();
        if (draft) permissions.Add(AiCenterConstants.DocumentDraftPermission);
        if (demo) permissions.Add("demo-business-order:create");
        if (other) permissions.Add(OtherPermission);
        var (registry, _, _, _) = CreateRegistry(permissions);

        var tools = registry.GetAvailableTools();
        Assert.Equal(count, tools.Count);
        Assert.Equal(draft && demo, tools.Any(tool => tool.ToolCode == AiBusinessActionConstants.DemoBusinessOrderToolCode));
        Assert.Equal(draft && other, tools.Any(tool => tool.ToolCode == "synthetic.prepare"));
    }

    [Fact]
    public async Task Execute_OtherActionDoesNotRequireDemoPermission()
    {
        var (registry, _, _, other) = CreateRegistry([AiCenterConstants.DocumentDraftPermission, OtherPermission]);
        await registry.ExecuteAsync("synthetic.prepare", Context(), "{}");
        Assert.Equal(1, other.PreparationCount);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => registry.ExecuteAsync(
            AiBusinessActionConstants.DemoBusinessOrderToolCode, Context(), "{}"));
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task Execute_RevocationAfterDiscoveryRejectsBeforeHandler()
    {
        var (registry, user, _, other) = CreateRegistry([AiCenterConstants.DocumentDraftPermission, OtherPermission]);
        Assert.Single(registry.GetAvailableTools());
        ((ICollection<string>)user.PermissionCodes).Remove(OtherPermission);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => registry.ExecuteAsync("synthetic.prepare", Context(), "{}"));
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Empty(registry.GetAvailableTools());
        Assert.Equal(0, other.PreparationCount);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("tenant")]
    [InlineData("run")]
    [InlineData("conversation")]
    [InlineData("invocation")]
    public async Task Execute_RejectsForgedOrMissingContext(string field)
    {
        var (registry, _, _, other) = CreateRegistry([AiCenterConstants.DocumentDraftPermission, OtherPermission]);
        var valid = Context();
        var context = new AiActionDraftContext
        {
            ActorUserId = field == "user" ? Guid.NewGuid() : valid.ActorUserId,
            TenantId = field == "tenant" ? Guid.NewGuid() : valid.TenantId,
            RunId = field == "run" ? Guid.Empty : valid.RunId,
            ConversationId = field == "conversation" ? Guid.Empty : valid.ConversationId,
            InvocationId = field == "invocation" ? new string('x', 129) : valid.InvocationId
        };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => registry.ExecuteAsync("synthetic.prepare", context, "{}"));
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, other.PreparationCount);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("missing-tenant")]
    [InlineData("empty-user")]
    [InlineData("wrong-tenant")]
    [InlineData("disabled")]
    [InlineData("unapproved-tenant")]
    public void Discovery_RejectsInvalidIdentityOrConfiguration(string condition)
    {
        var (registry, user, configuration, _) = CreateRegistry([AiCenterConstants.DocumentDraftPermission, OtherPermission]);
        switch (condition)
        {
            case "anonymous": user.UserId = null; break;
            case "missing-tenant": user.TenantId = null; break;
            case "empty-user": user.UserId = Guid.Empty; break;
            case "wrong-tenant": user.TenantId = Guid.NewGuid(); break;
            case "disabled": configuration.Enabled = false; break;
            case "unapproved-tenant": configuration.AllowedTenantIds = []; break;
        }
        Assert.Empty(registry.GetAvailableTools());
    }

    [Fact]
    public void ExecutionCapability_RequiresDeclaredStagePermissionAndSupportedHandler()
    {
        var user = new TestCurrentUserService(permissions: [AiCenterConstants.DocumentDraftPermission,
            OtherPermission, AiCenterConstants.DocumentExecutePermission]);
        var policy = Aic009ActionTestSupport.Policy(user, new TestConfiguration());
        var supported = Definition(executionPermissions: [AiCenterConstants.DocumentExecutePermission, "synthetic-order:approve"]);
        Assert.True(policy.CanPrepare(supported));
        Assert.False(policy.CanExecute(supported));
        ((ICollection<string>)user.PermissionCodes).Add("synthetic-order:approve");
        Assert.True(policy.CanExecute(supported));
        Assert.False(policy.CanExecute(Definition(supportsExecution: false)));
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("function")]
    [InlineData("business-version")]
    public void Registration_RejectsDuplicateIdentity(string duplicate)
    {
        var policy = Aic009ActionTestSupport.Policy(new TestCurrentUserService(), new TestConfiguration());
        var first = Definition();
        var second = Definition(toolCode: duplicate == "tool" ? "synthetic.prepare" : "synthetic.other",
            functionName: duplicate == "function" ? "prepare_synthetic" : "prepare_other",
            businessType: duplicate == "business-version" ? "SyntheticOrder" : "OtherOrder");
        Assert.Throws<InvalidOperationException>(() => new AiActionToolRegistry(
            [new TestBusinessActionHandler(first), new TestBusinessActionHandler(second)], policy));
    }

    [Theory]
    [InlineData("no-business-permission")]
    [InlineData("empty-permissions")]
    [InlineData("missing-draft-permission")]
    [InlineData("blank-permission")]
    [InlineData("missing-execute-permission")]
    [InlineData("unsupported-execute-permission")]
    [InlineData("business-type")]
    [InlineData("version")]
    [InlineData("function")]
    [InlineData("schema")]
    [InlineData("scope")]
    [InlineData("rows")]
    [InlineData("timeout")]
    public void Registration_RejectsIncompleteCapabilityDeclarations(string invalid)
    {
        string[] permissions = invalid switch
        {
            "no-business-permission" => [AiCenterConstants.DocumentDraftPermission],
            "empty-permissions" => [],
            "missing-draft-permission" => [OtherPermission],
            "blank-permission" => [AiCenterConstants.DocumentDraftPermission, " "],
            _ => [AiCenterConstants.DocumentDraftPermission, OtherPermission]
        };
        var definition = Definition(permissions: permissions,
            businessType: invalid == "business-type" ? "" : "SyntheticOrder",
            version: invalid == "version" ? "2.0" : "1.0",
            functionName: invalid == "function" ? "bad.name" : "prepare_synthetic",
            schema: invalid == "schema" ? "[]" : "{}",
            scope: invalid == "scope" ? AiToolDataScopePolicies.CurrentTenant : AiToolDataScopePolicies.ActorOwnedDraft,
            rows: invalid == "rows" ? 2 : 1, timeout: invalid == "timeout" ? 0 : 60,
            supportsExecution: invalid != "unsupported-execute-permission",
            executionPermissions: invalid == "missing-execute-permission" ? [] : [AiCenterConstants.DocumentExecutePermission]);
        var policy = Aic009ActionTestSupport.Policy(new TestCurrentUserService(), new TestConfiguration());
        Assert.Throws<InvalidOperationException>(() => new AiActionToolRegistry([new TestBusinessActionHandler(definition)], policy));
    }

    [Fact]
    public void FindDefinition_UsesExactBusinessTypeAndVersion()
    {
        var (registry, _, _, _) = CreateRegistry([]);
        Assert.NotNull(registry.FindDefinition("SyntheticOrder", "1.0"));
        Assert.Null(registry.FindDefinition("SyntheticOrder", "2.0"));
        Assert.Null(registry.FindDefinition("Unknown", "1.0"));
    }

    private static AiActionDraftContext Context() => new()
    {
        TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId,
        RunId = Guid.NewGuid(), ConversationId = Guid.NewGuid(), InvocationId = "synthetic-call"
    };

    internal static AiBusinessActionDefinition Definition(string toolCode = "synthetic.prepare", string functionName = "prepare_synthetic",
        string businessType = "SyntheticOrder", string version = "1.0", IReadOnlyCollection<string>? permissions = null,
        bool supportsExecution = true, IReadOnlyCollection<string>? executionPermissions = null,
        string schema = "{}", string scope = AiToolDataScopePolicies.ActorOwnedDraft, int rows = 1, int timeout = 60) => new()
    {
        BusinessType = businessType, HandlerVersion = "1.0", SupportsExecution = supportsExecution,
        RequiredExecutionPermissions = executionPermissions ?? (supportsExecution ? [AiCenterConstants.DocumentExecutePermission] : []),
        ToolDefinition = new()
        {
            ToolCode = toolCode, FunctionName = functionName, Version = version,
            Description = "Synthetic draft only", DataClassification = "Internal", DataScopePolicy = scope,
            RequiredPermissions = permissions ?? [AiCenterConstants.DocumentDraftPermission, OtherPermission],
            InputSchemaJson = schema, OutputSchemaJson = "{}", MaxRows = rows, TimeoutSeconds = timeout
        }
    };

    private static (AiActionToolRegistry Registry, TestCurrentUserService User, TestConfiguration Configuration, TestBusinessActionHandler Other)
        CreateRegistry(IReadOnlyCollection<string> permissions)
    {
        var user = new TestCurrentUserService(permissions: permissions);
        var configuration = new TestConfiguration();
        var other = new TestBusinessActionHandler(Definition());
        var registry = new AiActionToolRegistry(
            [new TestBusinessActionHandler(DemoBusinessOrderDraftHandler.ActionDefinition), other],
            Aic009ActionTestSupport.Policy(user, configuration));
        return (registry, user, configuration, other);
    }

    private sealed class TestConfiguration : IAiCenterConfiguration
    {
        public bool Enabled { get; set; } = true;
        public IReadOnlyCollection<Guid> AllowedTenantIds { get; set; } = [TestIds.TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
    }
}
