using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Integration;

public sealed class ApiClientQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateApiClientRequest
{
    public string ClientCode { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string? AllowedScopes { get; init; }

    public string? AllowedIpList { get; init; }

    public int RateLimitPerMinute { get; init; } = 60;
}

public sealed class UpdateApiClientRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string ClientName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? AllowedScopes { get; init; }

    public string? AllowedIpList { get; init; }

    public int RateLimitPerMinute { get; init; } = 60;
}

public sealed class ApiClientResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ClientCode { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    public string? AllowedScopes { get; init; }

    public string? AllowedIpList { get; init; }

    public int RateLimitPerMinute { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class GenerateApiClientSecretResponse
{
    public Guid ClientId { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public string ApiSecret { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class ApiClientValidationResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? ClientId { get; init; }

    public string? ClientCode { get; init; }

    public string? AllowedScopes { get; init; }

    public int RateLimitPerMinute { get; init; }
}

public sealed class WebhookQueryRequest : PaginationRequest
{
    public string? EventType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateWebhookSubscriptionRequest
{
    public string EventType { get; init; } = string.Empty;

    public string TargetUrl { get; init; } = string.Empty;

    public string? Secret { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int RetryCount { get; init; } = 3;
}

public sealed class UpdateWebhookSubscriptionRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string TargetUrl { get; init; } = string.Empty;

    public string? Secret { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int RetryCount { get; init; } = 3;
}

public sealed class WebhookSubscriptionResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string TargetUrl { get; init; } = string.Empty;

    public string Secret { get; init; } = "******";

    public bool IsEnabled { get; init; }

    public int RetryCount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class WebhookDeliveryLogQueryRequest : PaginationRequest
{
    public string? EventType { get; init; }

    public string? Status { get; init; }
}

public sealed class WebhookDeliveryLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid SubscriptionId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string Payload { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int? ResponseStatusCode { get; init; }

    public string? ResponseBody { get; init; }

    public int RetryCount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ExternalApiCallLogQueryRequest : PaginationRequest
{
    public Guid? ClientId { get; init; }

    public string? Path { get; init; }
}

public sealed class ExternalApiCallLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? ClientId { get; init; }

    public string? ClientCode { get; init; }

    public string Path { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public int StatusCode { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class RecordExternalApiCallRequest
{
    public Guid TenantId { get; init; }

    public Guid? ClientId { get; init; }

    public string Path { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public int StatusCode { get; init; }

    public long ElapsedMilliseconds { get; init; }
}

public sealed class WebhookSendResult
{
    public bool Succeeded { get; init; }

    public int? StatusCode { get; init; }

    public string? ResponseBody { get; init; }
}

public interface IApiClientContext
{
    Guid? ClientId { get; }

    string? ClientCode { get; }

    string? AllowedScopes { get; }

    bool IsAuthenticated { get; }

    void SetClient(Guid clientId, string clientCode, string? allowedScopes);
}

public interface IOpenIntegrationService
{
    Task<PagedResult<ApiClientResponse>> GetClientsAsync(ApiClientQueryRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResponse> CreateClientAsync(CreateApiClientRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResponse> UpdateClientAsync(Guid id, UpdateApiClientRequest request, CancellationToken cancellationToken = default);

    Task DeleteClientAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GenerateApiClientSecretResponse> GenerateSecretAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetClientEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default);

    Task<ApiClientValidationResult> ValidateApiClientAsync(
        string apiKey,
        string apiSecret,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task RecordExternalApiCallAsync(RecordExternalApiCallRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<WebhookSubscriptionResponse>> GetWebhooksAsync(WebhookQueryRequest request, CancellationToken cancellationToken = default);

    Task<WebhookSubscriptionResponse> CreateWebhookAsync(CreateWebhookSubscriptionRequest request, CancellationToken cancellationToken = default);

    Task<WebhookSubscriptionResponse> UpdateWebhookAsync(Guid id, UpdateWebhookSubscriptionRequest request, CancellationToken cancellationToken = default);

    Task DeleteWebhookAsync(Guid id, CancellationToken cancellationToken = default);

    Task TestWebhookAsync(Guid id, CancellationToken cancellationToken = default);

    Task PublishWebhookAsync(string eventType, object payload, CancellationToken cancellationToken = default);

    Task DeliverWebhookAsync(Guid subscriptionId, string eventType, string payload, int attempt, CancellationToken cancellationToken = default);

    Task<PagedResult<WebhookDeliveryLogResponse>> GetWebhookLogsAsync(WebhookDeliveryLogQueryRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<ExternalApiCallLogResponse>> GetApiCallLogsAsync(ExternalApiCallLogQueryRequest request, CancellationToken cancellationToken = default);
}

public interface IWebhookHttpSender
{
    Task<WebhookSendResult> SendAsync(
        string targetUrl,
        string eventType,
        string payload,
        string secret,
        CancellationToken cancellationToken = default);
}
