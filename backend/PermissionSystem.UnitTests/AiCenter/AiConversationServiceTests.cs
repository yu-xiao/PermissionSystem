using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiConversationServiceTests
{
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
        Assert.Contains("没有经过系统只读工具验证", response.ResponseMessage!.Content, StringComparison.Ordinal);
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
                    Name = "search_users",
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

    private sealed class ServiceFixture
    {
        public ServiceFixture(IAiCenterConfiguration? configuration = null)
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
            Service = new AiConversationService(
                Conversations,
                Messages,
                Runs,
                new InMemoryRepository<AiProviderConfig>(provider),
                ToolInvocations,
                UsageLogs,
                new InMemoryAsyncQueryExecutor(),
                new TestCurrentUserService(permissions:
                [
                    AiCenterConstants.ChatUsePermission,
                    AiCenterConstants.ConversationViewPermission
                ]),
                ToolRegistry,
                Gateway,
                new TestConfigValueProtector(),
                new TestCancellationProbe(),
                new AiRunCancellationCoordinator(),
                new NullAiRunRealtimeSender(),
                new TestUnitOfWork(),
                configuration ?? new TestAiConfiguration());
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
        public Queue<AiModelGatewayResponse> Responses { get; } = new();
        public int CallCount { get; private set; }

        public Task<AiModelGatewayResponse> CompleteAsync(
            AiProviderConnectionSettings provider,
            AiModelGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Responses.Dequeue());
        }
    }

    private sealed class TestToolRegistry : IAiReadOnlyToolRegistry
    {
        public int ExecutionCount { get; private set; }

        public IReadOnlyList<AiToolDefinition> GetAvailableTools() =>
        [
            new AiToolDefinition
            {
                ToolCode = "permission.users.search",
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
            return Task.FromResult(new AiToolExecutionResult
            {
                ContentJson = "{\"items\":[{\"userName\":\"alice\"}]}",
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
