using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiDocumentExecutionRecoveryStore : IAiDocumentExecutionRecoveryStore
{
    private readonly AppDbContext _dbContext;

    public AiDocumentExecutionRecoveryStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AiDocumentExecution?> GetByBusinessIdempotencyKeyAsync(
        Guid tenantId,
        string businessIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeTracker.Clear();
        return _dbContext.AiDocumentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(entity =>
                entity.TenantId == tenantId &&
                entity.BusinessIdempotencyKey == businessIdempotencyKey,
                cancellationToken);
    }

    public async Task RecordFailureAsync(
        AiDocumentExecutionFailureRecord record,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeTracker.Clear();
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var existing = await _dbContext.AiDocumentExecutions
                .FirstOrDefaultAsync(entity =>
                    entity.TenantId == record.TenantId &&
                    entity.BusinessIdempotencyKey == record.BusinessIdempotencyKey,
                    cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var confirmation = await _dbContext.AiDocumentConfirmations
                .FirstOrDefaultAsync(entity =>
                    entity.Id == record.ConfirmationId &&
                    entity.TenantId == record.TenantId &&
                    entity.ActorUserId == record.ActorUserId &&
                    entity.DraftId == record.DraftId &&
                    entity.ConfirmationVersion == record.ConfirmationVersion,
                    cancellationToken);
            if (confirmation is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            confirmation.Status = AiDocumentConfirmationStatus.Consumed;
            confirmation.ConsumedAt ??= record.OccurredAt;
            confirmation.UpdatedBy = record.ActorUserId;
            _dbContext.AiDocumentExecutions.Add(new AiDocumentExecution
            {
                TenantId = record.TenantId,
                CreatedBy = record.ActorUserId,
                ConfirmationId = record.ConfirmationId,
                ConfirmationVersion = record.ConfirmationVersion,
                DraftId = record.DraftId,
                RunId = confirmation.RunId,
                ActorUserId = record.ActorUserId,
                BusinessType = record.BusinessType,
                BusinessIdempotencyKey = record.BusinessIdempotencyKey,
                Status = record.Status,
                TraceId = string.IsNullOrWhiteSpace(record.TraceId) ? Guid.NewGuid().ToString("N") : record.TraceId,
                ErrorCode = record.ErrorCode,
                ErrorSummary = record.ErrorSummary,
                StartedAt = record.OccurredAt,
                CompletedAt = record.OccurredAt
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
