using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.NumberRules;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.PrintTemplates;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.DemoBusinessOrders;

public sealed class DemoBusinessOrderService : IDemoBusinessOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<DemoBusinessOrder> _orderRepository;
    private readonly IDataScopeService _dataScopeService;
    private readonly IDataPermissionFilter _dataPermissionFilter;
    private readonly INumberGenerator _numberGenerator;
    private readonly IStateTransitionExecutor _stateTransitionExecutor;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IExcelService _excelService;
    private readonly IFileService _fileService;
    private readonly IPrintTemplateService _printTemplateService;
    private readonly IOperationLogService _operationLogService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public DemoBusinessOrderService(
        IRepository<DemoBusinessOrder> orderRepository,
        IDataScopeService dataScopeService,
        IDataPermissionFilter dataPermissionFilter,
        INumberGenerator numberGenerator,
        IStateTransitionExecutor stateTransitionExecutor,
        IWorkflowEngine workflowEngine,
        IExcelService excelService,
        IFileService fileService,
        IPrintTemplateService printTemplateService,
        IOperationLogService operationLogService,
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _dataScopeService = dataScopeService;
        _dataPermissionFilter = dataPermissionFilter;
        _numberGenerator = numberGenerator;
        _stateTransitionExecutor = stateTransitionExecutor;
        _workflowEngine = workflowEngine;
        _excelService = excelService;
        _fileService = fileService;
        _printTemplateService = printTemplateService;
        _operationLogService = operationLogService;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DemoBusinessOrderResponse>> GetPagedAsync(
        DemoBusinessOrderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildVisibleQueryAsync(request, cancellationToken);
        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return PagedResult<DemoBusinessOrderResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<DemoBusinessOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ToResponse(await GetVisibleOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<DemoBusinessOrderResponse> CreateAsync(
        CreateDemoBusinessOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveRequiredTenantId(request.TenantId);
        var userId = RequireUserId();
        var orderNo = await _numberGenerator.GenerateAsync(DemoBusinessOrderConstants.NumberRuleCode, cancellationToken);
        if (_orderRepository.Query().Any(entity => entity.TenantId == tenantId && entity.OrderNo == orderNo))
        {
            throw new BusinessException(ErrorCode.Conflict, "Demo business order no already exists.");
        }

        var order = new DemoBusinessOrder
        {
            TenantId = tenantId,
            CreatedBy = userId,
            OrderNo = orderNo,
            Title = TrimRequired(request.Title, "Title is required."),
            CustomerName = TrimRequired(request.CustomerName, "Customer name is required."),
            Amount = EnsureNonNegative(request.Amount),
            DepartmentId = request.DepartmentId,
            OwnerUserId = userId,
            OwnerUserName = _currentUserService.Username ?? "Unknown",
            ApprovalStatus = ApprovalStatus.Draft
        };
        AppendChange(order, "Create", "Created demo business order.");

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(order);
    }

    public async Task<DemoBusinessOrderResponse> UpdateAsync(
        Guid id,
        UpdateDemoBusinessOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        EnsureEditable(order);

        var changes = BuildChangeDescription(order, request);
        order.Title = TrimRequired(request.Title, "Title is required.");
        order.CustomerName = TrimRequired(request.CustomerName, "Customer name is required.");
        order.Amount = EnsureNonNegative(request.Amount);
        order.DepartmentId = request.DepartmentId;
        order.UpdatedBy = _currentUserService.UserId;
        AppendChange(order, "Update", changes);

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(order);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        if (order.ApprovalStatus != ApprovalStatus.Draft)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only draft demo business orders can be deleted.");
        }

        AppendChange(order, "Delete", "Deleted draft demo business order.");
        _orderRepository.Remove(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<DemoBusinessOrderResponse> SubmitAsync(
        Guid id,
        SubmitDemoBusinessOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        await _stateTransitionExecutor.ValidateTransitionAsync(
            DemoBusinessOrderConstants.BusinessType,
            order.Id.ToString(),
            order.ApprovalStatus.ToString(),
            "Submit",
            cancellationToken);

        await _workflowEngine.StartAsync(new StartWorkflowInstanceRequest
        {
            BusinessType = DemoBusinessOrderConstants.BusinessType,
            BusinessId = order.Id.ToString(),
            BusinessTitle = $"{order.OrderNo} {order.Title}",
            FormDataJson = BuildFormDataJson(order),
            Remark = request.Remark
        }, cancellationToken);

        await NotifyOwnerInternalAsync(order, "Demo business order submitted.", cancellationToken);
        return ToResponse(await GetVisibleOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<DemoBusinessOrderResponse> WithdrawAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        if (order.ApprovalStatus != ApprovalStatus.Pending || !order.WorkflowInstanceId.HasValue)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only pending demo business orders can be withdrawn.");
        }

        await _workflowEngine.WithdrawAsync(order.WorkflowInstanceId.Value, request, cancellationToken);
        return ToResponse(await GetVisibleOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<DemoBusinessOrderResponse> CancelAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        await _stateTransitionExecutor.ExecuteTransitionAsync(
            DemoBusinessOrderConstants.BusinessType,
            order.Id.ToString(),
            "Cancel",
            request.Comment,
            cancellationToken);

        return ToResponse(await GetVisibleOrderOrThrowAsync(id, cancellationToken));
    }

    public async Task<byte[]> ExportAsync(DemoBusinessOrderQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = await BuildVisibleQueryAsync(request, cancellationToken);
        var rows = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new DemoBusinessOrderExportRow
            {
                OrderNo = entity.OrderNo,
                Title = entity.Title,
                CustomerName = entity.CustomerName,
                Amount = entity.Amount,
                OwnerUserName = entity.OwnerUserName,
                ApprovalStatus = entity.ApprovalStatus.ToString(),
                CreatedAt = entity.CreatedAt
            })
            .ToList();

        return await _excelService.ExportAsync(
            new ExportRequest<DemoBusinessOrderExportRow>
            {
                SheetName = "DemoBusinessOrders",
                Items = rows
            },
            cancellationToken);
    }

    public Task<byte[]> CreateImportTemplateAsync(CancellationToken cancellationToken = default)
    {
        return _excelService.CreateTemplateAsync<DemoBusinessOrderImportRow>("Demo Business Order Import", cancellationToken);
    }

    public async Task<ImportResult<DemoBusinessOrderImportRow>> ImportPreviewAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = await _excelService.ImportAsync<DemoBusinessOrderImportRow>(stream, cancellationToken);
        var errors = result.Errors.ToList();
        var validItems = new List<DemoBusinessOrderImportRow>();
        var rowNumber = 1;

        foreach (var item in result.Items)
        {
            rowNumber++;
            var hasError = false;
            if (item.Amount < 0)
            {
                errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    ColumnName = "Amount",
                    Message = "Amount cannot be negative.",
                    RawValue = item.Amount.ToString()
                });
                hasError = true;
            }

            if (!hasError)
            {
                validItems.Add(item);
            }
        }

        return new ImportResult<DemoBusinessOrderImportRow>
        {
            TotalRows = result.TotalRows,
            SuccessRows = validItems.Count,
            FailedRows = errors.Select(error => error.RowNumber).Distinct().Count(),
            Items = validItems,
            Errors = errors
        };
    }

    public async Task<IReadOnlyList<FileResourceResponse>> GetAttachmentsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _ = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        return await _fileService.GetByBusinessAsync(DemoBusinessOrderConstants.BusinessType, id, cancellationToken);
    }

    public async Task<FileResourceResponse> UploadAttachmentAsync(
        Guid id,
        Stream content,
        string originalName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        var file = await _fileService.UploadAsync(new UploadFileRequest
        {
            TenantId = order.TenantId,
            Content = content,
            OriginalName = originalName,
            ContentType = contentType,
            Size = size,
            BusinessType = DemoBusinessOrderConstants.BusinessType,
            BusinessId = order.Id
        }, cancellationToken);

        AppendChange(order, "UploadAttachment", $"Uploaded attachment {file.OriginalName}.");
        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    public Task<IReadOnlyList<PrintTemplateResponse>> GetPrintTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return _printTemplateService.GetByBusinessTypeAsync(DemoBusinessOrderConstants.BusinessType, cancellationToken);
    }

    public async Task<DemoBusinessOrderPrintResponse> RenderPrintAsync(
        Guid id,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        var renderResult = await _printTemplateService.RenderAsync(templateId, new PrintRenderRequest
        {
            BusinessId = order.Id.ToString(),
            Data = JsonSerializer.SerializeToElement(new
            {
                order.OrderNo,
                order.Title,
                order.CustomerName,
                order.Amount,
                order.OwnerUserName,
                ApprovalStatus = order.ApprovalStatus.ToString(),
                CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }, JsonOptions)
        }, cancellationToken);

        AppendChange(order, "Print", $"Rendered print template {renderResult.TemplateName}.");
        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DemoBusinessOrderPrintResponse
        {
            TemplateId = renderResult.TemplateId,
            TemplateName = renderResult.TemplateName,
            Html = renderResult.Html
        };
    }

    public async Task<PagedResult<OperationLogResponse>> GetOperationLogsAsync(
        Guid id,
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        return await _operationLogService.GetPagedAsync(new OperationLogQueryRequest
        {
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            Module = "DemoBusinessOrder",
            Keyword = order.Id.ToString()
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DemoBusinessOrderChangeHistoryResponse>> GetChangeHistoriesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        return DeserializeChanges(order.ChangeHistoryJson)
            .OrderByDescending(item => item.ChangedAt)
            .ToList();
    }

    public async Task NotifyOwnerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await GetVisibleOrderOrThrowAsync(id, cancellationToken);
        await NotifyOwnerInternalAsync(order, "Demo business order notification.", cancellationToken);
        AppendChange(order, "Notify", "Sent demo notification to owner.");
        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<IQueryable<DemoBusinessOrder>> BuildVisibleQueryAsync(
        DemoBusinessOrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = _orderRepository.Query();
        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        query = query.ApplyDataPermission(
            _dataPermissionFilter,
            dataScope,
            entity => entity.CreatedBy,
            entity => entity.DepartmentId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.OrderNo.Contains(keyword) ||
                entity.Title.Contains(keyword) ||
                entity.CustomerName.Contains(keyword) ||
                entity.OwnerUserName.Contains(keyword));
        }

        if (request.ApprovalStatus.HasValue)
        {
            query = query.Where(entity => entity.ApprovalStatus == request.ApprovalStatus.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(entity => entity.DepartmentId == request.DepartmentId.Value);
        }

        return query;
    }

    private async Task<DemoBusinessOrder> GetVisibleOrderOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var query = await BuildVisibleQueryAsync(new DemoBusinessOrderQueryRequest(), cancellationToken);
        return query.FirstOrDefault(entity => entity.Id == id)
            ?? throw new BusinessException(ErrorCode.NotFound, "Demo business order was not found.");
    }

    private void EnsureEditable(DemoBusinessOrder order)
    {
        if (order.ApprovalStatus is not (ApprovalStatus.Draft or ApprovalStatus.Rejected or ApprovalStatus.Withdrawn))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only draft, rejected or withdrawn demo business orders can be edited.");
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

    private async Task NotifyOwnerInternalAsync(
        DemoBusinessOrder order,
        string content,
        CancellationToken cancellationToken)
    {
        await _notificationService.SendSystemNotificationAsync(new SendSystemNotificationRequest
        {
            TenantId = order.TenantId,
            RecipientUserIds = [order.OwnerUserId],
            Type = NotificationTypes.Approval,
            Title = $"Demo business order {order.OrderNo}",
            Content = content,
            LinkUrl = $"/demo/business-order?keyword={Uri.EscapeDataString(order.OrderNo)}",
            Payload = JsonSerializer.Serialize(new
            {
                businessType = DemoBusinessOrderConstants.BusinessType,
                businessId = order.Id,
                order.OrderNo
            }, JsonOptions)
        }, cancellationToken);
    }

    private void AppendChange(DemoBusinessOrder order, string action, string description)
    {
        var changes = DeserializeChanges(order.ChangeHistoryJson).ToList();
        changes.Add(new DemoBusinessOrderChangeHistoryResponse
        {
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = _currentUserService.UserId,
            ChangedByName = _currentUserService.Username,
            Action = action,
            Description = description
        });

        order.ChangeHistoryJson = JsonSerializer.Serialize(changes, JsonOptions);
    }

    private static IReadOnlyList<DemoBusinessOrderChangeHistoryResponse> DeserializeChanges(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<DemoBusinessOrderChangeHistoryResponse>>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildFormDataJson(DemoBusinessOrder order)
    {
        return JsonSerializer.Serialize(new
        {
            amount = order.Amount,
            departmentId = order.DepartmentId,
            ownerUserId = order.OwnerUserId,
            ownerUserName = order.OwnerUserName,
            customerName = order.CustomerName
        }, JsonOptions);
    }

    private static string BuildChangeDescription(
        DemoBusinessOrder order,
        UpdateDemoBusinessOrderRequest request)
    {
        var changes = new List<string>();
        AddChange(changes, "Title", order.Title, request.Title);
        AddChange(changes, "CustomerName", order.CustomerName, request.CustomerName);
        AddChange(changes, "Amount", order.Amount, request.Amount);
        AddChange(changes, "DepartmentId", order.DepartmentId, request.DepartmentId);
        return changes.Count == 0 ? "No business fields changed." : string.Join("; ", changes);
    }

    private static void AddChange<T>(ICollection<string> changes, string field, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            changes.Add($"{field}: {oldValue} -> {newValue}");
        }
    }

    private static DemoBusinessOrderResponse ToResponse(DemoBusinessOrder entity)
    {
        return new DemoBusinessOrderResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            OrderNo = entity.OrderNo,
            Title = entity.Title,
            CustomerName = entity.CustomerName,
            Amount = entity.Amount,
            DepartmentId = entity.DepartmentId,
            OwnerUserId = entity.OwnerUserId,
            OwnerUserName = entity.OwnerUserName,
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

    private static decimal EnsureNonNegative(decimal value)
    {
        if (value < 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Amount cannot be negative.");
        }

        return value;
    }
}
