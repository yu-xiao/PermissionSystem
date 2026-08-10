using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Security;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Integration;

public sealed class OpenIntegrationService : IOpenIntegrationService
{
    private const string MaskedSecret = "******";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRepository<ApiClient> _clientRepository;
    private readonly IRepository<ApiClientSecret> _secretRepository;
    private readonly IRepository<WebhookSubscription> _webhookRepository;
    private readonly IRepository<WebhookDeliveryLog> _webhookLogRepository;
    private readonly IRepository<ExternalApiCallLog> _apiCallLogRepository;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IConfigValueProtector _valueProtector;
    private readonly IWebhookHttpSender _webhookHttpSender;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantStatusChecker _tenantStatusChecker;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;

    public OpenIntegrationService(
        IRepository<ApiClient> clientRepository,
        IRepository<ApiClientSecret> secretRepository,
        IRepository<WebhookSubscription> webhookRepository,
        IRepository<WebhookDeliveryLog> webhookLogRepository,
        IRepository<ExternalApiCallLog> apiCallLogRepository,
        IBackgroundJobService backgroundJobService,
        IConfigValueProtector valueProtector,
        IWebhookHttpSender webhookHttpSender,
        ISecurityPolicyService securityPolicyService,
        ITenantContext tenantContext,
        ITenantStatusChecker tenantStatusChecker,
        IUnitOfWork unitOfWork,
        IAsyncQueryExecutor asyncQueryExecutor)
    {
        _clientRepository = clientRepository;
        _secretRepository = secretRepository;
        _webhookRepository = webhookRepository;
        _webhookLogRepository = webhookLogRepository;
        _apiCallLogRepository = apiCallLogRepository;
        _backgroundJobService = backgroundJobService;
        _valueProtector = valueProtector;
        _webhookHttpSender = webhookHttpSender;
        _securityPolicyService = securityPolicyService;
        _tenantContext = tenantContext;
        _tenantStatusChecker = tenantStatusChecker;
        _unitOfWork = unitOfWork;
        _asyncQueryExecutor = asyncQueryExecutor;
    }

    public Task<PagedResult<ApiClientResponse>> GetClientsAsync(
        ApiClientQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _clientRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.ClientCode.Contains(keyword) ||
                entity.ClientName.Contains(keyword) ||
                (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<ApiClientResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<ApiClientResponse> CreateClientAsync(
        CreateApiClientRequest request,
        CancellationToken cancellationToken = default)
    {
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("integration:client:create", force: true, cancellationToken);
        var clientCode = NormalizeCode(request.ClientCode, "Client code is required.");
        if (_clientRepository.Query().Any(entity => entity.ClientCode == clientCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "API client code already exists.");
        }

        var client = new ApiClient
        {
            ClientCode = clientCode,
            ClientName = TrimRequired(request.ClientName, "Client name is required."),
            Description = NormalizeOptional(request.Description),
            IsEnabled = request.IsEnabled,
            AllowedScopes = NormalizeOptional(request.AllowedScopes),
            AllowedIpList = NormalizeOptional(request.AllowedIpList),
            RateLimitPerMinute = NormalizeRateLimit(request.RateLimitPerMinute)
        };

        await _clientRepository.AddAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(client);
    }

    public async Task<ApiClientResponse> UpdateClientAsync(
        Guid id,
        UpdateApiClientRequest request,
        CancellationToken cancellationToken = default)
    {
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("integration:client:update", force: true, cancellationToken);
        var client = await GetClientOrThrowAsync(id, cancellationToken);
        client.ClientName = TrimRequired(request.ClientName, "Client name is required.");
        client.Description = NormalizeOptional(request.Description);
        client.AllowedScopes = NormalizeOptional(request.AllowedScopes);
        client.AllowedIpList = NormalizeOptional(request.AllowedIpList);
        client.RateLimitPerMinute = NormalizeRateLimit(request.RateLimitPerMinute);
        _clientRepository.Update(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(client);
    }

    public async Task DeleteClientAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("integration:client:delete", force: true, cancellationToken);
        var client = await GetClientOrThrowAsync(id, cancellationToken);
        foreach (var secret in _secretRepository.Query().Where(entity => entity.ClientId == client.Id).ToList())
        {
            _secretRepository.Remove(secret);
        }

        _clientRepository.Remove(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<GenerateApiClientSecretResponse> GenerateSecretAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("integration:client:secret", force: true, cancellationToken);
        var client = await GetClientOrThrowAsync(id, cancellationToken);
        var rawSecret = GenerateSecretValue();
        var entity = new ApiClientSecret
        {
            TenantId = client.TenantId,
            ClientId = client.Id,
            SecretHash = HashSecret(rawSecret)
        };

        await _secretRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new GenerateApiClientSecretResponse
        {
            ClientId = client.Id,
            ApiKey = client.ClientCode,
            ApiSecret = rawSecret,
            ExpiresAt = entity.ExpiresAt
        };
    }

    public async Task SetClientEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync(
            isEnabled ? "integration:client:enable" : "integration:client:disable",
            force: true,
            cancellationToken);
        var client = await GetClientOrThrowAsync(id, cancellationToken);
        client.IsEnabled = isEnabled;
        _clientRepository.Update(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApiClientValidationResult> ValidateApiClientAsync(
        string apiKey,
        string apiSecret,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var clientCode = NormalizeCode(apiKey, "API key is required.");
        if (!ShouldUseResolvedTenantForApiKeyLookup())
        {
            return FailedValidation("X-Tenant-Id is required for API client authentication.");
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var client = _clientRepository.QueryForTenant(tenantId)
            .FirstOrDefault(entity => entity.ClientCode == clientCode);
        if (client is null || !client.IsEnabled)
        {
            return FailedValidation("API client is invalid or disabled.");
        }

        if (!IsIpAllowed(client.AllowedIpList, ipAddress))
        {
            return FailedValidation("Current IP is not allowed for this API client.");
        }

        var now = DateTimeOffset.UtcNow;
        var secretHash = HashSecret(apiSecret);
        var secret = _secretRepository.QueryForTenant(client.TenantId)
            .FirstOrDefault(entity =>
                !entity.IsDeleted &&
                entity.TenantId == client.TenantId &&
                entity.ClientId == client.Id &&
                entity.SecretHash == secretHash &&
                (entity.ExpiresAt == null || entity.ExpiresAt > now));
        if (secret is null)
        {
            return FailedValidation("API secret is invalid or expired.");
        }

        secret.LastUsedAt = now;
        _secretRepository.Update(secret);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApiClientValidationResult
        {
            Succeeded = true,
            TenantId = client.TenantId,
            ClientId = client.Id,
            ClientCode = client.ClientCode,
            AllowedScopes = client.AllowedScopes,
            RateLimitPerMinute = client.RateLimitPerMinute
        };
    }

    public async Task RecordExternalApiCallAsync(
        RecordExternalApiCallRequest request,
        CancellationToken cancellationToken = default)
    {
        await _apiCallLogRepository.AddAsync(new ExternalApiCallLog
        {
            TenantId = request.TenantId,
            ClientId = request.ClientId,
            Path = Truncate(TrimRequired(request.Path, "Path is required."), 500) ?? string.Empty,
            Method = Truncate(TrimRequired(request.Method, "Method is required."), 16) ?? string.Empty,
            IpAddress = Truncate(NormalizeOptional(request.IpAddress), 64),
            StatusCode = request.StatusCode,
            ElapsedMilliseconds = request.ElapsedMilliseconds
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<PagedResult<WebhookSubscriptionResponse>> GetWebhooksAsync(
        WebhookQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _webhookRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            var eventType = request.EventType.Trim();
            query = query.Where(entity => entity.EventType == eventType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.EventType)
            .ThenByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<WebhookSubscriptionResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<WebhookSubscriptionResponse> CreateWebhookAsync(
        CreateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var secret = string.IsNullOrWhiteSpace(request.Secret) ? GenerateSecretValue() : request.Secret.Trim();
        var webhook = new WebhookSubscription
        {
            EventType = NormalizeEventType(request.EventType),
            TargetUrl = NormalizeHttpsUrl(request.TargetUrl),
            Secret = _valueProtector.Protect(secret),
            IsEnabled = request.IsEnabled,
            RetryCount = NormalizeRetryCount(request.RetryCount)
        };

        await _webhookRepository.AddAsync(webhook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(webhook);
    }

    public async Task<WebhookSubscriptionResponse> UpdateWebhookAsync(
        Guid id,
        UpdateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var webhook = await GetWebhookOrThrowAsync(id, cancellationToken);
        webhook.EventType = NormalizeEventType(request.EventType);
        webhook.TargetUrl = NormalizeHttpsUrl(request.TargetUrl);
        if (!string.IsNullOrWhiteSpace(request.Secret) && request.Secret.Trim() != MaskedSecret)
        {
            webhook.Secret = _valueProtector.Protect(request.Secret.Trim());
        }

        webhook.IsEnabled = request.IsEnabled;
        webhook.RetryCount = NormalizeRetryCount(request.RetryCount);
        _webhookRepository.Update(webhook);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(webhook);
    }

    public async Task DeleteWebhookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var webhook = await GetWebhookOrThrowAsync(id, cancellationToken);
        _webhookRepository.Remove(webhook);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task TestWebhookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var webhook = await GetWebhookOrThrowAsync(id, cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            eventType = webhook.EventType,
            occurredAt = DateTimeOffset.UtcNow,
            data = new { message = "Webhook test event" }
        }, JsonOptions);

        _backgroundJobService.Enqueue<WebhookDeliveryJob>(
            job => job.DeliverAsync(webhook.Id, webhook.EventType, payload, 0));
        await Task.CompletedTask;
    }

    public async Task PublishWebhookAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var normalizedEventType = NormalizeEventType(eventType);
        var body = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            eventType = normalizedEventType,
            occurredAt = DateTimeOffset.UtcNow,
            data = payload
        }, JsonOptions);

        var tenantId = ResolveTenantId();
        var subscriptions = _webhookRepository.Query()
            .Where(entity => entity.EventType == normalizedEventType && entity.IsEnabled)
            .Where(entity => entity.TenantId == tenantId)
            .ToList();
        foreach (var subscription in subscriptions)
        {
            _backgroundJobService.Enqueue<WebhookDeliveryJob>(
                job => job.DeliverAsync(subscription.Id, normalizedEventType, body, 0));
        }

        await Task.CompletedTask;
    }

    public async Task DeliverWebhookAsync(
        Guid subscriptionId,
        string eventType,
        string payload,
        int attempt,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _webhookRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription is null || !subscription.IsEnabled)
        {
            return;
        }

        if (!await _tenantStatusChecker.IsActiveAsync(subscription.TenantId, cancellationToken))
        {
            return;
        }

        var result = await _webhookHttpSender.SendAsync(
            subscription.TargetUrl,
            eventType,
            payload,
            _valueProtector.Unprotect(subscription.Secret),
            cancellationToken);
        var status = result.Succeeded ? "Succeeded" : "Failed";

        await _webhookLogRepository.AddAsync(new WebhookDeliveryLog
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            EventType = eventType,
            Payload = BuildPayloadLogSummary(payload),
            Status = status,
            ResponseStatusCode = result.StatusCode,
            ResponseBody = BuildResponseLogSummary(result.ResponseBody),
            RetryCount = attempt
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!result.Succeeded && attempt < subscription.RetryCount)
        {
            var nextAttempt = attempt + 1;
            var delay = TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, attempt)));
            _backgroundJobService.Schedule<WebhookDeliveryJob>(
                job => job.DeliverAsync(subscription.Id, eventType, payload, nextAttempt),
                delay);
        }
    }

    public Task<PagedResult<WebhookDeliveryLogResponse>> GetWebhookLogsAsync(
        WebhookDeliveryLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _webhookLogRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            var eventType = request.EventType.Trim();
            query = query.Where(entity => entity.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(entity => entity.Status == status);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<WebhookDeliveryLogResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<PagedResult<ExternalApiCallLogResponse>> GetApiCallLogsAsync(
        ExternalApiCallLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _apiCallLogRepository.Query();
        if (request.ClientId.HasValue)
        {
            query = query.Where(entity => entity.ClientId == request.ClientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            var path = request.Path.Trim();
            query = query.Where(entity => entity.Path.Contains(path));
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var rows = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new
                {
                    entity.Id,
                    entity.TenantId,
                    entity.ClientId,
                    entity.Path,
                    entity.Method,
                    entity.IpAddress,
                    entity.StatusCode,
                    entity.ElapsedMilliseconds,
                    entity.CreatedAt
                }),
            cancellationToken);
        var clientIds = rows
            .Where(entity => entity.ClientId.HasValue)
            .Select(entity => entity.ClientId!.Value)
            .Distinct()
            .ToArray();
        var clientsById = clientIds.Length == 0
            ? new Dictionary<Guid, string>()
            : (await _asyncQueryExecutor.ToListAsync(
                    _clientRepository.Query()
                        .Where(entity => clientIds.Contains(entity.Id))
                        .Select(entity => new { entity.Id, entity.ClientCode }),
                    cancellationToken))
                .ToDictionary(entity => entity.Id, entity => entity.ClientCode);
        var items = rows.Select(entity => new ExternalApiCallLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ClientId = entity.ClientId,
            ClientCode = entity.ClientId.HasValue
                ? clientsById.GetValueOrDefault(entity.ClientId.Value)
                : null,
            Path = entity.Path,
            Method = entity.Method,
            IpAddress = entity.IpAddress,
            StatusCode = entity.StatusCode,
            ElapsedMilliseconds = entity.ElapsedMilliseconds,
            CreatedAt = entity.CreatedAt
        }).ToList();

        return PagedResult<ExternalApiCallLogResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    private async Task<ApiClient> GetClientOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _clientRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "API client was not found.");
    }

    private async Task<WebhookSubscription> GetWebhookOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _webhookRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Webhook subscription was not found.");
    }

    private static ApiClientValidationResult FailedValidation(string message)
    {
        return new ApiClientValidationResult
        {
            Succeeded = false,
            ErrorMessage = message
        };
    }

    private static string GenerateSecretValue()
    {
        return "ps_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashSecret(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static bool IsIpAllowed(string? allowedIpList, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(allowedIpList))
        {
            return true;
        }

        return IpAccessMatcher.AnyMatches(allowedIpList, ipAddress);
    }

    private Guid ResolveTenantId()
    {
        return _tenantContext.TenantId ?? Guid.Parse("10000000-0000-0000-0000-000000000001");
    }

    private bool ShouldUseResolvedTenantForApiKeyLookup()
    {
        return _tenantContext.TenantId.HasValue &&
            (_tenantContext.IsSuperAdmin ||
                string.Equals(_tenantContext.Source, "Header", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_tenantContext.Source, "Claims", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_tenantContext.Source, "ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCode(string value, string message)
    {
        return TrimRequired(value, message).Trim().ToUpperInvariant();
    }

    private static string NormalizeEventType(string value)
    {
        return TrimRequired(value, "Event type is required.").Trim().ToLowerInvariant();
    }

    private static string NormalizeHttpsUrl(string value)
    {
        var url = TrimRequired(value, "Target URL is required.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !IsLocalHttp(uri)))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Webhook target URL must be HTTPS. Local HTTP is allowed for development testing.");
        }

        return url;
    }

    private static bool IsLocalHttp(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp &&
            (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase));
    }

    private static int NormalizeRateLimit(int value)
    {
        return Math.Clamp(value, 0, 10000);
    }

    private static int NormalizeRetryCount(int value)
    {
        return Math.Clamp(value, 0, 10);
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

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string BuildPayloadLogSummary(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return $"[redacted] bytes={bytes.Length}; sha256={hash}";
    }

    private static string? BuildResponseLogSummary(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(responseBody);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return $"[redacted] bytes={bytes.Length}; sha256={hash}";
    }

    private static ApiClientResponse ToResponse(ApiClient entity)
    {
        return new ApiClientResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ClientCode = entity.ClientCode,
            ClientName = entity.ClientName,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            AllowedScopes = entity.AllowedScopes,
            AllowedIpList = entity.AllowedIpList,
            RateLimitPerMinute = entity.RateLimitPerMinute,
            CreatedAt = entity.CreatedAt
        };
    }

    private static WebhookSubscriptionResponse ToResponse(WebhookSubscription entity)
    {
        return new WebhookSubscriptionResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EventType = entity.EventType,
            TargetUrl = entity.TargetUrl,
            Secret = MaskedSecret,
            IsEnabled = entity.IsEnabled,
            RetryCount = entity.RetryCount,
            CreatedAt = entity.CreatedAt
        };
    }

    private static WebhookDeliveryLogResponse ToResponse(WebhookDeliveryLog entity)
    {
        return new WebhookDeliveryLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SubscriptionId = entity.SubscriptionId,
            EventType = entity.EventType,
            Payload = entity.Payload,
            Status = entity.Status,
            ResponseStatusCode = entity.ResponseStatusCode,
            ResponseBody = entity.ResponseBody,
            RetryCount = entity.RetryCount,
            CreatedAt = entity.CreatedAt
        };
    }

    private static ExternalApiCallLogResponse ToResponse(
        ExternalApiCallLog entity,
        IReadOnlyDictionary<Guid, string> clientsById)
    {
        return new ExternalApiCallLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ClientId = entity.ClientId,
            ClientCode = entity.ClientId.HasValue && clientsById.TryGetValue(entity.ClientId.Value, out var clientCode)
                ? clientCode
                : null,
            Path = entity.Path,
            Method = entity.Method,
            IpAddress = entity.IpAddress,
            StatusCode = entity.StatusCode,
            ElapsedMilliseconds = entity.ElapsedMilliseconds,
            CreatedAt = entity.CreatedAt
        };
    }
}
