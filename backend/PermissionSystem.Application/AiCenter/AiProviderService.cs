using System.Text.Json;
using System.Text.RegularExpressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public interface IAiProviderService
{
    Task<PagedResult<AiProviderListResponse>> GetPagedAsync(AiProviderQueryRequest request, CancellationToken cancellationToken = default);

    Task<AiProviderDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AiProviderDetailResponse> CreateAsync(CreateAiProviderRequest request, CancellationToken cancellationToken = default);

    Task<AiProviderDetailResponse> UpdateAsync(Guid id, UpdateAiProviderRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(Guid id, SetAiProviderEnabledRequest request, CancellationToken cancellationToken = default);

    Task SetDefaultAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AiProviderConnectionTestResult> TestAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetComplianceAsync(Guid id, SetAiProviderComplianceRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiProviderService : IAiProviderService
{
    private const string MaskedApiKey = "********";
    private static readonly Regex ProviderCodeRegex = new(
        "^[a-z0-9][a-z0-9_-]{0,99}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<AiProviderConfig> _providerRepository;
    private readonly IRepository<AiRun> _runRepository;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;
    private readonly IConfigValueProtector _valueProtector;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IAiProviderConnectionTester _connectionTester;
    private readonly IAiCenterConfiguration _aiCenterConfiguration;
    private readonly IUnitOfWork _unitOfWork;

    public AiProviderService(
        IRepository<AiProviderConfig> providerRepository,
        IRepository<AiRun> runRepository,
        IAsyncQueryExecutor asyncQueryExecutor,
        IConfigValueProtector valueProtector,
        ITenantWriteResolver tenantWriteResolver,
        IAiProviderConnectionTester connectionTester,
        IUnitOfWork unitOfWork,
        IAiCenterConfiguration? aiCenterConfiguration = null)
    {
        _providerRepository = providerRepository;
        _runRepository = runRepository;
        _asyncQueryExecutor = asyncQueryExecutor;
        _valueProtector = valueProtector;
        _tenantWriteResolver = tenantWriteResolver;
        _connectionTester = connectionTester;
        _unitOfWork = unitOfWork;
        _aiCenterConfiguration = aiCenterConfiguration ?? new DefaultAiCenterConfiguration();
    }

    public async Task<PagedResult<AiProviderListResponse>> GetPagedAsync(
        AiProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _providerRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.ProviderCode.Contains(keyword) ||
                entity.ProviderName.Contains(keyword) ||
                entity.ModelName.Contains(keyword));
        }

        if (request.Enabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.Enabled.Value);
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var entities = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.IsDefault)
                .ThenBy(entity => entity.ProviderCode)
                .Skip(request.Skip)
                .Take(request.PageSize),
            cancellationToken);

        return PagedResult<AiProviderListResponse>.Create(
            entities.Select(ToListResponse).ToList(),
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    public async Task<AiProviderDetailResponse> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return ToDetailResponse(await GetProviderOrThrowAsync(id, cancellationToken));
    }

    public async Task<AiProviderDetailResponse> CreateAsync(
        CreateAiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        if (request.IsDefault && !request.IsEnabled)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "A disabled AI provider cannot be the default.");
        }

        var providerCode = NormalizeProviderCode(request.ProviderCode);
        if (await _asyncQueryExecutor.AnyAsync(
                _providerRepository.Query().Where(entity =>
                    entity.TenantId == tenantId && entity.ProviderCode == providerCode),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "AI provider code already exists in current tenant.");
        }

        var apiKey = TrimRequired(request.ApiKey, "AI provider API key is required.");
        var allowedHosts = NormalizeAllowedHosts(request.AllowedHosts);
        var settings = BuildConnectionSettings(
            request.ProviderType,
            request.BaseUrl,
            request.ChatCompletionsPath,
            apiKey,
            request.ModelName,
            request.TimeoutSeconds,
            request.AllowInsecureHttp,
            request.AllowPrivateNetwork,
            allowedHosts);
        ValidateSettings(settings, request.Temperature, request.MaxTokens);

        var provider = new AiProviderConfig
        {
            TenantId = tenantId,
            ProviderCode = providerCode,
            ProviderName = TrimRequired(request.ProviderName, "AI provider name is required.", 200),
            ProviderType = request.ProviderType,
            BaseUrl = settings.BaseUrl,
            ChatCompletionsPath = settings.ChatCompletionsPath,
            ApiKeyEncrypted = _valueProtector.Protect(apiKey),
            ModelName = settings.ModelName,
            IsDefault = request.IsDefault,
            IsEnabled = request.IsEnabled,
            TimeoutSeconds = settings.TimeoutSeconds,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            AllowInsecureHttp = request.AllowInsecureHttp,
            AllowPrivateNetwork = request.AllowPrivateNetwork,
            AllowedHostsJson = JsonSerializer.Serialize(allowedHosts, JsonOptions),
            DataResidency = NormalizeOptional(request.DataResidency, 100, "Data residency is too long."),
            Remark = NormalizeOptional(request.Remark, 500, "Remark is too long.")
        };

        if (!provider.IsDefault)
        {
            await _providerRepository.AddAsync(provider, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDetailResponse(provider);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ClearCurrentDefaultAsync(tenantId, null, token);
            await _providerRepository.AddAsync(provider, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToDetailResponse(provider);
    }

    public async Task<AiProviderDetailResponse> UpdateAsync(
        Guid id,
        UpdateAiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(provider, request.ConcurrencyToken);

        var apiKey = string.IsNullOrWhiteSpace(request.ApiKey) || request.ApiKey.Trim() == MaskedApiKey
            ? _valueProtector.Unprotect(provider.ApiKeyEncrypted)
            : request.ApiKey.Trim();
        var allowedHosts = NormalizeAllowedHosts(request.AllowedHosts);
        var settings = BuildConnectionSettings(
            provider.ProviderType,
            request.BaseUrl,
            request.ChatCompletionsPath,
            apiKey,
            request.ModelName,
            request.TimeoutSeconds,
            request.AllowInsecureHttp,
            request.AllowPrivateNetwork,
            allowedHosts);
        ValidateSettings(settings, request.Temperature, request.MaxTokens);

        provider.ProviderName = TrimRequired(request.ProviderName, "AI provider name is required.", 200);
        provider.BaseUrl = settings.BaseUrl;
        provider.ChatCompletionsPath = settings.ChatCompletionsPath;
        if (!string.IsNullOrWhiteSpace(request.ApiKey) && request.ApiKey.Trim() != MaskedApiKey)
        {
            provider.ApiKeyEncrypted = _valueProtector.Protect(request.ApiKey.Trim());
        }

        provider.ModelName = settings.ModelName;
        provider.TimeoutSeconds = settings.TimeoutSeconds;
        provider.Temperature = request.Temperature;
        provider.MaxTokens = request.MaxTokens;
        provider.AllowInsecureHttp = request.AllowInsecureHttp;
        provider.AllowPrivateNetwork = request.AllowPrivateNetwork;
        provider.AllowedHostsJson = JsonSerializer.Serialize(allowedHosts, JsonOptions);
        provider.DataResidency = NormalizeOptional(request.DataResidency, 100, "Data residency is too long.");
        provider.Remark = NormalizeOptional(request.Remark, 500, "Remark is too long.");

        _providerRepository.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDetailResponse(provider);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        if (await _asyncQueryExecutor.AnyAsync(
                _runRepository.Query().Where(entity => entity.ProviderConfigId == provider.Id),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "AI providers referenced by runs cannot be deleted.");
        }

        _providerRepository.Remove(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(
        Guid id,
        SetAiProviderEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(provider, request.ConcurrencyToken);
        provider.IsEnabled = request.IsEnabled;
        if (!request.IsEnabled)
        {
            provider.IsDefault = false;
        }

        _providerRepository.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        if (!provider.IsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "A disabled AI provider cannot be set as default.");
        }

        if (provider.IsDefault)
        {
            return;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ClearCurrentDefaultAsync(provider.TenantId, provider.Id, token);
            provider.IsDefault = true;
            _providerRepository.Update(provider);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task<AiProviderConnectionTestResult> TestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_aiCenterConfiguration.Enabled)
        {
            throw new BusinessException(ErrorCode.Forbidden, "AI center is disabled by the global kill switch.");
        }

        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        if (!_aiCenterConfiguration.AllowedTenantIds.Contains(provider.TenantId))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current tenant is not allowed to use AI.");
        }

        if (!provider.IsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "AI provider is disabled.");
        }

        EnsureComplianceConfirmed(provider);

        var settings = ToConnectionSettings(provider);
        _connectionTester.Validate(settings);
        return await _connectionTester.TestAsync(settings, cancellationToken);
    }

    public async Task SetComplianceAsync(
        Guid id,
        SetAiProviderComplianceRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(provider, request.ConcurrencyToken);
        provider.ComplianceConfirmedAt = request.IsConfirmed ? DateTimeOffset.UtcNow : null;
        _providerRepository.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static void EnsureComplianceConfirmed(AiProviderConfig provider)
    {
        if (!provider.ComplianceConfirmedAt.HasValue)
        {
            throw new BusinessException(
                ErrorCode.Forbidden,
                "AI provider compliance must be confirmed before model calls are allowed.");
        }
    }

    private async Task ClearCurrentDefaultAsync(Guid tenantId, Guid? excludedId, CancellationToken cancellationToken)
    {
        var currentDefault = await _asyncQueryExecutor.FirstOrDefaultAsync(
            _providerRepository.Query().Where(entity =>
                entity.TenantId == tenantId &&
                entity.IsDefault &&
                (!excludedId.HasValue || entity.Id != excludedId.Value)),
            cancellationToken);
        if (currentDefault is null)
        {
            return;
        }

        currentDefault.IsDefault = false;
        _providerRepository.Update(currentDefault);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AiProviderConfig> GetProviderOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _providerRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "AI provider was not found.");
    }

    private AiProviderConnectionSettings ToConnectionSettings(AiProviderConfig provider)
    {
        return BuildConnectionSettings(
            provider.ProviderType,
            provider.BaseUrl,
            provider.ChatCompletionsPath,
            _valueProtector.Unprotect(provider.ApiKeyEncrypted),
            provider.ModelName,
            provider.TimeoutSeconds,
            provider.AllowInsecureHttp,
            provider.AllowPrivateNetwork,
            DeserializeAllowedHosts(provider.AllowedHostsJson));
    }

    private void ValidateSettings(
        AiProviderConnectionSettings settings,
        decimal? temperature,
        int? maxTokens)
    {
        if (settings.ProviderType != AiProviderType.OpenAiCompatible)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Only OpenAI Compatible providers are supported in P1.");
        }

        if (temperature is < 0 or > 2)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Temperature must be between 0 and 2.");
        }

        if (maxTokens is <= 0 or > 128000)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "MaxTokens must be between 1 and 128000.");
        }

        _connectionTester.Validate(settings);
    }

    private static AiProviderConnectionSettings BuildConnectionSettings(
        AiProviderType providerType,
        string baseUrl,
        string chatCompletionsPath,
        string apiKey,
        string modelName,
        int timeoutSeconds,
        bool allowInsecureHttp,
        bool allowPrivateNetwork,
        IReadOnlyCollection<string> allowedHosts)
    {
        return new AiProviderConnectionSettings
        {
            ProviderType = providerType,
            BaseUrl = TrimRequired(baseUrl, "AI provider BaseUrl is required.", 1000),
            ChatCompletionsPath = TrimRequired(chatCompletionsPath, "AI provider chat completions path is required.", 256),
            ApiKey = TrimRequired(apiKey, "AI provider API key is required.", 1000),
            ModelName = TrimRequired(modelName, "AI provider model name is required.", 200),
            TimeoutSeconds = timeoutSeconds,
            AllowInsecureHttp = allowInsecureHttp,
            AllowPrivateNetwork = allowPrivateNetwork,
            AllowedHosts = allowedHosts
        };
    }

    private static string NormalizeProviderCode(string value)
    {
        var code = TrimRequired(value, "AI provider code is required.").ToLowerInvariant();
        if (!ProviderCodeRegex.IsMatch(code))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI provider code is invalid.");
        }

        return code;
    }

    private static IReadOnlyList<string> NormalizeAllowedHosts(IReadOnlyCollection<string>? values)
    {
        var hosts = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (hosts.Count is 0 or > 32 ||
            hosts.Any(host => host.Length > 253 || host.Contains('*') || Uri.CheckHostName(host) == UriHostNameType.Unknown))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI provider allowed hosts are invalid.");
        }

        if (JsonSerializer.Serialize(hosts, JsonOptions).Length > 4000)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI provider allowed hosts are too large.");
        }

        return hosts;
    }

    private static IReadOnlyList<string> DeserializeAllowedHosts(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new BusinessException(ErrorCode.InternalServerError, "AI provider host policy is invalid.");
        }
    }

    private static string TrimRequired(string? value, string message, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        var normalized = value.Trim();
        if (maxLength.HasValue && normalized.Length > maxLength.Value)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return normalized;
    }

    private static AiProviderListResponse ToListResponse(AiProviderConfig entity)
    {
        return new AiProviderListResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderCode = entity.ProviderCode,
            ProviderName = entity.ProviderName,
            ProviderType = entity.ProviderType,
            BaseUrl = entity.BaseUrl,
            ModelName = entity.ModelName,
            IsDefault = entity.IsDefault,
            IsEnabled = entity.IsEnabled,
            DataResidency = entity.DataResidency,
            ComplianceConfirmedAt = entity.ComplianceConfirmedAt,
            CreatedAt = entity.CreatedAt,
            ConcurrencyToken = entity.RowVersion
        };
    }

    private static AiProviderDetailResponse ToDetailResponse(AiProviderConfig entity)
    {
        return new AiProviderDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderCode = entity.ProviderCode,
            ProviderName = entity.ProviderName,
            ProviderType = entity.ProviderType,
            BaseUrl = entity.BaseUrl,
            ChatCompletionsPath = entity.ChatCompletionsPath,
            ApiKey = string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted) ? string.Empty : MaskedApiKey,
            HasApiKey = !string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted),
            ModelName = entity.ModelName,
            IsDefault = entity.IsDefault,
            IsEnabled = entity.IsEnabled,
            TimeoutSeconds = entity.TimeoutSeconds,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            AllowInsecureHttp = entity.AllowInsecureHttp,
            AllowPrivateNetwork = entity.AllowPrivateNetwork,
            AllowedHosts = DeserializeAllowedHosts(entity.AllowedHostsJson),
            DataResidency = entity.DataResidency,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ConcurrencyToken = entity.RowVersion
        };
    }
}
