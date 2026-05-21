using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.OperationLogs;

public sealed class OperationLogService : IOperationLogService
{
    private readonly IRepository<OperationLog> _operationLogRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public OperationLogService(
        IRepository<OperationLog> operationLogRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _operationLogRepository = operationLogRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<OperationLogResponse>> GetPagedAsync(
        OperationLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(_operationLogRepository.Query(), request);

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<OperationLogResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<OperationLogDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _operationLogRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Operation log was not found.");

        var tenantId = ResolveTenantId(null);
        if (tenantId.HasValue && entity.TenantId != tenantId.Value)
        {
            throw new BusinessException(ErrorCode.NotFound, "Operation log was not found.");
        }

        return ToDetailResponse(entity);
    }

    public async Task CreateAsync(CreateOperationLogRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new OperationLog
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            UserName = request.UserName,
            Module = TrimOrDefault(request.Module, "Unknown"),
            Action = TrimOrDefault(request.Action, "Unknown"),
            Method = TrimOrDefault(request.Method, request.RequestMethod),
            RequestPath = request.RequestPath,
            RequestMethod = TrimOrDefault(request.RequestMethod, "UNKNOWN"),
            RequestBody = request.RequestBody,
            ResponseBody = request.ResponseBody,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            StatusCode = request.StatusCode,
            ElapsedMilliseconds = request.ElapsedMilliseconds,
            TraceId = request.TraceId
        };

        await _operationLogRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<OperationLog> ApplyQuery(
        IQueryable<OperationLog> query,
        OperationLogQueryRequest request)
    {
        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId.HasValue)
        {
            query = query.Where(entity => entity.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                (entity.UserName != null && entity.UserName.Contains(keyword)) ||
                entity.Module.Contains(keyword) ||
                entity.Action.Contains(keyword) ||
                (entity.RequestPath != null && entity.RequestPath.Contains(keyword)) ||
                (entity.TraceId != null && entity.TraceId.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var userName = request.UserName.Trim();
            query = query.Where(entity => entity.UserName != null && entity.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            var module = request.Module.Trim();
            query = query.Where(entity => entity.Module.Contains(module));
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(entity => entity.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(request.RequestMethod))
        {
            var requestMethod = request.RequestMethod.Trim().ToUpperInvariant();
            query = query.Where(entity => entity.RequestMethod == requestMethod);
        }

        if (request.StatusCode.HasValue)
        {
            query = query.Where(entity => entity.StatusCode == request.StatusCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.TraceId))
        {
            var traceId = request.TraceId.Trim();
            query = query.Where(entity => entity.TraceId != null && entity.TraceId.Contains(traceId));
        }

        if (request.StartTime.HasValue)
        {
            query = query.Where(entity => entity.CreatedAt >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(entity => entity.CreatedAt <= request.EndTime.Value);
        }

        return query;
    }

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private static OperationLogResponse ToResponse(OperationLog entity)
    {
        return new OperationLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            UserName = entity.UserName,
            Module = entity.Module,
            Action = entity.Action,
            Method = entity.Method,
            RequestPath = entity.RequestPath,
            RequestMethod = entity.RequestMethod,
            IpAddress = entity.IpAddress,
            UserAgent = entity.UserAgent,
            StatusCode = entity.StatusCode,
            ElapsedMilliseconds = entity.ElapsedMilliseconds,
            TraceId = entity.TraceId,
            CreatedAt = entity.CreatedAt
        };
    }

    private static OperationLogDetailResponse ToDetailResponse(OperationLog entity)
    {
        return new OperationLogDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            UserName = entity.UserName,
            Module = entity.Module,
            Action = entity.Action,
            Method = entity.Method,
            RequestPath = entity.RequestPath,
            RequestMethod = entity.RequestMethod,
            RequestBody = entity.RequestBody,
            ResponseBody = entity.ResponseBody,
            IpAddress = entity.IpAddress,
            UserAgent = entity.UserAgent,
            StatusCode = entity.StatusCode,
            ElapsedMilliseconds = entity.ElapsedMilliseconds,
            TraceId = entity.TraceId,
            CreatedAt = entity.CreatedAt
        };
    }

    private static string TrimOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
