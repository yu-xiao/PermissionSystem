using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.NumberRules;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.DemoApprovalOrders;

public sealed class DemoApprovalOrderService : IDemoApprovalOrderService
{
    private readonly IDataPermissionRepository<DemoApprovalOrder> _orderRepository;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly INumberGenerator _numberGenerator;
    private readonly IStateTransitionExecutor _stateTransitionExecutor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public DemoApprovalOrderService(
        IDataPermissionRepository<DemoApprovalOrder> orderRepository,
        IWorkflowEngine workflowEngine,
        INumberGenerator numberGenerator,
        IStateTransitionExecutor stateTransitionExecutor,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _workflowEngine = workflowEngine;
        _numberGenerator = numberGenerator;
        _stateTransitionExecutor = stateTransitionExecutor;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DemoApprovalOrderResponse>> GetPagedAsync(
        DemoApprovalOrderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = await _orderRepository.QueryVisibleAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.OrderNo.Contains(keyword) ||
                entity.Title.Contains(keyword) ||
                entity.ApplicantUserName.Contains(keyword));
        }

        if (request.ApprovalStatus.HasValue)
        {
            query = query.Where(entity => entity.ApprovalStatus == request.ApprovalStatus.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return PagedResult<DemoApprovalOrderResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<DemoApprovalOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ToResponse(await GetOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<DemoApprovalOrderResponse> CreateAsync(
        CreateDemoApprovalOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveRequiredTenantId(request.TenantId);
        var orderNo = await _numberGenerator.GenerateAsync(DemoApprovalOrderConstants.NumberRuleCode, cancellationToken);
        var visibleOrders = await _orderRepository.QueryVisibleAsync(cancellationToken);
        if (visibleOrders.Any(entity => entity.TenantId == tenantId && entity.OrderNo == orderNo))
        {
            throw new BusinessException(ErrorCode.Conflict, "Demo approval order no already exists.");
        }

        var userId = RequireUserId();
        var order = new DemoApprovalOrder
        {
            TenantId = tenantId,
            OrderNo = orderNo,
            Title = TrimRequired(request.Title, "Title is required."),
            Amount = request.Amount,
            DepartmentId = request.DepartmentId,
            ApplicantUserId = userId,
            ApplicantUserName = _currentUserService.Username ?? "Unknown",
            ApprovalStatus = ApprovalStatus.Draft
        };

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(order);
    }

    public async Task<DemoApprovalOrderResponse> UpdateAsync(
        Guid id,
        UpdateDemoApprovalOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(id, cancellationToken);
        EnsureEditable(order);

        order.Title = TrimRequired(request.Title, "Title is required.");
        order.Amount = request.Amount;
        order.DepartmentId = request.DepartmentId;

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(order);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(id, cancellationToken);
        if (order.ApprovalStatus != ApprovalStatus.Draft)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only draft demo approval orders can be deleted.");
        }

        _orderRepository.Remove(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<DemoApprovalOrderResponse> SubmitAsync(
        Guid id,
        SubmitDemoApprovalOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(id, cancellationToken);
        await _stateTransitionExecutor.ValidateTransitionAsync(
            DemoApprovalOrderConstants.BusinessType,
            order.Id.ToString(),
            order.ApprovalStatus.ToString(),
            "Submit",
            cancellationToken);

        await _workflowEngine.StartAsync(new StartWorkflowInstanceRequest
        {
            BusinessType = DemoApprovalOrderConstants.BusinessType,
            BusinessId = order.Id.ToString(),
            BusinessTitle = $"{order.OrderNo} {order.Title}",
            FormDataJson = BuildFormDataJson(order),
            Remark = request.Remark
        }, cancellationToken);

        return ToResponse(await GetOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<DemoApprovalOrderResponse> WithdrawAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(id, cancellationToken);
        if (order.ApprovalStatus != ApprovalStatus.Pending || !order.WorkflowInstanceId.HasValue)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only pending demo approval orders can be withdrawn.");
        }

        await _workflowEngine.WithdrawAsync(order.WorkflowInstanceId.Value, request, cancellationToken);
        return ToResponse(await GetOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<DemoApprovalOrderResponse> CancelAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(id, cancellationToken);
        await _stateTransitionExecutor.ExecuteTransitionAsync(
            DemoApprovalOrderConstants.BusinessType,
            order.Id.ToString(),
            "Cancel",
            request.Comment,
            cancellationToken);

        return ToResponse(await GetOrderOrThrowAsync(id, cancellationToken));
    }

    private async Task<DemoApprovalOrder> GetOrderOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetVisibleByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Demo approval order was not found.");
    }

    private void EnsureEditable(DemoApprovalOrder order)
    {
        if (order.ApprovalStatus is not (ApprovalStatus.Draft or ApprovalStatus.Rejected or ApprovalStatus.Withdrawn))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only draft, rejected or withdrawn demo approval orders can be edited.");
        }
    }

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId ?? _currentUserService.TenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private Guid ResolveRequiredTenantId(Guid? requestedTenantId)
    {
        return _tenantWriteResolver.ResolveTenantId(requestedTenantId);
    }

    private Guid RequireUserId()
    {
        return _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "User is not authenticated.");
    }

    private static string BuildFormDataJson(DemoApprovalOrder order)
    {
        return JsonSerializer.Serialize(new
        {
            amount = order.Amount,
            departmentId = order.DepartmentId,
            applicantUserId = order.ApplicantUserId,
            applicantUserName = order.ApplicantUserName
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static DemoApprovalOrderResponse ToResponse(DemoApprovalOrder entity)
    {
        return new DemoApprovalOrderResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            OrderNo = entity.OrderNo,
            Title = entity.Title,
            Amount = entity.Amount,
            DepartmentId = entity.DepartmentId,
            ApplicantUserId = entity.ApplicantUserId,
            ApplicantUserName = entity.ApplicantUserName,
            ApprovalStatus = entity.ApprovalStatus,
            WorkflowInstanceId = entity.WorkflowInstanceId,
            SubmittedAt = entity.SubmittedAt,
            SubmittedBy = entity.SubmittedBy,
            ApprovedAt = entity.ApprovedAt,
            RejectedAt = entity.RejectedAt,
            WithdrawnAt = entity.WithdrawnAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }
}
