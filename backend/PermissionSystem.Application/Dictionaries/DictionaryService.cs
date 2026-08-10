using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Dictionaries;

public sealed class DictionaryService : IDictionaryService
{
    private const string EnabledStatus = "Enabled";
    private const string DisabledStatus = "Disabled";
    private static readonly TimeSpan DictionaryCacheTtl = TimeSpan.FromMinutes(30);

    private readonly IRepository<DictionaryType> _typeRepository;
    private readonly IRepository<DictionaryItem> _itemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantWriteResolver _tenantWriteResolver;

    public DictionaryService(
        IRepository<DictionaryType> typeRepository,
        IRepository<DictionaryItem> itemRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ITenantContext tenantContext,
        ITenantWriteResolver tenantWriteResolver)
    {
        _typeRepository = typeRepository;
        _itemRepository = itemRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _tenantContext = tenantContext;
        _tenantWriteResolver = tenantWriteResolver;
    }

    public Task<PagedResult<DictionaryTypeResponse>> GetTypesPagedAsync(
        DictionaryTypeQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _typeRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Code.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeStatus(request.Status);
            query = query.Where(entity => entity.Status == status);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToTypeResponse)
            .ToList();

        return Task.FromResult(PagedResult<DictionaryTypeResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<DictionaryTypeResponse> CreateTypeAsync(
        CreateDictionaryTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Dictionary type code is required.");
        ValidateRequired(request.Name, "Dictionary type name is required.");

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = request.Code.Trim();
        if (_typeRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Dictionary type code already exists.");
        }

        var type = new DictionaryType
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Status = NormalizeStatus(request.Status),
            Sort = request.Sort
        };

        await _typeRepository.AddAsync(type, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveItemsCacheAsync(type.TenantId, type.Code, cancellationToken);

        return ToTypeResponse(type);
    }

    public async Task<DictionaryTypeResponse> UpdateTypeAsync(
        Guid id,
        UpdateDictionaryTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Dictionary type name is required.");

        var type = await GetTypeOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(type, request.ConcurrencyToken);
        type.Name = request.Name.Trim();
        type.Description = NormalizeOptional(request.Description);
        type.Status = NormalizeStatus(request.Status);
        type.Sort = request.Sort;

        _typeRepository.Update(type);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveItemsCacheAsync(type.TenantId, type.Code, cancellationToken);

        return ToTypeResponse(type);
    }

    public async Task DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var type = await GetTypeOrThrowAsync(id, cancellationToken);
        if (_itemRepository.Query().Any(entity => entity.TenantId == type.TenantId && entity.TypeCode == type.Code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Please delete dictionary items first.");
        }

        _typeRepository.Remove(type);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveItemsCacheAsync(type.TenantId, type.Code, cancellationToken);
    }

    public Task<PagedResult<DictionaryItemResponse>> GetItemsPagedAsync(
        DictionaryItemQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _itemRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.TypeCode))
        {
            var typeCode = request.TypeCode.Trim();
            query = query.Where(entity => entity.TypeCode == typeCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Label.Contains(keyword) ||
                entity.Value.Contains(keyword) ||
                (entity.Remark != null && entity.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeStatus(request.Status);
            query = query.Where(entity => entity.Status == status);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Value)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToItemResponse)
            .ToList();

        return Task.FromResult(PagedResult<DictionaryItemResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<DictionaryItemResponse> CreateItemAsync(
        CreateDictionaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.TypeCode, "Dictionary type code is required.");
        ValidateRequired(request.Label, "Dictionary item label is required.");
        ValidateRequired(request.Value, "Dictionary item value is required.");

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var typeCode = request.TypeCode.Trim();
        var value = request.Value.Trim();
        EnsureTypeExists(tenantId, typeCode);

        if (_itemRepository.Query().Any(entity =>
            entity.TenantId == tenantId &&
            entity.TypeCode == typeCode &&
            entity.Value == value))
        {
            throw new BusinessException(ErrorCode.Conflict, "Dictionary item value already exists.");
        }

        if (request.IsDefault)
        {
            ClearDefaultItems(tenantId, typeCode);
        }

        var item = new DictionaryItem
        {
            TenantId = tenantId,
            TypeCode = typeCode,
            Label = request.Label.Trim(),
            Value = value,
            Color = NormalizeOptional(request.Color),
            CssClass = NormalizeOptional(request.CssClass),
            IsDefault = request.IsDefault,
            Status = NormalizeStatus(request.Status),
            Sort = request.Sort,
            Remark = NormalizeOptional(request.Remark)
        };

        await _itemRepository.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveItemsCacheAsync(item.TenantId, item.TypeCode, cancellationToken);

        return ToItemResponse(item);
    }

    public async Task<DictionaryItemResponse> UpdateItemAsync(
        Guid id,
        UpdateDictionaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Label, "Dictionary item label is required.");
        ValidateRequired(request.Value, "Dictionary item value is required.");

        var item = await GetItemOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(item, request.ConcurrencyToken);
        var value = request.Value.Trim();
        if (_itemRepository.Query().Any(entity =>
            entity.Id != id &&
            entity.TenantId == item.TenantId &&
            entity.TypeCode == item.TypeCode &&
            entity.Value == value))
        {
            throw new BusinessException(ErrorCode.Conflict, "Dictionary item value already exists.");
        }

        if (request.IsDefault)
        {
            ClearDefaultItems(item.TenantId, item.TypeCode, item.Id);
        }

        item.Label = request.Label.Trim();
        item.Value = value;
        item.Color = NormalizeOptional(request.Color);
        item.CssClass = NormalizeOptional(request.CssClass);
        item.IsDefault = request.IsDefault;
        item.Status = NormalizeStatus(request.Status);
        item.Sort = request.Sort;
        item.Remark = NormalizeOptional(request.Remark);

        _itemRepository.Update(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveItemsCacheAsync(item.TenantId, item.TypeCode, cancellationToken);

        return ToItemResponse(item);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await GetItemOrThrowAsync(id, cancellationToken);
        var tenantId = item.TenantId;
        var typeCode = item.TypeCode;

        _itemRepository.Remove(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveItemsCacheAsync(tenantId, typeCode, cancellationToken);
    }

    public async Task<IReadOnlyList<DictionaryItemResponse>> GetEnabledItemsByTypeCodeAsync(
        string typeCode,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(typeCode, "Dictionary type code is required.");

        var normalizedTypeCode = typeCode.Trim();
        var tenantId = _tenantContext.TenantId;
        var cacheKey = tenantId.HasValue ? BuildItemsCacheKey(tenantId.Value, normalizedTypeCode) : null;

        if (cacheKey is not null)
        {
            var cachedItems = await _cacheService.GetAsync<IReadOnlyList<DictionaryItemResponse>>(cacheKey, cancellationToken);
            if (cachedItems is not null)
            {
                return cachedItems;
            }
        }

        var typeEnabled = _typeRepository.Query().Any(entity =>
            entity.Code == normalizedTypeCode &&
            entity.Status == EnabledStatus);

        var items = typeEnabled
            ? _itemRepository.Query()
                .Where(entity => entity.TypeCode == normalizedTypeCode && entity.Status == EnabledStatus)
                .OrderBy(entity => entity.Sort)
                .ThenBy(entity => entity.Value)
                .Select(ToItemResponse)
                .ToList()
            : [];

        if (cacheKey is not null)
        {
            await _cacheService.SetAsync(cacheKey, items, DictionaryCacheTtl, cancellationToken: cancellationToken);
        }

        return items;
    }

    private async Task<DictionaryType> GetTypeOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _typeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Dictionary type was not found.");
    }

    private async Task<DictionaryItem> GetItemOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _itemRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Dictionary item was not found.");
    }

    private void EnsureTypeExists(Guid tenantId, string typeCode)
    {
        if (!_typeRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == typeCode))
        {
            throw new BusinessException(ErrorCode.BadRequest, "Dictionary type is invalid.");
        }
    }

    private void ClearDefaultItems(Guid tenantId, string typeCode, Guid? exceptItemId = null)
    {
        var defaultItems = _itemRepository.Query()
            .Where(entity =>
                entity.TenantId == tenantId &&
                entity.TypeCode == typeCode &&
                entity.IsDefault &&
                (!exceptItemId.HasValue || entity.Id != exceptItemId.Value))
            .ToList();

        foreach (var item in defaultItems)
        {
            item.IsDefault = false;
            _itemRepository.Update(item);
        }
    }

    private Task RemoveItemsCacheAsync(Guid tenantId, string typeCode, CancellationToken cancellationToken)
    {
        return _cacheService.RemoveAsync(BuildItemsCacheKey(tenantId, typeCode), cancellationToken);
    }

    private static string BuildItemsCacheKey(Guid tenantId, string typeCode)
    {
        return $"ps:dict:items:{tenantId:N}:{typeCode.ToLowerInvariant()}";
    }

    private static DictionaryTypeResponse ToTypeResponse(DictionaryType type)
    {
        return new DictionaryTypeResponse
        {
            Id = type.Id,
            TenantId = type.TenantId,
            Code = type.Code,
            Name = type.Name,
            Description = type.Description,
            Status = type.Status,
            Sort = type.Sort,
            CreatedAt = type.CreatedAt,
            ConcurrencyToken = type.RowVersion
        };
    }

    private static DictionaryItemResponse ToItemResponse(DictionaryItem item)
    {
        return new DictionaryItemResponse
        {
            Id = item.Id,
            TenantId = item.TenantId,
            TypeCode = item.TypeCode,
            Label = item.Label,
            Value = item.Value,
            Color = item.Color,
            CssClass = item.CssClass,
            IsDefault = item.IsDefault,
            Status = item.Status,
            Sort = item.Sort,
            Remark = item.Remark,
            CreatedAt = item.CreatedAt,
            ConcurrencyToken = item.RowVersion
        };
    }

    private static string NormalizeStatus(string? status)
    {
        return string.Equals(status, DisabledStatus, StringComparison.OrdinalIgnoreCase)
            ? DisabledStatus
            : EnabledStatus;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
