using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;
using System.Text.Json;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Tenants;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiConversationServiceTests
{
    [Fact]
    public async Task DiagnosticTool_ShouldPersistServerCardAndKeepConclusionWhenModelDisagrees()
    {
        var fixture = new ServiceFixture(includeDiagnostics: true);
        fixture.ToolRegistry.Diagnostic = new PermissionDiagnosticResponse
        {
            Target = new() { Kind = PermissionDiagnosticKind.Permission, UserId = TestIds.NormalUserId, PermissionCode = "test:view" },
            Conclusion = PermissionDiagnosticConclusion.Denied,
            Summary = "Permission missing",
            EvaluatedAt = DateTimeOffset.UtcNow
        };
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            ToolCalls = [new() { Id = "diagnostic-1", Name = PermissionDiagnosticAiToolHandler.FunctionName, ArgumentsJson = "{\"kind\":\"Permission\",\"permissionCode\":\"test:view\"}" }]
        });
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse { Content = "The model incorrectly says allowed." });
        var run = await fixture.Service.SendMessageAsync(fixture.Conversation.Id, new() { Content = "Explain test:view" });
        Assert.Equal(PermissionDiagnosticConclusion.Denied, Assert.Single(run.PermissionDiagnostics).Data.Conclusion);
        Assert.Contains("incorrectly", run.ResponseMessage!.Content, StringComparison.Ordinal);
        var reloaded = await fixture.Service.GetDetailAsync(fixture.Conversation.Id);
        Assert.Equal(PermissionDiagnosticConclusion.Denied, Assert.Single(reloaded.PermissionDiagnostics).Data.Conclusion);
        Assert.DoesNotContain(reloaded.Messages, message => message.Role == AiMessageRole.Tool);
        fixture.Diagnostics.AllowRead = false;
        Assert.Empty((await fixture.Service.GetDetailAsync(fixture.Conversation.Id)).PermissionDiagnostics);
    }

    [Fact]
    public async Task SendMessageAsync_UsesSameOutputLimitForEstimatesAndEachRoutedRequest()
    {
        var primary = Provider("primary");
        var fallback = Provider("fallback");
        fallback.MaxTokens = 512;
        var fixture = new ServiceFixture(routeCandidates:
        [
            new AiModelRouteCandidate(primary, AiModelRouteRole.Primary),
            new AiModelRouteCandidate(fallback, AiModelRouteRole.Fallback)
        ]);
        fixture.Gateway.Failures.Enqueue(new AiModelGatewayException(
            "provider_timeout", ErrorCode.InternalServerError, "Timed out", true));
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            ToolCalls = [new AiModelToolCall { Id = "call-1", Name = "test_search_users", ArgumentsJson = "{}" }]
        });
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse { Content = "Found a user." });

        var response = await fixture.Service.SendMessageAsync(fixture.Conversation.Id,
            new SendAiMessageRequest { Content = "Find users" });

        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.Equal(new int?[] { 4096, 512, 512 }, fixture.Gateway.Requests.Select(request => request.MaxTokens));
        Assert.Equal(new int?[] { 4096, 512, 512 }, fixture.UsageLogs.Items.Select(usage => usage.EstimatedOutputTokens));
        Assert.All(fixture.UsageLogs.Items, usage => Assert.True(usage.EstimatedInputTokens > 0));
    }

    [Fact]
    public async Task SendMessageAsync_WithoutToolEvidenceReturnsSafeRefusal()
    {
        var fixture = new ServiceFixture();
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Content = "There are 12 users.",
            Model = "test-model",
            InputTokens = 8,
            OutputTokens = 5,
            TotalTokens = 13
        });

        var response = await fixture.Service.SendMessageAsync(
            fixture.Conversation.Id,
            new SendAiMessageRequest { Content = "有多少用户？" });

        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.Contains("没有经过系统工具验证", response.ResponseMessage!.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("12", response.ResponseMessage.Content, StringComparison.Ordinal);
        Assert.Single(fixture.UsageLogs.Items);
        Assert.Empty(fixture.ToolInvocations.Items);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolCallPersistsAuditAndCitation()
    {
        var fixture = new ServiceFixture();
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Model = "test-model",
            ToolCalls =
            [
                new AiModelToolCall
                {
                    Id = "call-1",
                    Name = "test_search_users",
                    ArgumentsJson = "{\"keyword\":\"alice\"}"
                }
            ]
        });
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Content = "查询到 1 个匹配用户。",
            Model = "test-model",
            InputTokens = 12,
            OutputTokens = 7,
            TotalTokens = 19
        });

        var response = await fixture.Service.SendMessageAsync(
            fixture.Conversation.Id,
            new SendAiMessageRequest { Content = "查询 alice" });

        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.Equal("查询到 1 个匹配用户。", response.ResponseMessage!.Content);
        var invocation = Assert.Single(fixture.ToolInvocations.Items);
        Assert.Equal(AiInvocationStatus.Completed, invocation.Status);
        Assert.Equal("permission.users.search", invocation.ToolCode);
        var citation = Assert.Single(response.Citations);
        Assert.Equal("permission.users.search", citation.ToolCode);
        Assert.Equal(2, fixture.UsageLogs.Items.Count);
        Assert.Equal(1, fixture.ToolRegistry.ExecutionCount);
    }

    [Fact]
    public async Task SendMessageAsync_ExecutesDraftActionWithoutCreatingDataCitation()
    {
        var actionRegistry = new TestActionToolRegistry();
        var fixture = new ServiceFixture(actionToolRegistry: actionRegistry, includeDraftPermissions: true);
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Model = "test-model",
            ToolCalls =
            [
                new AiModelToolCall
                {
                    Id = "draft-call-1",
                    Name = AiBusinessActionConstants.DemoBusinessOrderFunctionName,
                    ArgumentsJson = "{\"title\":\"Order\",\"customerName\":\"Contoso\",\"amount\":10}"
                }
            ]
        });
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Content = "草稿已生成，尚未创建正式单据。",
            Model = "test-model"
        });

        var response = await fixture.Service.SendMessageAsync(
            fixture.Conversation.Id,
            new SendAiMessageRequest { Content = "创建一张订单草稿" });

        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.Equal(1, actionRegistry.ExecutionCount);
        Assert.Empty(response.Citations);
        var invocation = Assert.Single(fixture.ToolInvocations.Items);
        Assert.Equal(AiBusinessActionConstants.DemoBusinessOrderToolCode, invocation.ToolCode);
        Assert.Null(invocation.CitationJson);
    }

    [Fact]
    public async Task SendMessageAsync_OnTransientProviderFailureUsesConfiguredFallback()
    {
        var primary = Provider("primary");
        var fallback = Provider("fallback");
        var fixture = new ServiceFixture(routeCandidates:
        [
            new AiModelRouteCandidate(primary, AiModelRouteRole.Primary),
            new AiModelRouteCandidate(fallback, AiModelRouteRole.Fallback)
        ]);
        fixture.Gateway.Failures.Enqueue(new AiModelGatewayException(
            "provider_unavailable",
            ErrorCode.InternalServerError,
            "Unavailable",
            true));
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Content = "fallback answer",
            Model = fallback.ModelName,
            InputTokens = 4,
            OutputTokens = 2
        });

        var response = await fixture.Service.SendMessageAsync(
            fixture.Conversation.Id,
            new SendAiMessageRequest { Content = "查询" });

        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.Equal(1, response.FallbackCount);
        Assert.Equal(fallback.ModelName, response.ModelName);
        Assert.Collection(
            fixture.UsageLogs.Items,
            item => Assert.Equal(AiModelRouteRole.Primary, item.RouteRole),
            item => Assert.Equal(AiModelRouteRole.Fallback, item.RouteRole));
    }

    [Fact]
    public async Task CreateAsync_WhenTenantIsNotAllowlistedIsRejected()
    {
        var fixture = new ServiceFixture(new TestAiConfiguration
        {
            Enabled = true,
            AllowedTenantIds = [Guid.NewGuid()]
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.CreateAsync(new CreateAiConversationRequest()));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task CancelRunAsync_MarksPendingRunCancelled()
    {
        var fixture = new ServiceFixture();
        var run = new AiRun
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ConversationId = fixture.Conversation.Id,
            ActorUserId = TestIds.NormalUserId,
            Status = AiRunStatus.Pending
        };
        fixture.Runs.Seed(run);

        await fixture.Service.CancelRunAsync(run.Id);

        Assert.Equal(AiRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CancellationRequestedAt);
        Assert.Equal("run_cancelled", run.ErrorCode);
    }

    [Fact]
    public async Task RetryRunAsync_CreatesRunLinkedToFailedRun()
    {
        var fixture = new ServiceFixture();
        var requestMessage = new AiMessage
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ConversationId = fixture.Conversation.Id,
            Role = AiMessageRole.User,
            Content = "重试查询",
            ContentDigest = "digest",
            Sequence = 1
        };
        await fixture.Messages.AddAsync(requestMessage);
        var failedRun = new AiRun
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ConversationId = fixture.Conversation.Id,
            RequestMessageId = requestMessage.Id,
            ActorUserId = TestIds.NormalUserId,
            ProviderConfigId = Guid.NewGuid(),
            AgentCode = "permission-platform-agent",
            AgentVersion = "2.0",
            PromptVersion = "2.0",
            ModelName = "test-model",
            TraceId = "trace",
            ExecutionLeaseId = Guid.NewGuid(),
            Status = AiRunStatus.Failed
        };
        fixture.Runs.Seed(failedRun);
        fixture.Gateway.Responses.Enqueue(new AiModelGatewayResponse
        {
            Content = "重试成功",
            Model = "test-model",
            InputTokens = 2,
            OutputTokens = 2,
            TotalTokens = 4
        });

        var response = await fixture.Service.RetryRunAsync(failedRun.Id);

        Assert.Equal(AiRunStatus.Completed, response.Status);
        var retry = fixture.Runs.Query().Single(run => run.Id != failedRun.Id);
        Assert.Equal(failedRun.Id, retry.RetryOfRunId);
    }

    [Fact]
    public async Task FollowUp_ShouldMergeSelectedContextAndPersistFullEnvelopeWithBoundedServerResult()
    {
        var source = SourceResult();
        var fixture = new ServiceFixture(structuredPage: new([source]));
        var query = new AiQueryTestFixture();
        fixture.ToolRegistry.OnExecute = args => query.UserHandler().ExecuteAsync(query.ToolContext, args);
        fixture.Gateway.Responses.Enqueue(new() { ToolCalls = [new() { Id = "follow-up-1", Name = "test_search_users", ArgumentsJson = "{\"isEnabled\":false}" }] });
        fixture.Gateway.Responses.Enqueue(new() { Content = "查询完成" });
        var response = await fixture.Service.SendMessageAsync(fixture.Conversation.Id, new()
        {
            Content = "只看停用的用户", ContextRef = new(source.RunId, source.InvocationId)
        });
        Assert.Equal(AiRunStatus.Completed, response.Status);
        using var args = JsonDocument.Parse(Assert.Single(fixture.ToolRegistry.ExecutionArguments));
        Assert.Equal("alice", args.RootElement.GetProperty("keyword").GetString());
        Assert.Equal(1, args.RootElement.GetProperty("limit").GetInt32());
        Assert.False(args.RootElement.GetProperty("isEnabled").GetBoolean());
        var stored = fixture.Messages.Items.Single(item => item.Role == AiMessageRole.Tool && item.Content.Contains("\"type\":\"structured-result\"", StringComparison.Ordinal));
        var envelope = JsonSerializer.Deserialize<AiStructuredResultEnvelope>(stored.Content, AiStructuredResults.JsonOptions)!;
        Assert.Equal(response.Id, envelope.Result.RunId);
        Assert.Equal("follow-up-1", envelope.Result.InvocationId);
        Assert.Equal(AiStructuredResults.Digest(stored.Content), fixture.ToolInvocations.Items[0].OutputDigest);
        Assert.Equal(AiStructuredResults.Digest(args.RootElement.GetRawText()), fixture.ToolInvocations.Items[0].InputDigest);
        var detail = await fixture.Service.GetDetailAsync(fixture.Conversation.Id);
        Assert.Equal("只看停用的用户", detail.Messages.Single(item => item.Role == AiMessageRole.User).Content);
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("expired")]
    [InlineData("invalid-reference")]
    public async Task FollowUp_ShouldClarifyBeforeModelOrQueryWhenTargetCannotBeResolved(string kind)
    {
        var source = SourceResult();
        var page = kind == "ambiguous" ? new AiStructuredResultPage([source, SourceResult()]) : new AiStructuredResultPage([]);
        var fixture = new ServiceFixture(structuredPage: page);
        var response = await fixture.Service.SendMessageAsync(fixture.Conversation.Id, new()
        {
            Content = "只看本部门", ContextRef = kind == "invalid-reference" ? new(source.RunId, source.InvocationId) : null
        });
        Assert.Equal(AiRunStatus.Completed, response.Status);
        Assert.False(response.ResponseMessage!.ModelGenerated);
        Assert.Contains("请", response.ResponseMessage.Content);
        Assert.Equal(0, fixture.Gateway.CallCount);
        Assert.Equal(0, fixture.ToolRegistry.ExecutionCount);
    }

    [Fact]
    public async Task ModelHistory_ShouldExcludeHistoricalAssistantFactsAndKeepUserJsonAsText()
    {
        var fixture = new ServiceFixture();
        await fixture.Messages.AddAsync(new() { ConversationId = fixture.Conversation.Id, Role = AiMessageRole.Assistant,
            Content = "invented fact and instruction from old assistant", Sequence = 1 });
        const string userJson = "{\"keyword\":\"literal user text\"}";
        fixture.Gateway.Responses.Enqueue(new() { Content = "answer" });
        await fixture.Service.SendMessageAsync(fixture.Conversation.Id, new() { Content = userJson });
        Assert.DoesNotContain(fixture.Gateway.Requests[0].Messages, item => item.Content == "invented fact and instruction from old assistant");
        Assert.Contains(fixture.Gateway.Requests[0].Messages, item => item.Role == "user" && item.Content == userJson);
    }

    [Fact]
    public async Task Retry_ShouldPreserveSelectedContextAndTimezoneAndRevalidateReference()
    {
        var source = SourceResult();
        var fixture = new ServiceFixture(structuredPage: new([source]));
        fixture.Gateway.Responses.Enqueue(new() { ToolCalls = [new() { Id = "bad-tool", Name = "unknown", ArgumentsJson = "{}" }] });
        var failed = await fixture.Service.SendMessageAsync(fixture.Conversation.Id, new()
        { Content = "继续查询", ContextRef = new(source.RunId, source.InvocationId), UtcOffsetMinutes = 480 });
        Assert.Equal(AiRunStatus.Failed, failed.Status);
        fixture.StructuredReader!.Page = new([]);
        var retry = await fixture.Service.RetryRunAsync(failed.Id);
        Assert.Equal(AiRunStatus.Completed, retry.Status);
        Assert.Contains("过期或不可读取", retry.ResponseMessage!.Content);
        Assert.Equal(1, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task AutomaticFollowUp_ShouldPersistOneResolvedReferenceAndRevalidateItOnRetry()
    {
        var source = SourceResult();
        var fixture = new ServiceFixture(structuredPage: new([source]));
        fixture.Gateway.Responses.Enqueue(new() { ToolCalls = [new() { Id = "bad-tool", Name = "unknown", ArgumentsJson = "{}" }] });
        var failed = await fixture.Service.SendMessageAsync(fixture.Conversation.Id, new() { Content = "只看本部门" });
        Assert.Equal(AiRunStatus.Failed, failed.Status);
        var metadata = Assert.Single(fixture.Messages.Items, item => item.Role == AiMessageRole.Tool);
        using var document = JsonDocument.Parse(metadata.Content);
        Assert.Equal(source.RunId, document.RootElement.GetProperty("contextRef").GetProperty("runId").GetGuid());
        Assert.Equal(source.InvocationId, document.RootElement.GetProperty("contextRef").GetProperty("invocationId").GetString());
        Assert.Equal(AiStructuredResults.Digest(metadata.Content), metadata.ContentDigest);
        var detail = await fixture.Service.GetDetailAsync(fixture.Conversation.Id);
        Assert.DoesNotContain(detail.Messages, item => item.Role == AiMessageRole.Tool);
        fixture.StructuredReader!.Page = new([]);
        var retry = await fixture.Service.RetryRunAsync(failed.Id);
        Assert.Equal(AiRunStatus.Completed, retry.Status);
        Assert.Contains("过期或不可读取", retry.ResponseMessage!.Content);
        Assert.Equal(1, fixture.Gateway.CallCount);
    }

    private static AiStructuredResult SourceResult() => new()
    {
        RunId = Guid.NewGuid(), InvocationId = Guid.NewGuid().ToString("N"), Type = "table", ToolCode = "permission.users.search", ToolVersion = "1.0",
        Context = new() { Parameters = AiStructuredResults.Parameters(new { keyword = "alice", limit = 1, isEnabled = (bool?)null, departmentScope = "Authorized" }) },
        Table = new()
    };

    private sealed class ServiceFixture
    {
        public ServiceFixture(
            IAiCenterConfiguration? configuration = null,
            IAiActionToolRegistry? actionToolRegistry = null,
            bool includeDraftPermissions = false,
            IReadOnlyList<AiModelRouteCandidate>? routeCandidates = null,
            bool includeDiagnostics = false,
            AiStructuredResultPage? structuredPage = null)
        {
            Conversation = new AiConversation
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                UserId = TestIds.NormalUserId,
                AgentCode = "permission-readonly-agent",
                AgentVersion = "1.0",
                Title = "新会话",
                Status = AiConversationStatus.Active,
                LastMessageAt = DateTimeOffset.UtcNow,
                RetentionUntil = DateTimeOffset.UtcNow.AddDays(30)
            };
            var provider = new AiProviderConfig
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                ProviderCode = "primary",
                ProviderName = "Primary",
                BaseUrl = "https://api.example.test",
                ChatCompletionsPath = "v1/chat/completions",
                ApiKeyEncrypted = "protected:test-key",
                ModelName = "test-model",
                IsDefault = true,
                IsEnabled = true,
                ComplianceConfirmedAt = DateTimeOffset.UtcNow,
                AllowedHostsJson = "[\"api.example.test\"]"
            };
            Conversations = new InMemoryRepository<AiConversation>(Conversation);
            Messages = new InMemoryRepository<AiMessage>();
            Runs = new SeedableRepository<AiRun>();
            ToolInvocations = new InMemoryRepository<AiToolInvocation>();
            UsageLogs = new InMemoryRepository<AiUsageLog>();
            Gateway = new TestModelGateway();
            ToolRegistry = new TestToolRegistry();
            var permissions = new List<string>
            {
                AiCenterConstants.ChatUsePermission,
                AiCenterConstants.ConversationViewPermission
            };
            if (includeDraftPermissions)
            {
                permissions.Add(AiCenterConstants.DocumentDraftPermission);
                permissions.Add("demo-business-order:create");
            }

            var currentUser = new TestCurrentUserService(permissions: permissions);
            var tenant = new TenantContext();
            tenant.SetTenant(TestIds.TenantId, "test");
            var diagnosticReader = includeDiagnostics ? new AiPermissionDiagnosticReader(Messages, Runs, Conversations,
                ToolInvocations, currentUser, tenant, new InMemoryAsyncQueryExecutor(), Diagnostics) : null;
            StructuredReader = structuredPage is null ? null : new TestStructuredReader { Page = structuredPage };
            Service = new AiConversationService(
                Conversations,
                Messages,
                Runs,
                new InMemoryRepository<AiProviderConfig>(provider),
                ToolInvocations,
                UsageLogs,
                new InMemoryAsyncQueryExecutor(),
                currentUser,
                ToolRegistry,
                Gateway,
                new TestConfigValueProtector(),
                new TestCancellationProbe(),
                new AiRunCancellationCoordinator(),
                new NullAiRunRealtimeSender(),
                new TestUnitOfWork(),
                configuration ?? new TestAiConfiguration(),
                actionToolRegistry,
                null,
                routeCandidates is null ? null : new TestModelRouteService(routeCandidates),
                diagnosticReader: diagnosticReader, structuredReader: StructuredReader,
                followUp: StructuredReader is null ? null : new AiFollowUpContextService(StructuredReader, new AiQueryTestFixture().Guard));
        }

        public AiConversation Conversation { get; }
        public InMemoryRepository<AiConversation> Conversations { get; }
        public InMemoryRepository<AiMessage> Messages { get; }
        public SeedableRepository<AiRun> Runs { get; }
        public InMemoryRepository<AiToolInvocation> ToolInvocations { get; }
        public InMemoryRepository<AiUsageLog> UsageLogs { get; }
        public TestModelGateway Gateway { get; }
        public TestToolRegistry ToolRegistry { get; }
        public AiConversationService Service { get; }
        public TestDiagnosticService Diagnostics { get; } = new();
        public TestStructuredReader? StructuredReader { get; }
    }

    private sealed class TestStructuredReader : IAiStructuredResultReader
    {
        public AiStructuredResultPage Page { get; set; } = new([]);
        public Task<AiStructuredResultPage> ReadAsync(Guid conversationId, Guid? runId = null, CancellationToken cancellationToken = default) => Task.FromResult(Page);
    }

    private sealed class SeedableRepository<TEntity> : PermissionSystem.Domain.Repositories.IRepository<TEntity>
        where TEntity : PermissionSystem.Domain.Common.BaseEntity
    {
        private readonly InMemoryRepository<TEntity> _inner = new();

        public void Seed(TEntity entity) => _inner.AddAsync(entity).GetAwaiter().GetResult();
        public IQueryable<TEntity> Query() => _inner.Query();
        public IQueryable<TEntity> QueryForTenant(Guid tenantId) => _inner.QueryForTenant(tenantId);
        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _inner.GetByIdAsync(id, cancellationToken);
        public Task<IReadOnlyList<TEntity>> ListAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => _inner.ListAsync(predicate, cancellationToken);
        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) => _inner.AddAsync(entity, cancellationToken);
        public void Update(TEntity entity) => _inner.Update(entity);
        public void Remove(TEntity entity) => _inner.Remove(entity);
    }

    private sealed class TestModelGateway : IAiModelGateway
    {
        public List<AiModelGatewayRequest> Requests { get; } = [];
        public Queue<AiModelGatewayResponse> Responses { get; } = new();
        public Queue<AiModelGatewayException> Failures { get; } = new();
        public int CallCount { get; private set; }

        public Task<AiModelGatewayResponse> CompleteAsync(
            AiProviderConnectionSettings provider,
            AiModelGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
            if (Failures.TryDequeue(out var failure))
            {
                throw failure;
            }

            return Task.FromResult(Responses.Dequeue());
        }
    }

    private sealed class TestModelRouteService : IAiModelRouteService
    {
        private readonly IReadOnlyList<AiModelRouteCandidate> _candidates;

        public TestModelRouteService(IReadOnlyList<AiModelRouteCandidate> candidates)
        {
            _candidates = candidates;
        }

        public Task<IReadOnlyList<AiModelRoutePolicyResponse>> GetPoliciesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiModelRoutePolicyResponse>>([]);

        public Task<IReadOnlyList<AiModelRouteProviderOptionResponse>> GetProviderOptionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiModelRouteProviderOptionResponse>>([]);

        public Task<AiModelRoutePolicyResponse> SavePolicyAsync(
            SaveAiModelRoutePolicyRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiModelRouteCandidate>> ResolveAsync(
            string agentCode,
            Guid conversationId,
            CancellationToken cancellationToken = default) => Task.FromResult(_candidates);
    }

    private static AiProviderConfig Provider(string code)
    {
        return new AiProviderConfig
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ProviderCode = code,
            ProviderName = code,
            BaseUrl = "https://api.example.test",
            ChatCompletionsPath = "v1/chat/completions",
            ApiKeyEncrypted = "protected:test-key",
            ModelName = $"{code}-model",
            IsEnabled = true,
            SupportsTools = true,
            ComplianceConfirmedAt = DateTimeOffset.UtcNow,
            AllowedHostsJson = "[\"api.example.test\"]"
        };
    }

    private sealed class TestToolRegistry : IAiReadOnlyToolRegistry
    {
        public PermissionDiagnosticResponse? Diagnostic { get; set; }
        public int ExecutionCount { get; private set; }
        public List<string> ExecutionArguments { get; } = [];
        public Func<string, Task<AiToolExecutionResult>>? OnExecute { get; set; }

        public IReadOnlyList<AiToolDefinition> GetAvailableTools() =>
        [
            new AiToolDefinition
            {
                ToolCode = Diagnostic is null ? "permission.users.search" : PermissionDiagnosticAiToolHandler.ToolCode,
                FunctionName = Diagnostic is null ? "test_search_users" : PermissionDiagnosticAiToolHandler.FunctionName,
                Version = "1.0",
                Description = "Search users.",
                InputSchemaJson = "{\"type\":\"object\"}"
            }
        ];

        public Task<AiToolExecutionResult> ExecuteAsync(
            string toolCode,
            string argumentsJson,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            ExecutionArguments.Add(argumentsJson);
            if (OnExecute is not null) return OnExecute(argumentsJson);
            return Task.FromResult(new AiToolExecutionResult
            {
                ContentJson = Diagnostic is null ? "{\"items\":[{\"userName\":\"alice\"}]}" : JsonSerializer.Serialize(Diagnostic, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                PermissionDiagnostic = Diagnostic,
                RowCount = 1,
                Citation = new AiToolCitation
                {
                    ToolCode = toolCode,
                    ToolVersion = "1.0",
                    QueryParametersDigest = "digest",
                    QueriedAt = DateTimeOffset.UtcNow,
                    RowCount = 1
                }
            });
        }
    }

    private sealed class TestDiagnosticService : IPermissionDiagnosticService
    {
        public bool AllowRead { get; set; } = true;
        public Task<PermissionDiagnosticResponse> DiagnoseAsync(PermissionDiagnosticRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Reading historical evidence must not execute diagnosis.");
        public Task<bool> CanReadAsync(PermissionDiagnosticResponse result, CancellationToken cancellationToken = default) => Task.FromResult(AllowRead);
    }

    private sealed class TestActionToolRegistry : IAiActionToolRegistry
    {
        public int ExecutionCount { get; private set; }

        public IReadOnlyList<AiToolDefinition> GetAvailableTools() =>
        [
            new AiToolDefinition
            {
                ToolCode = AiBusinessActionConstants.DemoBusinessOrderToolCode,
                FunctionName = AiBusinessActionConstants.DemoBusinessOrderFunctionName,
                Version = "1.0",
                Description = "Prepare a draft.",
                InputSchemaJson = "{\"type\":\"object\"}"
            }
        ];

        public bool IsActionTool(string toolCode) =>
            toolCode == AiBusinessActionConstants.DemoBusinessOrderToolCode;

        public Task<AiActionToolExecutionResult> ExecuteAsync(
            string toolCode,
            AiActionDraftContext context,
            string argumentsJson,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(new AiActionToolExecutionResult
            {
                ContentJson = "{\"type\":\"document_draft\"}",
                Draft = new AiDocumentDraftResponse { Id = Guid.NewGuid() }
            });
        }
    }

    private sealed class TestCancellationProbe : IAiRunCancellationProbe
    {
        public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class TestAiConfiguration : IAiCenterConfiguration
    {
        public bool Enabled { get; init; } = true;
        public IReadOnlyCollection<Guid> AllowedTenantIds { get; init; } = [TestIds.TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
    }
}
