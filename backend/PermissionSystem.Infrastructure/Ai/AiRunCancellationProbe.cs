using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiRunCancellationProbe : IAiRunCancellationProbe
{
    private readonly AppDbContext _dbContext;

    public AiRunCancellationProbe(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsCancellationRequestedAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AiRuns
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == runId && entity.CancellationRequestedAt != null, cancellationToken);
    }
}
