using PermissionSystem.Application.Reports;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class DisabledReadOnlyReportQueryService : IReadOnlyReportQueryService
{
    public Task<ReportQueryResponse> QueryAsync(
        Guid id,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new BusinessException(ErrorCode.Forbidden, "The report dataset AI tool is disabled in this host.");
    }
}
