using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Security;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Security;

public sealed class StepUpVerificationStore : IStepUpVerificationStore
{
    private readonly AppDbContext _dbContext;

    public StepUpVerificationStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> RegisterFailedAttemptAsync(
        Guid id,
        int maxAttempts,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.SensitiveOperationVerifications
            .Where(entity => entity.Id == id &&
                entity.ExpiresAt > now &&
                entity.LockedAt == null &&
                entity.VerifiedAt == null &&
                entity.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.FailedAttemptCount, entity => entity.FailedAttemptCount + 1)
                .SetProperty(
                    entity => entity.LockedAt,
                    entity => entity.FailedAttemptCount + 1 >= maxAttempts ? now : entity.LockedAt),
                cancellationToken);

        return affected == 1;
    }

    public async Task<bool> MarkVerifiedAsync(
        Guid id,
        string ticketHash,
        DateTimeOffset verifiedAt,
        DateTimeOffset ticketExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.SensitiveOperationVerifications
            .Where(entity => entity.Id == id &&
                entity.ExpiresAt > verifiedAt &&
                entity.LockedAt == null &&
                entity.VerifiedAt == null &&
                entity.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.TicketHash, ticketHash)
                .SetProperty(entity => entity.VerifiedAt, verifiedAt)
                .SetProperty(entity => entity.TicketExpiresAt, ticketExpiresAt),
                cancellationToken);

        return affected == 1;
    }

    public async Task<bool> TryConsumeTicketAsync(
        Guid tenantId,
        Guid userId,
        string sessionId,
        string operationCode,
        string ticketHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.SensitiveOperationVerifications
            .Where(entity => entity.TenantId == tenantId &&
                entity.UserId == userId &&
                entity.SessionId == sessionId &&
                entity.OperationCode == operationCode &&
                entity.TicketHash == ticketHash &&
                entity.TicketExpiresAt > now &&
                entity.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(entity => entity.UsedAt, now),
                cancellationToken);

        return affected == 1;
    }
}
