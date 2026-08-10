using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.PrintTemplates;

public sealed class PrintTemplateService : IPrintTemplateService
{
    private readonly IRepository<PrintTemplate> _templateRepository;
    private readonly IRepository<PrintRecord> _recordRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public PrintTemplateService(
        IRepository<PrintTemplate> templateRepository,
        IRepository<PrintRecord> recordRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _recordRepository = recordRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<PrintTemplateResponse>> GetPagedAsync(
        PrintTemplateQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _templateRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.TemplateCode.Contains(keyword) ||
                entity.TemplateName.Contains(keyword) ||
                entity.BusinessType.Contains(keyword) ||
                (entity.Remark != null && entity.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (!string.IsNullOrWhiteSpace(request.TemplateType))
        {
            var templateType = request.TemplateType.Trim();
            query = query.Where(entity => entity.TemplateType == templateType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.BusinessType)
            .ThenByDescending(entity => entity.IsDefault)
            .ThenBy(entity => entity.TemplateCode)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<PrintTemplateResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<PrintTemplateResponse> CreateAsync(
        CreatePrintTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var templateCode = TrimRequired(request.TemplateCode, "Template code is required.");
        if (_templateRepository.Query().Any(entity => entity.TemplateCode == templateCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "Template code already exists.");
        }

        var template = new PrintTemplate
        {
            TemplateCode = templateCode,
            TemplateName = TrimRequired(request.TemplateName, "Template name is required."),
            BusinessType = TrimRequired(request.BusinessType, "Business type is required."),
            TemplateType = TrimRequired(request.TemplateType, "Template type is required."),
            ContentHtml = NormalizeContent(request.ContentHtml),
            ContentJson = NormalizeOptional(request.ContentJson),
            PaperSize = TrimRequired(request.PaperSize, "Paper size is required."),
            Orientation = TrimRequired(request.Orientation, "Orientation is required."),
            IsDefault = request.IsDefault,
            IsEnabled = request.IsEnabled,
            Version = Math.Max(1, request.Version),
            Remark = NormalizeOptional(request.Remark)
        };

        if (template.IsDefault)
        {
            ClearDefault(template.BusinessType, template.TemplateType);
        }

        await _templateRepository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task<PrintTemplateResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateOrThrowAsync(id, cancellationToken);
        return ToResponse(template);
    }

    public async Task<PrintTemplateResponse> UpdateAsync(
        Guid id,
        UpdatePrintTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(template, request.ConcurrencyToken);
        template.TemplateName = TrimRequired(request.TemplateName, "Template name is required.");
        template.BusinessType = TrimRequired(request.BusinessType, "Business type is required.");
        template.TemplateType = TrimRequired(request.TemplateType, "Template type is required.");
        template.ContentHtml = NormalizeContent(request.ContentHtml);
        template.ContentJson = NormalizeOptional(request.ContentJson);
        template.PaperSize = TrimRequired(request.PaperSize, "Paper size is required.");
        template.Orientation = TrimRequired(request.Orientation, "Orientation is required.");
        template.IsDefault = request.IsDefault;
        template.IsEnabled = request.IsEnabled;
        template.Version = Math.Max(1, request.Version);
        template.Remark = NormalizeOptional(request.Remark);

        if (template.IsDefault)
        {
            ClearDefault(template.BusinessType, template.TemplateType, template.Id);
        }

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateOrThrowAsync(id, cancellationToken);
        _templateRepository.Remove(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<PrintTemplateResponse>> GetByBusinessTypeAsync(
        string businessType,
        CancellationToken cancellationToken = default)
    {
        var normalizedBusinessType = TrimRequired(businessType, "Business type is required.");
        var templates = _templateRepository.Query()
            .Where(entity => entity.BusinessType == normalizedBusinessType && entity.IsEnabled)
            .OrderByDescending(entity => entity.IsDefault)
            .ThenBy(entity => entity.TemplateName)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<PrintTemplateResponse>>(templates);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateOrThrowAsync(id, cancellationToken);
        ClearDefault(template.BusinessType, template.TemplateType, template.Id);
        template.IsDefault = true;
        template.IsEnabled = true;
        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PrintRenderResponse> PreviewAsync(
        Guid id,
        PrintRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateOrThrowAsync(id, cancellationToken);
        return BuildRenderResponse(template, request);
    }

    public async Task<PrintRenderResponse> RenderAsync(
        Guid id,
        PrintRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateOrThrowAsync(id, cancellationToken);
        if (!template.IsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "Print template is disabled.");
        }

        var businessId = NormalizeOptional(request.BusinessId) ?? "manual-preview";
        var response = BuildRenderResponse(template, request);
        await _recordRepository.AddAsync(new PrintRecord
        {
            TenantId = template.TenantId,
            TemplateId = template.Id,
            BusinessType = template.BusinessType,
            BusinessId = businessId,
            PrintUserId = _currentUserService.UserId,
            PrintUserName = _currentUserService.Username,
            PrintedAt = DateTimeOffset.UtcNow,
            PrintCount = 1
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public Task<PagedResult<PrintRecordResponse>> GetRecordsAsync(
        PrintRecordQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _recordRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessId))
        {
            var businessId = request.BusinessId.Trim();
            query = query.Where(entity => entity.BusinessId == businessId);
        }

        if (request.TemplateId.HasValue)
        {
            query = query.Where(entity => entity.TemplateId == request.TemplateId.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.PrintedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<PrintRecordResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    private async Task<PrintTemplate> GetTemplateOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _templateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Print template was not found.");
    }

    private void ClearDefault(string businessType, string templateType, Guid? exceptId = null)
    {
        foreach (var template in _templateRepository.Query()
            .Where(entity => entity.BusinessType == businessType &&
                entity.TemplateType == templateType &&
                entity.IsDefault &&
                (!exceptId.HasValue || entity.Id != exceptId.Value))
            .ToList())
        {
            template.IsDefault = false;
            _templateRepository.Update(template);
        }
    }

    private static PrintRenderResponse BuildRenderResponse(PrintTemplate template, PrintRenderRequest request)
    {
        return new PrintRenderResponse
        {
            TemplateId = template.Id,
            TemplateCode = template.TemplateCode,
            TemplateName = template.TemplateName,
            Html = PrintTemplateRenderer.Render(template.ContentHtml, request.Data)
        };
    }

    private static PrintTemplateResponse ToResponse(PrintTemplate template)
    {
        return new PrintTemplateResponse
        {
            Id = template.Id,
            TenantId = template.TenantId,
            TemplateCode = template.TemplateCode,
            TemplateName = template.TemplateName,
            BusinessType = template.BusinessType,
            TemplateType = template.TemplateType,
            ContentHtml = template.ContentHtml,
            ContentJson = template.ContentJson,
            PaperSize = template.PaperSize,
            Orientation = template.Orientation,
            IsDefault = template.IsDefault,
            IsEnabled = template.IsEnabled,
            Version = template.Version,
            Remark = template.Remark,
            CreatedAt = template.CreatedAt,
            ConcurrencyToken = template.RowVersion
        };
    }

    private static PrintRecordResponse ToResponse(PrintRecord record)
    {
        return new PrintRecordResponse
        {
            Id = record.Id,
            TenantId = record.TenantId,
            TemplateId = record.TemplateId,
            BusinessType = record.BusinessType,
            BusinessId = record.BusinessId,
            PrintUserId = record.PrintUserId,
            PrintUserName = record.PrintUserName,
            PrintedAt = record.PrintedAt,
            PrintCount = record.PrintCount
        };
    }

    private static string NormalizeContent(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<h1>{{OrderNo}}</h1><p>Created at: {{CreatedAt}}</p>"
            : value.Trim();
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
