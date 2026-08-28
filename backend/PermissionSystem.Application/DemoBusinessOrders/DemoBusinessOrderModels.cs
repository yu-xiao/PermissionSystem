using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.PrintTemplates;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.DemoBusinessOrders;

public static class DemoBusinessOrderConstants
{
    public const string BusinessType = "DemoBusinessOrder";

    public const string NumberRuleCode = "DemoBusinessOrder";
}

public sealed class DemoBusinessOrderQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public ApprovalStatus? ApprovalStatus { get; init; }

    public Guid? DepartmentId { get; init; }
}

public sealed class CreateDemoBusinessOrderRequest
{
    public Guid? TenantId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DepartmentId { get; init; }
}

public sealed class UpdateDemoBusinessOrderRequest
{
    public string Title { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DepartmentId { get; init; }
}

public sealed class SubmitDemoBusinessOrderRequest
{
    public string? Remark { get; init; }
}

public sealed class DemoBusinessOrderResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid OwnerUserId { get; init; }

    public string OwnerUserName { get; init; } = string.Empty;

    public ApprovalStatus ApprovalStatus { get; init; }

    public Guid? WorkflowInstanceId { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public Guid? SubmittedBy { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    public DateTimeOffset? RejectedAt { get; init; }

    public DateTimeOffset? WithdrawnAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class DemoBusinessOrderChangeHistoryResponse
{
    public DateTimeOffset ChangedAt { get; init; }

    public Guid? ChangedBy { get; init; }

    public string? ChangedByName { get; init; }

    public string Action { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public sealed class DemoBusinessOrderPrintResponse
{
    public Guid TemplateId { get; init; }

    public string TemplateName { get; init; } = string.Empty;

    public string Html { get; init; } = string.Empty;
}

public sealed class DemoBusinessOrderExportRow
{
    [ExcelColumn("Order No", Order = 1)]
    public string OrderNo { get; set; } = string.Empty;

    [ExcelColumn("Title", Order = 2)]
    public string Title { get; set; } = string.Empty;

    [ExcelColumn("Customer Name", Order = 3)]
    public string CustomerName { get; set; } = string.Empty;

    [ExcelColumn("Amount", Order = 4)]
    public decimal Amount { get; set; }

    [ExcelColumn("Owner", Order = 5)]
    public string OwnerUserName { get; set; } = string.Empty;

    [ExcelColumn("Approval Status", Order = 6)]
    public string ApprovalStatus { get; set; } = string.Empty;

    [ExcelColumn("Created At", Order = 7)]
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DemoBusinessOrderImportRow
{
    [ExcelColumn("Title", Order = 1, Required = true)]
    public string Title { get; set; } = string.Empty;

    [ExcelColumn("Customer Name", Order = 2, Required = true)]
    public string CustomerName { get; set; } = string.Empty;

    [ExcelColumn("Amount", Order = 3, Required = true)]
    public decimal Amount { get; set; }
}

public interface IDemoBusinessOrderService
{
    Task<PagedResult<DemoBusinessOrderResponse>> GetPagedAsync(
        DemoBusinessOrderQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderResponse> CreateAsync(
        CreateDemoBusinessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderResponse> UpdateAsync(
        Guid id,
        UpdateDemoBusinessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderResponse> SubmitAsync(
        Guid id,
        SubmitDemoBusinessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderResponse> WithdrawAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderResponse> CancelAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportAsync(DemoBusinessOrderQueryRequest request, CancellationToken cancellationToken = default);

    Task<byte[]> CreateImportTemplateAsync(CancellationToken cancellationToken = default);

    Task<ImportResult<DemoBusinessOrderImportRow>> ImportPreviewAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileResourceResponse>> GetAttachmentsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FileResourceResponse> UploadAttachmentAsync(
        Guid id,
        Stream content,
        string originalName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintTemplateResponse>> GetPrintTemplatesAsync(CancellationToken cancellationToken = default);

    Task<DemoBusinessOrderPrintResponse> RenderPrintAsync(
        Guid id,
        Guid templateId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OperationLogResponse>> GetOperationLogsAsync(
        Guid id,
        PaginationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DemoBusinessOrderChangeHistoryResponse>> GetChangeHistoriesAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task NotifyOwnerAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IDemoBusinessOrderValidator
{
    Task EnsureDepartmentAvailableAsync(
        Guid? departmentId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
