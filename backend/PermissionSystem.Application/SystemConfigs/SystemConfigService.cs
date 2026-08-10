using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.SystemConfigs;

public sealed class SystemConfigService : ISystemConfigService
{
    private const string EnabledStatus = "Enabled";
    private const string DisabledStatus = "Disabled";
    private const string MaskedValue = "******";
    private static readonly TimeSpan ConfigCacheTtl = TimeSpan.FromMinutes(30);

    private readonly IRepository<SystemConfig> _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IConfigValueProtector _valueProtector;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantWriteResolver _tenantWriteResolver;

    public SystemConfigService(
        IRepository<SystemConfig> configRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IConfigValueProtector valueProtector,
        ITenantContext tenantContext,
        ITenantWriteResolver tenantWriteResolver)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _valueProtector = valueProtector;
        _tenantContext = tenantContext;
        _tenantWriteResolver = tenantWriteResolver;
    }

    public Task<PagedResult<SystemConfigResponse>> GetPagedAsync(
        SystemConfigQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _configRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.ConfigKey.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                entity.GroupCode.Contains(keyword) ||
                (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.GroupCode))
        {
            var groupCode = request.GroupCode.Trim();
            query = query.Where(entity => entity.GroupCode == groupCode);
        }

        if (!string.IsNullOrWhiteSpace(request.ConfigType))
        {
            var configType = request.ConfigType.Trim();
            query = query.Where(entity => entity.ConfigType == configType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeStatus(request.Status);
            query = query.Where(entity => entity.Status == status);
        }

        if (request.IsEncrypted.HasValue)
        {
            query = query.Where(entity => entity.IsEncrypted == request.IsEncrypted.Value);
        }

        if (request.IsSystem.HasValue)
        {
            query = query.Where(entity => entity.IsSystem == request.IsSystem.Value);
        }

        var totalCount = query.LongCount();
        var configs = query
            .OrderBy(entity => entity.GroupCode)
            .ThenBy(entity => entity.Sort)
            .ThenBy(entity => entity.ConfigKey)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(entity => ToResponse(entity, maskSensitive: true))
            .ToList();

        return Task.FromResult(PagedResult<SystemConfigResponse>.Create(
            configs,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<SystemConfigResponse> CreateAsync(
        CreateSystemConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.ConfigKey, "Config key is required.");
        ValidateRequired(request.ConfigType, "Config type is required.");
        ValidateRequired(request.GroupCode, "Group code is required.");
        ValidateRequired(request.Name, "Config name is required.");

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var configKey = request.ConfigKey.Trim();
        if (_configRepository.Query().Any(entity => entity.TenantId == tenantId && entity.ConfigKey == configKey))
        {
            throw new BusinessException(ErrorCode.Conflict, "Config key already exists.");
        }

        var config = new SystemConfig
        {
            TenantId = tenantId,
            ConfigKey = configKey,
            ConfigValue = ProtectIfNeeded(request.ConfigValue, request.IsEncrypted),
            ConfigType = request.ConfigType.Trim(),
            GroupCode = request.GroupCode.Trim(),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            IsEncrypted = request.IsEncrypted,
            IsSystem = request.IsSystem,
            Status = NormalizeStatus(request.Status),
            Sort = request.Sort
        };

        await _configRepository.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveConfigCacheAsync(config.TenantId, config.ConfigKey, cancellationToken);

        return ToResponse(config, maskSensitive: true);
    }

    public async Task<SystemConfigResponse> UpdateAsync(
        Guid id,
        UpdateSystemConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.ConfigType, "Config type is required.");
        ValidateRequired(request.GroupCode, "Group code is required.");
        ValidateRequired(request.Name, "Config name is required.");

        var config = await GetConfigOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(config, request.ConcurrencyToken);
        var plainValue = request.ConfigValue is null
            ? UnprotectIfNeeded(config.ConfigValue, config.IsEncrypted)
            : request.ConfigValue;

        config.ConfigValue = ProtectIfNeeded(plainValue, request.IsEncrypted);
        config.ConfigType = request.ConfigType.Trim();
        config.GroupCode = request.GroupCode.Trim();
        config.Name = request.Name.Trim();
        config.Description = NormalizeOptional(request.Description);
        config.IsEncrypted = request.IsEncrypted;
        config.IsSystem = request.IsSystem;
        config.Status = NormalizeStatus(request.Status);
        config.Sort = request.Sort;

        _configRepository.Update(config);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveConfigCacheAsync(config.TenantId, config.ConfigKey, cancellationToken);

        return ToResponse(config, maskSensitive: true);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigOrThrowAsync(id, cancellationToken);
        if (config.IsSystem)
        {
            throw new BusinessException(ErrorCode.Conflict, "System config cannot be deleted.");
        }

        var tenantId = config.TenantId;
        var configKey = config.ConfigKey;

        _configRepository.Remove(config);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveConfigCacheAsync(tenantId, configKey, cancellationToken);
    }

    public async Task<SystemConfigValueResponse> GetValueByKeyAsync(
        string configKey,
        bool revealSensitive = false,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(configKey, "Config key is required.");

        var entry = await GetCacheEntryByKeyAsync(configKey.Trim(), cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Config was not found.");

        if (entry.Status != EnabledStatus)
        {
            throw new BusinessException(ErrorCode.NotFound, "Config was not found.");
        }

        var plainValue = UnprotectIfNeeded(entry.ConfigValue, entry.IsEncrypted);
        return new SystemConfigValueResponse
        {
            ConfigKey = entry.ConfigKey,
            ConfigValue = entry.IsEncrypted && !revealSensitive ? MaskedValue : plainValue,
            ConfigType = entry.ConfigType,
            IsEncrypted = entry.IsEncrypted
        };
    }

    public Task<IReadOnlyList<SystemConfigResponse>> GetEnabledByGroupCodeAsync(
        string groupCode,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(groupCode, "Group code is required.");

        var normalizedGroupCode = groupCode.Trim();
        var configs = _configRepository.Query()
            .Where(entity => entity.GroupCode == normalizedGroupCode && entity.Status == EnabledStatus)
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.ConfigKey)
            .ToList()
            .Select(entity => ToResponse(entity, maskSensitive: true))
            .ToList();

        return Task.FromResult<IReadOnlyList<SystemConfigResponse>>(configs);
    }

    private async Task<SystemConfigCacheEntry?> GetCacheEntryByKeyAsync(
        string configKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var cacheKey = tenantId.HasValue ? BuildConfigCacheKey(tenantId.Value, configKey) : null;

        if (cacheKey is not null)
        {
            var cachedEntry = await _cacheService.GetAsync<SystemConfigCacheEntry>(cacheKey, cancellationToken);
            if (cachedEntry is not null)
            {
                return cachedEntry;
            }
        }

        var config = _configRepository.Query().FirstOrDefault(entity => entity.ConfigKey == configKey);
        if (config is null)
        {
            return null;
        }

        var entry = new SystemConfigCacheEntry
        {
            TenantId = config.TenantId,
            ConfigKey = config.ConfigKey,
            ConfigValue = config.ConfigValue,
            ConfigType = config.ConfigType,
            IsEncrypted = config.IsEncrypted,
            Status = config.Status
        };

        if (cacheKey is not null)
        {
            await _cacheService.SetAsync(cacheKey, entry, ConfigCacheTtl, cancellationToken: cancellationToken);
        }

        return entry;
    }

    private async Task<SystemConfig> GetConfigOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _configRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Config was not found.");
    }

    private Task RemoveConfigCacheAsync(Guid tenantId, string configKey, CancellationToken cancellationToken)
    {
        return _cacheService.RemoveAsync(BuildConfigCacheKey(tenantId, configKey), cancellationToken);
    }

    private static string BuildConfigCacheKey(Guid tenantId, string configKey)
    {
        return $"ps:config:key:{tenantId:N}:{configKey.ToLowerInvariant()}";
    }

    private string ProtectIfNeeded(string? value, bool isEncrypted)
    {
        var normalizedValue = value ?? string.Empty;
        return isEncrypted ? _valueProtector.Protect(normalizedValue) : normalizedValue;
    }

    private string UnprotectIfNeeded(string value, bool isEncrypted)
    {
        return isEncrypted ? _valueProtector.Unprotect(value) : value;
    }

    private SystemConfigResponse ToResponse(SystemConfig config, bool maskSensitive)
    {
        var plainValue = UnprotectIfNeeded(config.ConfigValue, config.IsEncrypted);
        return new SystemConfigResponse
        {
            Id = config.Id,
            TenantId = config.TenantId,
            ConfigKey = config.ConfigKey,
            ConfigValue = config.IsEncrypted && maskSensitive ? MaskedValue : plainValue,
            ConfigType = config.ConfigType,
            GroupCode = config.GroupCode,
            Name = config.Name,
            Description = config.Description,
            IsEncrypted = config.IsEncrypted,
            IsSystem = config.IsSystem,
            Status = config.Status,
            Sort = config.Sort,
            CreatedAt = config.CreatedAt,
            ConcurrencyToken = config.RowVersion
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
