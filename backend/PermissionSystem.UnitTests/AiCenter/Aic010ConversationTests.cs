using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic010ConversationTests
{
    [Fact]
    public async Task AnswerHistoryAndReferences_AreRecheckedTogetherAfterRevocation()
    {
        using var f = new Aic010TestFixture(); var document = await f.Published();
        var gateway = new Gateway(); var (service, conversation) = await Chat(f, gateway);
        var response = await service.SendMessageAsync(conversation.Id, new() { Content = "查合成请假规则" });
        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.Contains("合成正文", response.ResponseMessage!.Content);
        Assert.Single(response.StructuredResults); Assert.Single(f.Db.AiKnowledgeRunReferences);
        var persisted = f.Db.AiMessages.Single(m => m.Role == AiMessageRole.Tool && m.Content.Contains("\"structured-result\""));
        Assert.DoesNotContain("负责人确认", persisted.Content);
        Assert.Single(response.StructuredResults[0].KnowledgeHits!);
        await f.Service.SetAccessAsync(document.Id, new() { RowVersion = document.RowVersion, RoleIds = [] });
        Assert.False(await f.Guard.CanReadAsync(response.Id, default));
        var run = await service.GetRunAsync(response.Id);
        Assert.Equal(AiKnowledgeContract.Unavailable, run.ResponseMessage!.Content);
        Assert.Empty(run.Citations); Assert.Empty(run.StructuredResults);
        var history = await service.GetDetailAsync(conversation.Id);
        Assert.Equal(AiKnowledgeContract.Unavailable, history.Messages.Last(m => m.Role == AiMessageRole.Assistant).Content);
        Assert.Empty(history.StructuredResults);
        Assert.Contains("合成正文", f.Db.AiMessages.Single(m => m.Role == AiMessageRole.Assistant).Content);
    }

    [Fact]
    public async Task EmptyKnowledgeCall_CannotValidateInventedAnswer()
    {
        using var f = new Aic010TestFixture(); var gateway = new Gateway();
        var (service, conversation) = await Chat(f, gateway);
        var response = await service.SendMessageAsync(conversation.Id, new() { Content = "查不存在的请假依据" });
        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.DoesNotContain("合成正文", response.ResponseMessage!.Content);
        Assert.Contains("没有找到", response.ResponseMessage.Content);
        Assert.Empty(f.Db.AiKnowledgeRunReferences);
    }

    [Fact]
    public async Task RevocationDuringModelCall_DiscardsLateAnswer()
    {
        using var f = new Aic010TestFixture(); var document = await f.Published();
        var gateway = new Gateway { BeforeSecond = () => f.Service.SetAccessAsync(document.Id, new() { RowVersion = document.RowVersion, RoleIds = [] }) };
        var (service, conversation) = await Chat(f, gateway);
        var response = await service.SendMessageAsync(conversation.Id, new() { Content = "查请假规则" });
        Assert.Equal(AiRunStatus.Failed, response.Status);
        Assert.DoesNotContain(f.Db.AiMessages, m => m.Role == AiMessageRole.Assistant && m.Content.Contains("合成正文"));
        Assert.Empty(response.Citations); Assert.Empty(response.StructuredResults);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("hash")]
    [InlineData("status")]
    [InlineData("actor")]
    [InlineData("envelope")]
    [InlineData("digest")]
    [InlineData("missing-invocation")]
    public async Task IncompleteOrAlteredEvidence_DefaultsToUnreadable(string change)
    {
        using var f = new Aic010TestFixture(); await f.Published();
        var (service, conversation) = await Chat(f, new Gateway());
        var response = await service.SendMessageAsync(conversation.Id, new() { Content = "查请假规则" });
        switch (change)
        {
            case "reference": f.Db.AiKnowledgeRunReferences.Single().IsDeleted = true; break;
            case "hash": f.Db.AiKnowledgeRunReferences.Single().ContentHash = new string('0', 64); break;
            case "status": f.Db.AiToolInvocations.Single().Status = AiInvocationStatus.Failed; break;
            case "actor": f.Db.AiRuns.Single().ActorUserId = Guid.NewGuid(); break;
            case "envelope":
                f.Db.AiMessages.Single(m => m.Role == AiMessageRole.Tool && m.Content.Contains("knowledge-citations")).Content = "{}"; break;
            case "digest": f.Db.AiToolInvocations.Single().OutputDigest = new string('0', 64); break;
            case "missing-invocation": f.Db.AiToolInvocations.Single().IsDeleted = true; break;
        }
        await f.Db.SaveChangesAsync();
        Assert.False(await f.Guard.CanReadAsync(response.Id, default));
    }

    [Fact]
    public async Task FollowUp_OnlyKeepsQueryConditionsAndRequeriesCurrentSources()
    {
        using var f = new Aic010TestFixture(); await f.Published();
        var gateway = new Gateway(); var (service, conversation) = await Chat(f, gateway);
        var first = await service.SendMessageAsync(conversation.Id, new() { Content = "查请假规则" });
        var result = Assert.Single(first.StructuredResults);
        var follow = new AiFollowUpContextService(Reader(f), f.QueryGuard);
        var arguments = await follow.PrepareArgumentsAsync(conversation.Id, AiKnowledgeContract.ToolCode, "{\"limit\":1}",
            new(first.Id, result.InvocationId));
        Assert.Contains("\"limit\":1", arguments);
        Assert.DoesNotContain("负责人确认", arguments);
        var document = (await f.Service.ListAsync(1, 50)).Items.Single();
        await f.Service.SetAccessAsync(document.Id, new() { RowVersion = document.RowVersion, RoleIds = [] });
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => follow.ResolveAsync(conversation.Id, new(first.Id, result.InvocationId)));
    }

    private static AiStructuredResultReader Reader(Aic010TestFixture f) => new(f.Repo<AiMessage>(), f.Repo<AiRun>(), f.Repo<AiConversation>(),
        f.Repo<AiToolInvocation>(), f.Repo<User>(), f.Repo<Department>(), f.Current, f.Tenant, f.Queries,
        new AiQueryTestFixture.DiagnosticSource(), f.QueryGuard, new DataPermissionFilter(), f.Options,
        knowledge: f.Service, knowledgeRuns: f.Guard);

    private static async Task<(AiConversationService Service, AiConversation Conversation)> Chat(Aic010TestFixture f, Gateway gateway)
    {
        var conversation = new AiConversation { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, UserId = TestIds.NormalUserId,
            Title = "合成知识测试", Status = AiConversationStatus.Active, LastMessageAt = DateTimeOffset.UtcNow,
            RetentionUntil = DateTimeOffset.UtcNow.AddDays(1), AgentCode = "permission-platform-agent", AgentVersion = "1.0" };
        f.Db.AiConversations.Add(conversation);
        f.Db.AiProviderConfigs.Add(new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ProviderCode = "synthetic", ProviderName = "Synthetic",
            IsDefault = true, IsEnabled = true, SupportsTools = true, ComplianceConfirmedAt = DateTimeOffset.UtcNow,
            BaseUrl = "https://evaluation.invalid", ModelName = "synthetic", ApiKeyEncrypted = "unusable-fixture" });
        await f.Db.SaveChangesAsync();
        var reader = Reader(f);
        var tool = new KnowledgeDocumentSearchAiToolHandler(f.Service, f.Options, f.QueryGuard);
        var registry = new AiReadOnlyToolRegistry([tool], f.Current, f.Tenant, new TraceContextAccessor());
        return (new(f.Repo<AiConversation>(), f.Repo<AiMessage>(), f.Repo<AiRun>(), f.Repo<AiProviderConfig>(), f.Repo<AiToolInvocation>(),
            f.Repo<AiUsageLog>(), f.Queries, f.Current, registry, gateway, new FixtureProtector(), new NeverCancelled(),
            new AiRunCancellationCoordinator(), new NullAiRunRealtimeSender(), f.Unit, f.Options,
            structuredReader: reader, followUp: new AiFollowUpContextService(reader, f.QueryGuard), knowledgeRuns: f.Guard), conversation);
    }

    private sealed class Gateway : IAiModelGateway
    {
        private int _calls;
        public Func<Task>? BeforeSecond { get; init; }
        public async Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            if (++_calls == 1) return new() { ToolCalls = [new() { Id = Guid.NewGuid().ToString("N"), Name = "search_knowledge_documents", ArgumentsJson = "{\"keyword\":\"请假\"}" }] };
            if (BeforeSecond is not null) await BeforeSecond();
            return new() { Content = "合成正文回答引用第一版。" };
        }
    }

    private sealed class FixtureProtector : IConfigValueProtector
    { public string Protect(string value) => "unusable-fixture"; public string Unprotect(string protectedValue) => "unusable-fixture"; }
    private sealed class NeverCancelled : IAiRunCancellationProbe
    { public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult(false); }
}
