using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class PermissionDiagnosticTests
{
    [Theory]
    [InlineData("system:user:view", "system:user:view", true)]
    [InlineData("SYSTEM:USER:VIEW", "system:user:view", true)]
    [InlineData("*", "system:user:delete", true)]
    [InlineData("system:user:view", "system:user:delete|system:user:view", true)]
    [InlineData("system:user:view", "system:user:delete, system:user:view", true)]
    [InlineData("system:user:view", "system:user:delete", false)]
    public async Task SelfPermission_ShouldMatchActualAuthorizationHandler(string granted, string requested, bool expected)
    {
        await using var fixture = await Fixture.CreateAsync(extraPermissions: [granted]);
        var requirement = new PermissionRequirement(requested);
        var context = new AuthorizationHandlerContext([requirement], fixture.Http.HttpContext!.User, null);
        await new PermissionAuthorizationHandler(fixture.Current).HandleAsync(context);
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest(requested));
        Assert.Equal(expected, context.HasSucceeded);
        Assert.Equal(expected ? PermissionDiagnosticConclusion.Allowed : PermissionDiagnosticConclusion.Denied, result.Conclusion);
        Assert.Equal("CurrentServerIdentity", result.EvaluationBasis);
        if (granted == "*") Assert.Contains(result.Checks, check => check.Source == "RolePermission");
        else Assert.DoesNotContain(result.Checks, check => check.Source == "RolePermission");
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SessionId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuperAdmin_ShouldUseExistingExceptionAndAllDataScope()
    {
        await using var fixture = await Fixture.CreateAsync(superAdmin: true);
        var permission = await fixture.Service.DiagnoseAsync(PermissionRequest("not:configured"));
        var scope = await fixture.Service.DiagnoseAsync(new() { Kind = PermissionDiagnosticKind.DataScope });
        Assert.Equal(PermissionDiagnosticConclusion.Allowed, permission.Conclusion);
        Assert.Equal(PermissionDiagnosticConclusion.Allowed, scope.Conclusion);
        Assert.Contains(scope.Checks, check => check.Description.Contains("SuperAdmin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FrontendMismatch_ShouldRemainExplicitAndNotChangeBackendVerdict()
    {
        await using var fixture = await Fixture.CreateAsync(extraPermissions: ["*"]);
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest("system:user:view"));
        Assert.Equal(PermissionDiagnosticConclusion.Allowed, result.Conclusion);
        Assert.Contains(result.Checks, check => check.Code == "frontend.permission" && check.Status == PermissionDiagnosticCheckStatus.Failed);
    }

    [Theory]
    [InlineData("Menu")]
    [InlineData("Permission")]
    [InlineData("DataScope")]
    public async Task OtherUser_ShouldRequireSpecificPermissionsBeforeReturningEvidence(string kind)
    {
        await using var fixture = await Fixture.CreateAsync();
        var target = fixture.Add(new User());
        await fixture.SaveAsync();
        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(
            RequestFor(Enum.Parse<PermissionDiagnosticKind>(kind), target.Id)));
        Assert.Equal(ErrorCode.Forbidden, error.ErrorCode);
        Assert.Equal("The diagnostic target is not available.", error.Message);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cross-tenant")]
    [InlineData("deleted")]
    [InlineData("out-of-scope")]
    public async Task UnavailableTargets_ShouldReturnSameNonEnumeratingFailure(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var target = fixture.Add(new User { TenantId = scenario == "cross-tenant" ? Guid.NewGuid() : TestIds.TenantId });
        await fixture.SaveAsync();
        if (scenario == "deleted") { target.IsDeleted = true; await fixture.SaveAsync(); }
        if (scenario == "out-of-scope")
        {
            fixture.Add(new UserDataScope { UserId = fixture.Actor.Id, ScopeType = DataScopeType.CurrentUser });
            await fixture.SaveAsync();
        }
        var request = PermissionRequest("system:user:view", scenario == "missing" ? Guid.NewGuid() : target.Id);
        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(request));
        Assert.Equal(ErrorCode.Forbidden, error.ErrorCode);
        Assert.Equal("The diagnostic target is not available.", error.Message);
    }

    [Fact]
    public async Task OtherPermission_ShouldUseCurrentAuthenticationConfigAndIgnoreDisabledRoles()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var target = fixture.Add(new User());
        var role = fixture.Add(new Role { Code = "TargetRole", IsEnabled = false });
        var permission = fixture.Add(new Permission { Code = "business:approve" });
        fixture.Add(new UserRole { UserId = target.Id, RoleId = role.Id });
        fixture.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await fixture.SaveAsync();
        var denied = await fixture.Service.DiagnoseAsync(PermissionRequest(permission.Code, target.Id));
        Assert.Equal(PermissionDiagnosticConclusion.Denied, denied.Conclusion);
        role.IsEnabled = true;
        await fixture.SaveAsync();
        var allowed = await fixture.Service.DiagnoseAsync(PermissionRequest(permission.Code, target.Id));
        Assert.Equal(PermissionDiagnosticConclusion.Allowed, allowed.Conclusion);
        Assert.Equal("CurrentConfiguration", allowed.EvaluationBasis);
        Assert.Contains(allowed.Checks, check => check.Source == "RolePermission" && check.Description.Contains(role.Code, StringComparison.Ordinal));
        Assert.Contains(allowed.Limitations, text => text.Contains("Token", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InactiveTarget_ShouldDenyWithoutEvaluatingResource()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var target = fixture.Add(new User { IsEnabled = false });
        await fixture.SaveAsync();
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest("anything", target.Id));
        Assert.Equal(PermissionDiagnosticConclusion.Denied, result.Conclusion);
        Assert.Contains(result.Checks, check => check.Code == "identity.active" && check.Status == PermissionDiagnosticCheckStatus.Failed);
        Assert.DoesNotContain(result.Checks, check => check.Code == "api.permission");
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("unauthenticated")]
    [InlineData("tenant-mismatch")]
    [InlineData("kill-switch")]
    [InlineData("revoke-tool")]
    public async Task InvalidCaller_ShouldFailClosed(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        switch (scenario)
        {
            case "actor": fixture.Actor.IsEnabled = false; break;
            case "tenant": fixture.Db.Tenants.Single().Status = TenantStatus.Disabled; break;
            case "unauthenticated": fixture.Http.HttpContext!.User = new ClaimsPrincipal(); break;
            case "tenant-mismatch": fixture.Tenant.SetTenant(Guid.NewGuid(), "test"); break;
            case "kill-switch": fixture.Configuration.Enabled = false; break;
            case "revoke-tool": fixture.Db.RolePermissions.Single().IsDeleted = true; break;
        }
        await fixture.SaveAsync();
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(PermissionRequest("test:view")));
    }

    [Fact]
    public async Task Menu_ShouldMatchExistingTreeIncludingAncestorExpansionAndHiddenParents()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var parent = fixture.Add(new Menu { Name = "Parent" });
        var child = fixture.Add(new Menu { Name = "Child", ParentId = parent.Id });
        fixture.Add(new RoleMenu { RoleId = fixture.ActorRole.Id, MenuId = child.Id });
        await fixture.SaveAsync();
        var tree = await fixture.MenuResolver.GetCurrentUserMenusAsync();
        Assert.Equal(parent.Id, Assert.Single(tree).Id);
        var result = await fixture.Service.DiagnoseAsync(new() { Kind = PermissionDiagnosticKind.Menu, MenuId = parent.Id });
        Assert.Equal(PermissionDiagnosticConclusion.Allowed, result.Conclusion);
        parent.Visible = false;
        await fixture.SaveAsync();
        Assert.Empty(await fixture.MenuResolver.GetCurrentUserMenusAsync());
        var hidden = await fixture.Service.DiagnoseAsync(new() { Kind = PermissionDiagnosticKind.Menu, MenuId = child.Id });
        Assert.Equal(PermissionDiagnosticConclusion.Denied, hidden.Conclusion);
        Assert.Contains(hidden.Checks, check => check.Code == "menu.role-assignment" && check.Status == PermissionDiagnosticCheckStatus.Passed);
        Assert.Contains(hidden.Checks, check => check.Code == "menu.ancestors" && check.Status == PermissionDiagnosticCheckStatus.Failed);
    }

    [Fact]
    public async Task MenuWithoutConfigPermission_ShouldNotRevealHiddenMetadata()
    {
        await using var fixture = await Fixture.CreateAsync();
        var hidden = fixture.Add(new Menu { Name = "Hidden private menu", Visible = false });
        await fixture.SaveAsync();
        var result = await fixture.Service.DiagnoseAsync(new() { Kind = PermissionDiagnosticKind.Menu, MenuId = hidden.Id });
        Assert.Equal(PermissionDiagnosticConclusion.InsufficientEvidence, result.Conclusion);
        Assert.DoesNotContain(result.Checks, check => check.Source is "MenuConfiguration" or "RoleMenu");
        Assert.DoesNotContain(hidden.Name, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DataScopeType.All, PermissionDiagnosticConclusion.Allowed)]
    [InlineData(DataScopeType.CurrentUser, PermissionDiagnosticConclusion.Limited)]
    [InlineData(DataScopeType.CurrentDepartment, PermissionDiagnosticConclusion.Denied)]
    [InlineData(DataScopeType.CustomDepartments, PermissionDiagnosticConclusion.Denied)]
    public async Task DataScope_ShouldUseOverrideAndExplainMissingResourceContext(DataScopeType type, PermissionDiagnosticConclusion expected)
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        fixture.Add(new UserDataScope { UserId = fixture.Actor.Id, ScopeType = type });
        await fixture.SaveAsync();
        var actual = await fixture.Scopes.GetCurrentUserDataScopeAsync();
        var result = await fixture.Service.DiagnoseAsync(new() { Kind = PermissionDiagnosticKind.DataScope });
        Assert.Equal(type, actual.ScopeType);
        Assert.Equal(expected, result.Conclusion);
        Assert.Contains(result.Checks, check => check.Description.Contains("UserOverride", StringComparison.Ordinal));
        Assert.Contains(result.Checks, check => check.Code == "data-scope.resource" && check.Status == PermissionDiagnosticCheckStatus.NotEvaluated);
    }

    [Fact]
    public async Task OtherDataScope_ShouldUseTargetDepartmentAndMergeWithSelfInsteadOfActorScope()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var department = fixture.Add(new Department { Name = "Target department", TreePath = "/target/" });
        var target = fixture.Add(new User { DepartmentId = department.Id });
        var first = fixture.Add(new Role { Code = "Self", IsEnabled = true });
        var second = fixture.Add(new Role { Code = "Department", IsEnabled = true });
        fixture.Add(new UserRole { UserId = target.Id, RoleId = first.Id });
        fixture.Add(new UserRole { UserId = target.Id, RoleId = second.Id });
        fixture.Add(new RoleDataScope { RoleId = first.Id, ScopeType = DataScopeType.CurrentUser });
        fixture.Add(new RoleDataScope { RoleId = second.Id, ScopeType = DataScopeType.CurrentDepartment });
        await fixture.SaveAsync();
        var actual = await fixture.Scopes.ResolveAsync(new(TestIds.TenantId, target.Id, target.DepartmentId, false));
        var result = await fixture.Service.DiagnoseAsync(new() { Kind = PermissionDiagnosticKind.DataScope, TargetUserId = target.Id });
        Assert.True(actual.IncludesCurrentUser);
        Assert.Equal([department.Id], actual.DepartmentIds);
        Assert.Equal(PermissionDiagnosticConclusion.Limited, result.Conclusion);
        Assert.DoesNotContain(department.Name, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HistoryRead_ShouldReauthorizeTargetAndEvidenceWithoutRecomputingConclusion()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var target = fixture.Add(new User());
        await fixture.SaveAsync();
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest("business:approve", target.Id));
        Assert.True(await fixture.Service.CanReadAsync(result));
        var relation = await (from rp in fixture.Db.RolePermissions
            join permission in fixture.Db.Permissions on rp.PermissionId equals permission.Id
            where rp.RoleId == fixture.ActorRole.Id && permission.Code == "system:role:view" select rp).SingleAsync();
        relation.IsDeleted = true;
        await fixture.SaveAsync();
        Assert.False(await fixture.Service.CanReadAsync(result));
        Assert.Equal(PermissionDiagnosticConclusion.Denied, result.Conclusion);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"kind\":\"Unknown\"}")]
    [InlineData("{\"kind\":\"Permission\"}")]
    [InlineData("{\"kind\":\"DataScope\",\"menuId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"kind\":\"DataScope\",\"tenantId\":\"10000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"kind\":\"DataScope\",\"actorUserId\":\"30000000-0000-0000-0000-000000000001\"}")]
    public async Task Tool_ShouldRejectInvalidAndForgedIdentityArguments(string arguments)
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = new PermissionDiagnosticAiToolHandler(fixture.Service, fixture.Current, fixture.Tenant);
        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.ExecuteAsync(fixture.ToolContext, arguments));
        Assert.Equal(ErrorCode.ValidationFailed, error.ErrorCode);
    }

    [Fact]
    public async Task Tool_ShouldRejectForgedContextAndPropagateCancellation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = new PermissionDiagnosticAiToolHandler(fixture.Service, fixture.Current, fixture.Tenant);
        await Assert.ThrowsAsync<BusinessException>(() => handler.ExecuteAsync(
            new() { TenantId = TestIds.TenantId, ActorUserId = Guid.NewGuid() }, "{\"kind\":\"DataScope\"}"));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(fixture.ToolContext,
            "{\"kind\":\"DataScope\"}", source.Token));
        var result = await handler.ExecuteAsync(fixture.ToolContext, "{\"kind\":\"DataScope\"}");
        Assert.NotNull(result.PermissionDiagnostic);
        Assert.Equal(1, result.RowCount);
    }

    [Theory]
    [InlineData("ai:tool:user-query", PermissionDiagnosticKind.Permission)]
    [InlineData("system:user:view", PermissionDiagnosticKind.Permission)]
    [InlineData("system:role:view", PermissionDiagnosticKind.Permission)]
    [InlineData("system:permission:view", PermissionDiagnosticKind.Permission)]
    [InlineData("system:menu:view", PermissionDiagnosticKind.Menu)]
    [InlineData("system:role:data-scope", PermissionDiagnosticKind.DataScope)]
    public async Task OtherUser_ShouldRequireEveryPermissionInApprovedMatrix(string missing, PermissionDiagnosticKind kind)
    {
        await using var fixture = await Fixture.CreateAsync(manager: true, missingPermission: missing);
        var target = fixture.Add(new User());
        await fixture.SaveAsync();
        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(RequestFor(kind, target.Id)));
        Assert.Equal(ErrorCode.Forbidden, error.ErrorCode);
    }

    [Fact]
    public async Task SelfHistory_ShouldHideRoleEvidenceAfterRoleViewRevocation()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest("system:user:view"));
        Assert.True(await fixture.Service.CanReadAsync(result));
        var permission = fixture.Db.Permissions.Single(item => item.Code == "system:role:view");
        fixture.Db.RolePermissions.Single(item => item.PermissionId == permission.Id).IsDeleted = true;
        await fixture.SaveAsync();
        Assert.False(await fixture.Service.CanReadAsync(result));
        var current = await fixture.Service.DiagnoseAsync(PermissionRequest("system:user:view"));
        Assert.DoesNotContain(current.Checks, check => check.Source == "RolePermission");
    }

    [Fact]
    public async Task RoleEvidence_ShouldRespectSizeLimitAndMarkTruncation()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var permission = fixture.Add(new Permission { Code = "business:approve" });
        for (var index = 0; index < 25; index++)
        {
            var role = fixture.Add(new Role { Code = $"Source-{index:D2}", IsEnabled = true });
            fixture.Add(new UserRole { UserId = fixture.Actor.Id, RoleId = role.Id });
            fixture.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await fixture.SaveAsync();
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest(permission.Code));
        Assert.True(result.IsTruncated);
        Assert.Equal(PermissionDiagnosticResponse.MaxChecks, result.Checks.Count);
        Assert.True(JsonSerializer.Serialize(result).Length <= PermissionDiagnosticResponse.MaxSerializedLength);
    }

    [Fact]
    public async Task Tool_ShouldRejectOversizedAndExcessiveCompositeRequirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(PermissionRequest(new string('a', 501))));
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(PermissionRequest(string.Join('|', Enumerable.Range(0, 11).Select(index => $"p:{index}")))));
    }

    [Fact]
    public async Task StaleSuperAdminClaims_ShouldNotExpandCallerDataScopeAfterDowngrade()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true, superAdmin: true);
        var target = fixture.Add(new User());
        fixture.ActorRole.Code = "Downgraded";
        await fixture.SaveAsync();
        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.DiagnoseAsync(PermissionRequest("test:view", target.Id)));
        Assert.Equal(ErrorCode.Unauthorized, error.ErrorCode);
    }

    [Fact]
    public async Task HistoricalOtherUserCard_ShouldDisappearWhenCallerScopeShrinks()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var target = fixture.Add(new User());
        await fixture.SaveAsync();
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest("test:view", target.Id));
        Assert.True(await fixture.Service.CanReadAsync(result));
        fixture.Add(new UserDataScope { UserId = fixture.Actor.Id, ScopeType = DataScopeType.CurrentUser });
        await fixture.SaveAsync();
        Assert.False(await fixture.Service.CanReadAsync(result));
    }

    [Fact]
    public async Task LargeUnicodeEvidence_ShouldTruncateOptionalSourcesAndKeepRequiredConclusion()
    {
        await using var fixture = await Fixture.CreateAsync(manager: true);
        var permission = fixture.Add(new Permission { Code = new string('权', 40) + ":view" });
        for (var index = 0; index < 18; index++)
        {
            var role = fixture.Add(new Role { Code = new string('角', 40) + index.ToString("D2"), IsEnabled = true });
            fixture.Add(new UserRole { UserId = fixture.Actor.Id, RoleId = role.Id });
            fixture.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await fixture.SaveAsync();
        var result = await fixture.Service.DiagnoseAsync(PermissionRequest(permission.Code));
        Assert.True(result.IsTruncated);
        Assert.Equal(PermissionDiagnosticConclusion.Denied, result.Conclusion);
        Assert.Contains(result.Checks, check => check.Code == "api.permission");
        Assert.True(JsonSerializer.Serialize(result).Length <= PermissionDiagnosticResponse.MaxSerializedLength);
    }

    private static PermissionDiagnosticRequest PermissionRequest(string code, Guid? target = null) =>
        new() { Kind = PermissionDiagnosticKind.Permission, PermissionCode = code, TargetUserId = target };

    private static PermissionDiagnosticRequest RequestFor(PermissionDiagnosticKind kind, Guid target) =>
        new() { Kind = kind, TargetUserId = target, MenuId = kind == PermissionDiagnosticKind.Menu ? Guid.NewGuid() : null,
            PermissionCode = kind == PermissionDiagnosticKind.Permission ? "test:view" : null };

    private sealed class Fixture : IAsyncDisposable
    {
        public AppDbContext Db { get; private init; } = null!;
        public TenantContext Tenant { get; private init; } = null!;
        public FixedHttpContextAccessor Http { get; } = new();
        public CurrentUserService Current => new(Http);
        public User Actor { get; private set; } = null!;
        public Role ActorRole { get; private set; } = null!;
        public DataScopeService Scopes { get; private set; } = null!;
        public CurrentUserAppService MenuResolver { get; private set; } = null!;
        public PermissionDiagnosticService Service { get; private set; } = null!;
        public TestConfiguration Configuration { get; } = new();
        public AiToolExecutionContext ToolContext => new() { ActorUserId = Actor.Id, TenantId = TestIds.TenantId };

        public static async Task<Fixture> CreateAsync(bool manager = false, bool superAdmin = false, string[]? extraPermissions = null,
            string? missingPermission = null)
        {
            var tenant = new TenantContext();
            tenant.SetTenant(TestIds.TenantId, "test");
            var fixture = new Fixture { Tenant = tenant, Db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
                tenant, new NullAuditContext()) };
            fixture.Add(new Tenant { Id = TestIds.TenantId, Code = "test", Name = "Test", Status = TenantStatus.Active });
            fixture.Actor = fixture.Add(new User { Id = TestIds.NormalUserId });
            fixture.ActorRole = fixture.Add(new Role { Code = superAdmin ? ClaimConstants.SuperAdminRoleCode : "Operator", IsEnabled = true });
            fixture.Add(new UserRole { UserId = fixture.Actor.Id, RoleId = fixture.ActorRole.Id });
            var permissions = new List<string> { AiCenterConstants.ToolQueryPermission };
            if (manager) permissions.AddRange([AiCenterConstants.UserQueryPermission, "system:user:view", "system:role:view",
                "system:permission:view", "system:menu:view", "system:role:data-scope"]);
            permissions.AddRange(extraPermissions ?? []);
            permissions.RemoveAll(code => code == missingPermission);
            foreach (var code in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var permission = fixture.Add(new Permission { Code = code, Name = code, Group = "Test" });
                fixture.Add(new RolePermission { RoleId = fixture.ActorRole.Id, PermissionId = permission.Id });
            }
            if (manager) fixture.Add(new RoleDataScope { RoleId = fixture.ActorRole.Id, ScopeType = DataScopeType.All });
            var identity = new ClaimsIdentity("test");
            identity.AddClaim(new Claim(ClaimConstants.UserId, fixture.Actor.Id.ToString()));
            identity.AddClaim(new Claim(ClaimConstants.TenantId, TestIds.TenantId.ToString()));
            identity.AddClaim(new Claim("role", fixture.ActorRole.Code));
            foreach (var code in permissions) identity.AddClaim(new Claim(ClaimConstants.PermissionCode, code));
            fixture.Http.HttpContext!.User = new ClaimsPrincipal(identity);
            await fixture.SaveAsync();
            var current = fixture.Current;
            fixture.Scopes = new DataScopeService(fixture.Repo<Role>(), fixture.Repo<User>(), fixture.Repo<UserRole>(),
                fixture.Repo<RoleDataScope>(), fixture.Repo<UserDataScope>(), fixture.Repo<Department>(), current,
                NullLogger<DataScopeService>.Instance, new TestUnitOfWork());
            fixture.MenuResolver = new CurrentUserAppService(current, fixture.Repo<Menu>(), fixture.Repo<Role>(), fixture.Repo<RoleMenu>(),
                fixture.Repo<UserRole>(), fixture.Repo<RolePermission>(), fixture.Repo<Permission>(), tenant);
            fixture.Service = new PermissionDiagnosticService(current, tenant,
                new UserCredentialValidator(fixture.Db, new PasswordHasher<User>()), fixture.Repo<User>(), fixture.Repo<Menu>(),
                fixture.Repo<Role>(), fixture.Repo<UserRole>(), fixture.Repo<RolePermission>(), fixture.Repo<RoleMenu>(), fixture.Repo<Permission>(),
                fixture.Scopes, new DataPermissionFilter(), fixture.MenuResolver, new EfCoreAsyncQueryExecutor(), fixture.Configuration);
            return fixture;
        }

        public T Add<T>(T entity) where T : BaseEntity
        {
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            if (entity.TenantId == Guid.Empty) entity.TenantId = TestIds.TenantId;
            Db.Add(entity);
            return entity;
        }

        public async Task SaveAsync()
        {
            using var scope = new SystemTenantScope(Tenant, NullLogger<SystemTenantScope>.Instance).Begin("PermissionDiagnosticTestSetup");
            await Db.SaveChangesAsync();
        }

        private Repository<T> Repo<T>() where T : BaseEntity => new(Db);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestConfiguration : IAiCenterConfiguration
    {
        public bool Enabled { get; set; } = true;
        public IReadOnlyCollection<Guid> AllowedTenantIds => [TestIds.TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
    }
}
