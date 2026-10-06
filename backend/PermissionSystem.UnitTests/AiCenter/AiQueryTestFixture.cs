using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

internal sealed class AiQueryTestFixture
{
    public static readonly string[] Permissions = [AiCenterConstants.ChatUsePermission, AiCenterConstants.ConversationViewPermission,
        AiCenterConstants.ToolQueryPermission, AiCenterConstants.UserQueryPermission, "system:user:view",
        AiCenterConstants.LoginLogQueryPermission, "system:login-log:view", AiCenterConstants.OperationLogQueryPermission, "system:operation-log:view"];
    public TestCurrentUserService Current { get; } = new(permissions: Permissions);
    public TenantContext Tenant { get; } = new();
    public IdentitySource Identities { get; } = new();
    public ScopeSource Scopes { get; } = new();
    public TestConfiguration Configuration { get; } = new();
    public InMemoryRepository<User> Users { get; } = new();
    public InMemoryRepository<Department> Departments { get; } = new();
    public InMemoryRepository<Menu> Menus { get; } = new();
    public InMemoryRepository<AiMessage> Messages { get; } = new();
    public InMemoryRepository<AiRun> Runs { get; } = new();
    public InMemoryRepository<AiToolInvocation> Invocations { get; } = new();
    public AiConversation Conversation { get; } = new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, UserId = TestIds.NormalUserId };
    public InMemoryRepository<AiConversation> Conversations { get; }
    public InMemoryAsyncQueryExecutor Queries { get; } = new();
    public DiagnosticSource Diagnostics { get; } = new();
    public AiQueryAccessGuard Guard { get; }
    public AiStructuredResultReader Reader { get; }
    public AiFollowUpContextService FollowUp { get; }
    public AiToolExecutionContext ToolContext => new() { TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId };

    public AiQueryTestFixture()
    {
        Tenant.SetTenant(TestIds.TenantId, "test");
        Conversations = new(Conversation);
        Guard = new(Current, Tenant, Identities, Configuration, Scopes);
        Reader = new(Messages, Runs, Conversations, Invocations, Users, Departments, Current, Tenant, Queries,
            Diagnostics, Guard, new DataPermissionFilter(), Configuration, Menus);
        FollowUp = new(Reader, Guard);
    }

    public UserSearchAiToolHandler UserHandler() => new(Scopes, new DataPermissionFilter(), Users, Queries,
        accessGuard: Guard, departments: Departments);

    public async Task<AiMessage> PersistAsync(AiToolExecutionResult output)
    {
        var run = new AiRun { TenantId = TestIds.TenantId, ConversationId = Conversation.Id, ActorUserId = TestIds.NormalUserId };
        await Runs.AddAsync(run);
        var result = output.StructuredResult!;
        result.RunId = run.Id;
        result.InvocationId = Guid.NewGuid().ToString("N");
        var content = AiStructuredResults.SerializeBounded(result);
        await Invocations.AddAsync(new AiToolInvocation
        {
            RunId = run.Id, InvocationId = result.InvocationId, TenantId = TestIds.TenantId,
            ToolCode = result.ToolCode, ToolVersion = result.ToolVersion, Status = AiInvocationStatus.Completed,
            InputDigest = result.Citation.QueryParametersDigest, OutputDigest = AiStructuredResults.Digest(content)
        });
        var message = new AiMessage { TenantId = TestIds.TenantId, ConversationId = Conversation.Id, Role = AiMessageRole.Tool,
            Content = content, ContentDigest = AiStructuredResults.Digest(content), Sequence = Messages.Items.Count + 1 };
        await Messages.AddAsync(message);
        return message;
    }

    public AiContextReference Reference => new(Runs.Items.Last().Id, Invocations.Items.Last().InvocationId);

    public sealed class ScopeSource : IDataScopeResolver, IDataScopeService
    {
        public DataScopeContext Scope { get; set; } = new() { ScopeType = DataScopeType.All, CurrentUserId = TestIds.NormalUserId };
        public Task<DataScopeContext> ResolveAsync(PermissionSubject subject, CancellationToken cancellationToken = default) => Task.FromResult(Scope);
        public Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default) => Task.FromResult(Scope);
        public Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetRoleDataScopeAsync(Guid roleId, SetRoleDataScopeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public sealed class IdentitySource : IUserCredentialValidator
    {
        public bool Active { get; set; } = true;
        public AuthenticatedUser Actor { get; set; } = new(TestIds.NormalUserId, "actor", TestIds.TenantId, null,
            Guid.NewGuid(), [], Permissions);
        public Task<AuthenticatedUser?> GetAuthenticationStateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthenticatedUser?>(Active ? Actor : null);
        public Task<Guid?> ResolveActiveTenantIdAsync(string tenantCodeOrId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public sealed class TestConfiguration : IAiCenterConfiguration
    {
        public bool Enabled { get; set; } = true;
        public IReadOnlyCollection<Guid> AllowedTenantIds { get; set; } = [TestIds.TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
    }

    public sealed class DiagnosticSource : IPermissionDiagnosticService
    {
        public bool Allow { get; set; } = true;
        public Task<PermissionDiagnosticResponse> DiagnoseAsync(PermissionDiagnosticRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Reading history must not perform a new query.");
        public Task<bool> CanReadAsync(PermissionDiagnosticResponse result, CancellationToken cancellationToken = default) => Task.FromResult(Allow);
    }
}
