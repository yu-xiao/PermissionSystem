using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiRetentionHostedService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiRetentionHostedService> _logger;

    public AiRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AiRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "AI retention cleanup failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AiCenterOptions>>().Value;
        var now = DateTimeOffset.UtcNow;
        var contentCutoff = now.AddDays(-options.ConversationRetentionDays);
        var auditCutoff = now.AddDays(-options.AuditRetentionDays);
        var draftCutoff = now.AddDays(-options.DraftRetentionDays);
        const string expiredContent = "[expired]";

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sanitizedMessages = await dbContext.AiMessages
            .IgnoreQueryFilters()
            .Where(entity => entity.CreatedAt < contentCutoff && entity.Content != expiredContent)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.Content, expiredContent)
                .SetProperty(entity => entity.ContentDigest, "EXPIRED")
                .SetProperty(entity => entity.TokenCount, (int?)null), cancellationToken);
        var sanitizedConversations = await dbContext.AiConversations
            .IgnoreQueryFilters()
            .Where(entity => entity.CreatedAt < contentCutoff && entity.Title != "历史会话")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.Title, "历史会话"), cancellationToken);

        var expiredRunIds = dbContext.AiRuns
            .IgnoreQueryFilters()
            .Where(entity =>
                entity.CreatedAt < auditCutoff &&
                entity.Status != AiRunStatus.Pending &&
                entity.Status != AiRunStatus.Running)
            .Select(entity => entity.Id);
        var expiredExecutionIds = dbContext.AiDocumentExecutions
            .IgnoreQueryFilters()
            .Where(entity =>
                expiredRunIds.Contains(entity.RunId) ||
                (entity.CreatedAt < auditCutoff && entity.Status != AiDocumentExecutionStatus.Executing))
            .Select(entity => entity.Id);
        var deletedExecutions = await dbContext.AiDocumentExecutions
            .IgnoreQueryFilters()
            .Where(entity => expiredExecutionIds.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var expiredDraftIds = dbContext.AiDocumentDrafts
            .IgnoreQueryFilters()
            .Where(entity =>
                expiredRunIds.Contains(entity.RunId) ||
                (entity.CreatedAt < draftCutoff && !dbContext.AiDocumentExecutions
                    .IgnoreQueryFilters()
                    .Any(execution => execution.DraftId == entity.Id)))
            .Select(entity => entity.Id);
        var deletedConfirmations = await dbContext.AiDocumentConfirmations
            .IgnoreQueryFilters()
            .Where(entity => expiredDraftIds.Contains(entity.DraftId))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedDraftValidations = await dbContext.AiDocumentDraftValidations
            .IgnoreQueryFilters()
            .Where(entity => expiredDraftIds.Contains(entity.DraftId))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedDrafts = await dbContext.AiDocumentDrafts
            .IgnoreQueryFilters()
            .Where(entity => expiredDraftIds.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedToolInvocations = await dbContext.AiToolInvocations
            .IgnoreQueryFilters()
            .Where(entity => expiredRunIds.Contains(entity.RunId))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedUsageLogs = await dbContext.AiUsageLogs
            .IgnoreQueryFilters()
            .Where(entity => expiredRunIds.Contains(entity.RunId))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedRuns = await dbContext.AiRuns
            .IgnoreQueryFilters()
            .Where(entity => expiredRunIds.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedMessages = await dbContext.AiMessages
            .IgnoreQueryFilters()
            .Where(message =>
                message.CreatedAt < auditCutoff &&
                !dbContext.AiRuns.IgnoreQueryFilters().Any(run =>
                    run.RequestMessageId == message.Id || run.ResponseMessageId == message.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var deletedConversations = await dbContext.AiConversations
            .IgnoreQueryFilters()
            .Where(conversation =>
                conversation.LastMessageAt < auditCutoff &&
                !dbContext.AiMessages.IgnoreQueryFilters().Any(message =>
                    message.ConversationId == conversation.Id) &&
                !dbContext.AiRuns.IgnoreQueryFilters().Any(run =>
                    run.ConversationId == conversation.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "AI retention cleanup completed. SanitizedMessages={SanitizedMessages}, SanitizedConversations={SanitizedConversations}, DeletedExecutions={DeletedExecutions}, DeletedConfirmations={DeletedConfirmations}, DeletedDraftValidations={DeletedDraftValidations}, DeletedDrafts={DeletedDrafts}, DeletedToolInvocations={DeletedToolInvocations}, DeletedUsageLogs={DeletedUsageLogs}, DeletedRuns={DeletedRuns}, DeletedMessages={DeletedMessages}, DeletedConversations={DeletedConversations}.",
            sanitizedMessages,
            sanitizedConversations,
            deletedExecutions,
            deletedConfirmations,
            deletedDraftValidations,
            deletedDrafts,
            deletedToolInvocations,
            deletedUsageLogs,
            deletedRuns,
            deletedMessages,
            deletedConversations);
    }
}
