using System.Text;
using System.Text.Json;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiStructuredResultTests
{
    [Fact]
    public async Task UserTable_ShouldSeparateAuthorizedTotalAndDisplayedRowsAndNormalizeConditions()
    {
        var fixture = new AiQueryTestFixture();
        for (var index = 0; index < 3; index++) await fixture.Users.AddAsync(new User { UserName = "alice" + index, DisplayName = "爱丽丝", IsEnabled = true });
        await fixture.Users.AddAsync(new User { TenantId = Guid.NewGuid(), UserName = "alice-hidden" });
        var output = await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{\"keyword\":\" alice \",\"limit\":1}");
        await fixture.PersistAsync(output);
        var result = Assert.Single((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
        Assert.Equal(3, result.Table!.TotalCount);
        Assert.Equal(1, result.Table.DisplayedRowCount);
        Assert.True(result.IsTruncated);
        Assert.Equal("alice", result.Context.Parameters["keyword"].GetString());
        Assert.Equal("Authorized", result.Context.Parameters["departmentScope"].GetString());
        Assert.Equal(0, fixture.Users.UpdateCount);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("expired-text")]
    [InlineData("different-tenant")]
    [InlineData("different-owner")]
    [InlineData("different-conversation")]
    [InlineData("failed-invocation")]
    [InlineData("unknown-version")]
    [InlineData("tampered-data")]
    [InlineData("tampered-parameters")]
    [InlineData("tampered-source")]
    [InlineData("assistant")]
    [InlineData("model-generated")]
    [InlineData("deleted-user")]
    [InlineData("smaller-scope")]
    [InlineData("revoked-permission")]
    [InlineData("revoked-view-permission")]
    [InlineData("inactive-identity")]
    [InlineData("inactive-tenant")]
    public async Task Reader_ShouldRejectUnavailableOrUntrustedSnapshots(string scenario)
    {
        var fixture = new AiQueryTestFixture();
        var user = new User { UserName = "alice" };
        await fixture.Users.AddAsync(user);
        var message = await fixture.PersistAsync(await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{}"));
        switch (scenario)
        {
            case "expired": message.CreatedAt = DateTimeOffset.UtcNow.AddDays(-31); break;
            case "expired-text": message.Content = "[expired]"; break;
            case "different-tenant": fixture.Tenant.SetTenant(Guid.NewGuid(), "other"); break;
            case "different-owner": fixture.Conversation.UserId = Guid.NewGuid(); break;
            case "different-conversation": fixture.Runs.Items[0].ConversationId = Guid.NewGuid(); break;
            case "failed-invocation": fixture.Invocations.Items[0].Status = AiInvocationStatus.Failed; break;
            case "unknown-version": message.Content = message.Content.Replace("\"version\":1", "\"version\":99", StringComparison.Ordinal); break;
            case "tampered-data": message.Content = message.Content.Replace("alice", "other", StringComparison.Ordinal); break;
            case "tampered-parameters": message.Content = message.Content.Replace("\"limit\":20", "\"limit\":200", StringComparison.Ordinal); break;
            case "tampered-source": message.Content = message.Content.Replace("PermissionSystem", "OtherSystem", StringComparison.Ordinal); break;
            case "assistant": message.Role = AiMessageRole.Assistant; break;
            case "model-generated": message.ModelGenerated = true; break;
            case "deleted-user": user.IsDeleted = true; break;
            case "smaller-scope": fixture.Scopes.Scope = new() { ScopeType = DataScopeType.CurrentUser, CurrentUserId = TestIds.NormalUserId }; break;
            case "revoked-permission": fixture.Identities.Actor = fixture.Identities.Actor with { PermissionCodes = [AiCenterConstants.ToolQueryPermission] }; break;
            case "revoked-view-permission": fixture.Identities.Actor = fixture.Identities.Actor with { PermissionCodes = AiQueryTestFixture.Permissions.Where(code => code != AiCenterConstants.ConversationViewPermission).ToArray() }; break;
            case "inactive-identity": fixture.Identities.Active = false; break;
            case "inactive-tenant": fixture.Configuration.AllowedTenantIds = []; break;
        }
        Assert.Empty((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => fixture.FollowUp.ResolveAsync(fixture.Conversation.Id, fixture.Reference));
    }

    [Fact]
    public async Task Reader_ShouldVerifyFullEnvelopeAgainstInvocationEvenIfMessageDigestIsRecomputed()
    {
        var fixture = new AiQueryTestFixture();
        var message = await fixture.PersistAsync(await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{}"));
        message.Content = message.Content.Replace("\"limit\":20", "\"limit\":200", StringComparison.Ordinal);
        message.ContentDigest = AiStructuredResults.Digest(message.Content);
        Assert.Empty((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
    }

    [Fact]
    public async Task Reader_ShouldAdaptOldDiagnosticAndIgnoreOldPlainToolJson()
    {
        var fixture = new AiQueryTestFixture();
        var data = new PermissionDiagnosticResponse { Target = new() { UserId = TestIds.NormalUserId, Kind = PermissionDiagnosticKind.Permission,
            PermissionCode = "system:user:view" }, EvaluatedAt = DateTimeOffset.UtcNow };
        var run = new AiRun { ConversationId = fixture.Conversation.Id, ActorUserId = TestIds.NormalUserId };
        await fixture.Runs.AddAsync(run);
        var invocation = new AiToolInvocation { RunId = run.Id, InvocationId = "old-diagnostic", ToolCode = "permission.diagnose",
            ToolVersion = "1.0", Status = AiInvocationStatus.Completed, OutputDigest = AiStructuredResults.Digest(JsonSerializer.Serialize(data, AiStructuredResults.JsonOptions)) };
        await fixture.Invocations.AddAsync(invocation);
        await fixture.Messages.AddAsync(new AiMessage { ConversationId = fixture.Conversation.Id, Role = AiMessageRole.Tool,
            Content = JsonSerializer.Serialize(new AiPermissionDiagnosticEnvelope { RunId = run.Id, InvocationId = invocation.InvocationId, Data = data }, AiStructuredResults.JsonOptions) });
        await fixture.Messages.AddAsync(new AiMessage { ConversationId = fixture.Conversation.Id, Role = AiMessageRole.Tool, Content = "{\"items\":[]}" });
        var result = Assert.Single((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
        Assert.Same(data.GetType(), result.Diagnostic!.GetType());
        Assert.Equal(JsonValueKind.Null, result.Context.Parameters["targetUserId"].ValueKind);
    }

    [Fact]
    public async Task OperationSummary_ShouldExpressModuleTruncationWithoutChangingLogTotal()
    {
        var fixture = new AiQueryTestFixture();
        var logs = new InMemoryRepository<OperationLog>();
        for (var index = 0; index < 25; index++) await logs.AddAsync(new OperationLog { Module = "module" + index, StatusCode = 200 });
        var output = await new OperationLogSummaryAiToolHandler(logs, fixture.Queries).ExecuteAsync(fixture.ToolContext, "{}");
        Assert.True(output.IsTruncated);
        var statistics = output.StructuredResult!.Statistics!;
        Assert.Equal(25, statistics.TotalCount);
        var modules = statistics.Groups.Single(item => item.Code == "byModule");
        Assert.Equal(25, modules.TotalGroupCount);
        Assert.Equal(20, modules.DisplayedGroupCount);
        Assert.True(modules.IsTruncated);
    }

    [Theory]
    [InlineData("all", 2)]
    [InlineData("self", 1)]
    [InlineData("other-department", 0)]
    public async Task CurrentDepartment_ShouldIntersectScopeWithoutIncludingChildren(string scopeKind, int expected)
    {
        var fixture = new AiQueryTestFixture();
        var department = new Department();
        await fixture.Departments.AddAsync(department);
        fixture.Current.DepartmentId = department.Id;
        fixture.Identities.Actor = fixture.Identities.Actor with { DepartmentId = department.Id };
        await fixture.Users.AddAsync(new User { Id = TestIds.NormalUserId, DepartmentId = department.Id, UserName = "self" });
        await fixture.Users.AddAsync(new User { DepartmentId = department.Id, UserName = "same" });
        await fixture.Users.AddAsync(new User { DepartmentId = Guid.NewGuid(), UserName = "child" });
        fixture.Scopes.Scope = scopeKind switch
        {
            "self" => new() { ScopeType = DataScopeType.CurrentUser, CurrentUserId = TestIds.NormalUserId },
            "other-department" => new() { ScopeType = DataScopeType.CustomDepartments, DepartmentIds = [Guid.NewGuid()] },
            _ => fixture.Scopes.Scope
        };
        var output = await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{\"departmentScope\":\"CurrentDepartment\"}");
        Assert.Equal(expected, output.StructuredResult!.Table!.TotalCount);
        Assert.DoesNotContain("child", output.ContentJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("disabled")]
    public async Task CurrentDepartment_ShouldRejectUnavailableDepartmentWithoutFallingBack(string kind)
    {
        var fixture = new AiQueryTestFixture();
        var department = new Department { Id = Guid.NewGuid(), IsDeleted = kind == "deleted", IsEnabled = kind != "disabled" };
        await fixture.Departments.AddAsync(department);
        fixture.Current.DepartmentId = kind == "missing" ? null : department.Id;
        fixture.Identities.Actor = fixture.Identities.Actor with { DepartmentId = fixture.Current.DepartmentId };
        await Assert.ThrowsAsync<BusinessException>(() => fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{\"departmentScope\":\"CurrentDepartment\"}"));
    }

    [Fact]
    public async Task UnicodeResult_ShouldTrimRowsToByteLimitWithoutLosingConditionsOrTotal()
    {
        var fixture = new AiQueryTestFixture();
        for (var index = 0; index < 30; index++) await fixture.Users.AddAsync(new User { UserName = "u" + index, DisplayName = new string('字', 500) });
        var output = await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{\"limit\":30}");
        var message = await fixture.PersistAsync(output);
        Assert.True(Encoding.UTF8.GetByteCount(message.Content) <= AiStructuredResults.MaxEnvelopeBytes);
        var result = Assert.Single((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
        Assert.True(result.IsTruncated);
        Assert.Equal(30, result.Table!.TotalCount);
        Assert.True(result.Table.DisplayedRowCount < 30);
        Assert.Equal(30, result.Context.Parameters["limit"].GetInt32());
        Assert.Equal(result.Table.DisplayedRowCount, result.Citation.RowCount);
    }

    [Fact]
    public async Task Reader_ShouldBoundRecentWindowAndAllowAnExplicitOlderRunWithinRetention()
    {
        var fixture = new AiQueryTestFixture();
        for (var index = 0; index < 51; index++)
            await fixture.PersistAsync(await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{}"));
        var recent = await fixture.Reader.ReadAsync(fixture.Conversation.Id);
        Assert.True(recent.IsWindowLimited);
        Assert.Equal(50, recent.Results.Count);
        var first = fixture.Runs.Items[0].Id;
        var targeted = await fixture.Reader.ReadAsync(fixture.Conversation.Id, first);
        Assert.Equal(first, Assert.Single(targeted.Results).RunId);
    }

    [Fact]
    public async Task ReadOnlyToolSerialization_ShouldNotExposeInternalContextToExternalMcp()
    {
        var fixture = new AiQueryTestFixture();
        var output = await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{}");
        var json = JsonSerializer.Serialize(output, AiStructuredResults.JsonOptions);
        Assert.DoesNotContain("structuredResult", json, StringComparison.Ordinal);
        Assert.DoesNotContain("dataScopeFingerprint", json, StringComparison.Ordinal);
        Assert.NotNull(output.StructuredResult);
        Assert.DoesNotContain("contextRef", fixture.UserHandler().Definition.InputSchemaJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_ShouldBoundTotalResponseBytesAndRejectCancelledReads()
    {
        var fixture = new AiQueryTestFixture();
        for (var index = 0; index < 30; index++) await fixture.Users.AddAsync(new User { UserName = "u" + index, DisplayName = new string('字', 500) });
        for (var index = 0; index < 8; index++) await fixture.PersistAsync(await fixture.UserHandler().ExecuteAsync(fixture.ToolContext, "{\"limit\":30}"));
        var page = await fixture.Reader.ReadAsync(fixture.Conversation.Id);
        Assert.True(page.IsWindowLimited);
        Assert.True(page.Results.Count < 8);
        Assert.True(page.Results.Sum(item => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(item, AiStructuredResults.JsonOptions))) <= AiStructuredResults.MaxResponseBytes);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Reader.ReadAsync(fixture.Conversation.Id, cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task MenuReference_ShouldDisappearAfterTargetDeletionAndCannotBeReused()
    {
        var fixture = new AiQueryTestFixture();
        var menu = new Menu();
        await fixture.Menus.AddAsync(menu);
        var diagnostic = new PermissionDiagnosticResponse { Target = new() { Kind = PermissionDiagnosticKind.Menu, UserId = TestIds.NormalUserId, MenuId = menu.Id } };
        await fixture.PersistAsync(new() { StructuredResult = new()
        {
            Type = "permission-diagnostic", ToolCode = "permission.diagnose", ToolVersion = "1.0", Diagnostic = diagnostic,
            Context = new() { Parameters = AiStructuredResults.Parameters(new { kind = "Menu", targetUserId = (Guid?)null, menuId = menu.Id }) },
            Citation = new() { ToolCode = "permission.diagnose", ToolVersion = "1.0" }
        } });
        Assert.Single((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
        menu.IsDeleted = true;
        Assert.Empty((await fixture.Reader.ReadAsync(fixture.Conversation.Id)).Results);
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => fixture.FollowUp.ResolveAsync(fixture.Conversation.Id, fixture.Reference));
    }
}
