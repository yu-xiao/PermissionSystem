using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class AiToolService : IAiToolService
{
    private static readonly IReadOnlyList<AiDatasetDescriptor> P0Datasets =
    [
        new()
        {
            Key = "platform-capabilities",
            Name = "Platform capabilities",
            Description = "Non-sensitive metadata describing the PermissionSystem platform.",
            DataClassification = "Public"
        }
    ];

    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly ITraceContextAccessor _traceContextAccessor;

    public AiToolService(
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        ITraceContextAccessor traceContextAccessor)
    {
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _traceContextAccessor = traceContextAccessor;
    }

    public Task<AiToolResult<IReadOnlyList<AiDatasetDescriptor>>> ListDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAuthorized();

        return Task.FromResult(new AiToolResult<IReadOnlyList<AiDatasetDescriptor>>
        {
            Data = P0Datasets,
            Source = "permission-system:ai-tool-catalog",
            QueriedAt = DateTimeOffset.UtcNow,
            IsComplete = true,
            TraceId = _traceContextAccessor.TraceId
        });
    }

    private void EnsureAuthorized()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "Authentication is required.");
        }

        if (!_currentUserService.TenantId.HasValue ||
            !_tenantContext.TenantId.HasValue ||
            _currentUserService.TenantId.Value != _tenantContext.TenantId.Value)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The tenant context is invalid.");
        }

        if (!_currentUserService.HasPermission(AiCenterConstants.McpDatasetQueryPermission))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The current user cannot query MCP datasets.");
        }
    }
}
