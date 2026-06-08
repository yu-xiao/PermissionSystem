using System.Text.Json;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.PrintTemplates;

public sealed class PrintTemplateQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? BusinessType { get; init; }

    public string? TemplateType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreatePrintTemplateRequest
{
    public string TemplateCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public string BusinessType { get; init; } = string.Empty;

    public string TemplateType { get; init; } = "Document";

    public string ContentHtml { get; init; } = string.Empty;

    public string? ContentJson { get; init; }

    public string PaperSize { get; init; } = "A4";

    public string Orientation { get; init; } = "Portrait";

    public bool IsDefault { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int Version { get; init; } = 1;

    public string? Remark { get; init; }
}

public sealed class UpdatePrintTemplateRequest
{
    public string TemplateName { get; init; } = string.Empty;

    public string BusinessType { get; init; } = string.Empty;

    public string TemplateType { get; init; } = "Document";

    public string ContentHtml { get; init; } = string.Empty;

    public string? ContentJson { get; init; }

    public string PaperSize { get; init; } = "A4";

    public string Orientation { get; init; } = "Portrait";

    public bool IsDefault { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int Version { get; init; } = 1;

    public string? Remark { get; init; }
}

public sealed class PrintTemplateResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string TemplateCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public string BusinessType { get; init; } = string.Empty;

    public string TemplateType { get; init; } = string.Empty;

    public string ContentHtml { get; init; } = string.Empty;

    public string? ContentJson { get; init; }

    public string PaperSize { get; init; } = string.Empty;

    public string Orientation { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public bool IsEnabled { get; init; }

    public int Version { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PrintRenderRequest
{
    public string BusinessId { get; init; } = string.Empty;

    public JsonElement? Data { get; init; }
}

public sealed class PrintRenderResponse
{
    public Guid TemplateId { get; init; }

    public string TemplateCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public string Html { get; init; } = string.Empty;
}

public sealed class PrintRecordQueryRequest : PaginationRequest
{
    public string? BusinessType { get; init; }

    public string? BusinessId { get; init; }

    public Guid? TemplateId { get; init; }
}

public sealed class PrintRecordResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid TemplateId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public Guid? PrintUserId { get; init; }

    public string? PrintUserName { get; init; }

    public DateTimeOffset PrintedAt { get; init; }

    public int PrintCount { get; init; }
}

public interface IPrintTemplateService
{
    Task<PagedResult<PrintTemplateResponse>> GetPagedAsync(
        PrintTemplateQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PrintTemplateResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PrintTemplateResponse> CreateAsync(CreatePrintTemplateRequest request, CancellationToken cancellationToken = default);

    Task<PrintTemplateResponse> UpdateAsync(Guid id, UpdatePrintTemplateRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintTemplateResponse>> GetByBusinessTypeAsync(string businessType, CancellationToken cancellationToken = default);

    Task SetDefaultAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PrintRenderResponse> PreviewAsync(Guid id, PrintRenderRequest request, CancellationToken cancellationToken = default);

    Task<PrintRenderResponse> RenderAsync(Guid id, PrintRenderRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<PrintRecordResponse>> GetRecordsAsync(
        PrintRecordQueryRequest request,
        CancellationToken cancellationToken = default);
}
