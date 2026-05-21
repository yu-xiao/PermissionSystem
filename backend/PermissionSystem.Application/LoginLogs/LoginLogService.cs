using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.LoginLogs;

public sealed class LoginLogService : ILoginLogService
{
    private readonly IRepository<LoginLog> _loginLogRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public LoginLogService(
        IRepository<LoginLog> loginLogRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _loginLogRepository = loginLogRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<LoginLogResponse>> GetPagedAsync(
        LoginLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(_loginLogRepository.Query(), request);

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<LoginLogResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<LoginLogResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _loginLogRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Login log was not found.");

        var tenantId = ResolveTenantId(null);
        if (tenantId.HasValue && entity.TenantId != tenantId.Value)
        {
            throw new BusinessException(ErrorCode.NotFound, "Login log was not found.");
        }

        return ToResponse(entity);
    }

    public async Task CreateAsync(CreateLoginLogRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new LoginLog
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? "unknown" : request.UserName.Trim(),
            LoginType = string.IsNullOrWhiteSpace(request.LoginType) ? "password" : request.LoginType.Trim(),
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            LoginResult = string.IsNullOrWhiteSpace(request.LoginResult) ? "Failed" : request.LoginResult.Trim(),
            FailureReason = request.FailureReason,
            TraceId = request.TraceId
        };

        await _loginLogRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<LoginLog> ApplyQuery(IQueryable<LoginLog> query, LoginLogQueryRequest request)
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
                entity.UserName.Contains(keyword) ||
                (entity.IpAddress != null && entity.IpAddress.Contains(keyword)) ||
                (entity.TraceId != null && entity.TraceId.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var userName = request.UserName.Trim();
            query = query.Where(entity => entity.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(request.LoginType))
        {
            var loginType = request.LoginType.Trim();
            query = query.Where(entity => entity.LoginType == loginType);
        }

        if (!string.IsNullOrWhiteSpace(request.LoginResult))
        {
            var loginResult = request.LoginResult.Trim();
            query = query.Where(entity => entity.LoginResult == loginResult);
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

    private static LoginLogResponse ToResponse(LoginLog entity)
    {
        return new LoginLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            UserName = entity.UserName,
            LoginType = entity.LoginType,
            IpAddress = entity.IpAddress,
            UserAgent = entity.UserAgent,
            LoginResult = entity.LoginResult,
            FailureReason = entity.FailureReason,
            TraceId = entity.TraceId,
            CreatedAt = entity.CreatedAt
        };
    }
}
