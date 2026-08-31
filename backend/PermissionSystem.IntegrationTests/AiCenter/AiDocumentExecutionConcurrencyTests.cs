using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.UnitOfWork;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class AiDocumentExecutionConcurrencyTests
{
    private const string ConnectionEnvName = "PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentBusinessKey_ShouldPersistOnlyOneOrderExecutionAndOutboxMessage()
    {
        var seed = await CreateSeedAsync();
        try
        {
            var attempts = await Task.WhenAll(
                TryExecuteAsync(seed, "DBO-P3-A"),
                TryExecuteAsync(seed, "DBO-P3-B"));

            Assert.Single(attempts, succeeded => succeeded);
            await using var verification = CreateContext(seed.TenantId);
            Assert.Equal(1, await verification.AiDocumentExecutions.IgnoreQueryFilters()
                .CountAsync(entity => entity.ConfirmationId == seed.ConfirmationId));
            Assert.Equal(1, await verification.DemoBusinessOrders.IgnoreQueryFilters()
                .CountAsync(entity => entity.TenantId == seed.TenantId));
            Assert.Equal(1, await verification.OutboxMessages.IgnoreQueryFilters()
                .CountAsync(entity => entity.TenantId == seed.TenantId));
        }
        finally
        {
            await CleanupAsync(seed.TenantId);
        }
    }

    private static async Task<bool> TryExecuteAsync(SeedIds seed, string orderNo)
    {
        try
        {
            await using var context = CreateContext(seed.TenantId);
            var unitOfWork = new UnitOfWork(context);
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var executionId = Guid.NewGuid();
                var orderId = Guid.NewGuid();
                context.AiDocumentExecutions.Add(new AiDocumentExecution
                {
                    Id = executionId,
                    TenantId = seed.TenantId,
                    ConfirmationId = seed.ConfirmationId,
                    ConfirmationVersion = 1,
                    DraftId = seed.DraftId,
                    RunId = seed.RunId,
                    ActorUserId = seed.UserId,
                    BusinessType = "DemoBusinessOrder",
                    BusinessIdempotencyKey = $"ai-document:{seed.ConfirmationId:N}:1",
                    Status = AiDocumentExecutionStatus.Succeeded,
                    BusinessEntityId = orderId,
                    BusinessNo = orderNo,
                    BusinessStatus = ApprovalStatus.Draft.ToString(),
                    TraceId = executionId.ToString("N"),
                    OutboxMessageId = executionId.ToString("N"),
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow
                });
                context.DemoBusinessOrders.Add(new DemoBusinessOrder
                {
                    Id = orderId,
                    TenantId = seed.TenantId,
                    CreatedBy = seed.UserId,
                    OrderNo = orderNo,
                    Title = "P3 concurrency test",
                    CustomerName = "Test customer",
                    Amount = 10,
                    OwnerUserId = seed.UserId,
                    OwnerUserName = "p3-test",
                    ApprovalStatus = ApprovalStatus.Draft
                });
                context.OutboxMessages.Add(new OutboxMessage
                {
                    TenantId = seed.TenantId,
                    MessageId = executionId.ToString("N"),
                    Exchange = "permission-system.exchange",
                    RoutingKey = "ai.document.executed",
                    MessageType = "AiDocumentExecutedEvent",
                    Payload = "{}",
                    Status = "Pending",
                    NextRetryAt = DateTimeOffset.UtcNow
                });
                await unitOfWork.SaveChangesAsync(token);
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<SeedIds> CreateSeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var confirmationId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await context.Database.MigrateAsync();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Code = $"p3-{tenantId:N}",
            Name = "P3 test tenant",
            Status = TenantStatus.Active,
            StatusChangedAt = DateTimeOffset.UtcNow,
            InitializationStep = "Completed",
            InitializationProgress = 100,
            InitializedAt = DateTimeOffset.UtcNow
        });
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            UserName = $"p3-{tenantId:N}"[..32],
            NormalizedUserName = $"P3-{tenantId:N}"[..32],
            DisplayName = "P3 test user",
            PasswordHash = "test"
        });
        context.AiProviderConfigs.Add(new AiProviderConfig
        {
            Id = providerId,
            TenantId = tenantId,
            ProviderCode = "p3-test",
            ProviderName = "P3 test",
            BaseUrl = "https://example.invalid",
            ApiKeyEncrypted = "test",
            ModelName = "test",
            AllowedHostsJson = "[]"
        });
        context.AiConversations.Add(new AiConversation
        {
            Id = conversationId,
            TenantId = tenantId,
            UserId = userId,
            AgentCode = "assistant",
            AgentVersion = "1.0",
            Title = "P3 test",
            LastMessageAt = DateTimeOffset.UtcNow,
            RetentionUntil = DateTimeOffset.UtcNow.AddDays(30)
        });
        context.AiMessages.Add(new AiMessage
        {
            Id = messageId,
            TenantId = tenantId,
            ConversationId = conversationId,
            Role = AiMessageRole.User,
            Content = "Create an order",
            ContentDigest = "digest",
            Sequence = 1
        });
        context.AiRuns.Add(new AiRun
        {
            Id = runId,
            TenantId = tenantId,
            ConversationId = conversationId,
            RequestMessageId = messageId,
            ProviderConfigId = providerId,
            ActorUserId = userId,
            AgentCode = "assistant",
            AgentVersion = "1.0",
            PromptVersion = "1.0",
            ModelName = "test",
            Status = AiRunStatus.Completed,
            TraceId = "p3-test"
        });
        context.AiDocumentDrafts.Add(new AiDocumentDraft
        {
            Id = draftId,
            TenantId = tenantId,
            ConversationId = conversationId,
            RunId = runId,
            SourceInvocationId = "p3-test",
            ActorUserId = userId,
            BusinessType = "DemoBusinessOrder",
            HandlerVersion = "1.0",
            Status = AiDocumentDraftStatus.ReadyForConfirmation,
            PayloadJson = "{}",
            PayloadHash = new string('A', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        context.AiDocumentConfirmations.Add(new AiDocumentConfirmation
        {
            Id = confirmationId,
            TenantId = tenantId,
            DraftId = draftId,
            RunId = runId,
            ActorUserId = userId,
            DraftVersion = 1,
            ConfirmationVersion = 1,
            PayloadHash = new string('A', 64),
            HandlerVersion = "1.0",
            Status = AiDocumentConfirmationStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2)
        });
        await context.SaveChangesAsync();
        return new SeedIds(tenantId, userId, runId, draftId, confirmationId);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable(ConnectionEnvName)!)
            .Options;
        return new AppDbContext(options, new TestTenantContext(tenantId), new NullAuditContext());
    }

    private static async Task CleanupAsync(Guid tenantId)
    {
        await using var context = CreateContext(tenantId);
        await context.OutboxMessages.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.DemoBusinessOrders.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiDocumentExecutions.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiDocumentConfirmations.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiDocumentDraftValidations.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiDocumentDrafts.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiToolInvocations.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiUsageLogs.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiRuns.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiMessages.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiConversations.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.AiProviderConfigs.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Users.IgnoreQueryFilters().Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.IgnoreQueryFilters().Where(entity => entity.Id == tenantId).ExecuteDeleteAsync();
    }

    private sealed record SeedIds(Guid TenantId, Guid UserId, Guid RunId, Guid DraftId, Guid ConfirmationId);

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvName)))
            {
                Skip = $"Set {ConnectionEnvName} to run SQL Server integration tests.";
            }
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
        public string? Source => "Test";
        public bool IsResolved => true;
        public bool IsSuperAdmin => false;
        public bool IsSystemScopeActive => false;
        public bool IsHttpRequest => false;
        public void SetTenant(Guid tenantId, string source) { }
        public void MarkAsSuperAdmin(bool isSuperAdmin) { }
        public void MarkAsHttpRequest() { }
    }
}
