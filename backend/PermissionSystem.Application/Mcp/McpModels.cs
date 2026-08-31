using System.Text.Json;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Mcp;

public static class McpToolScopes
{
    public const string DatasetList = "mcp:dataset:list";
    public const string DatasetDescribe = "mcp:dataset:describe";
    public const string DatasetQuery = "mcp:dataset:query";

    public static IReadOnlyCollection<string> All { get; } =
        [DatasetList, DatasetDescribe, DatasetQuery];
}

public static class McpDatasetCodes
{
    public const string PlatformCapabilities = "platform-capabilities";
    public const string DepartmentDirectory = "department-directory";
}

public sealed class McpServiceClientRecord
{
    public Guid TenantId { get; init; }

    public Guid ClientBindingId { get; init; }

    public Guid ApiClientId { get; init; }

    public string OAuthClientId { get; init; } = string.Empty;

    public string ClientCode { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public string? AllowedScopes { get; init; }

    public string? AllowedIpList { get; init; }

    public int RateLimitPerMinute { get; init; }
}

public sealed class McpCallerAdmissionResult
{
    public bool Succeeded { get; init; }

    public bool IsRateLimited { get; init; }

    public TimeSpan RetryAfter { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public McpServiceClientRecord? Client { get; init; }
}

public sealed class McpDatasetQueryRequest
{
    public IReadOnlyList<string> Fields { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> Filters { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

    public int? Limit { get; init; }
}

public sealed class McpDatasetFieldResponse
{
    public string FieldCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public string DataClassification { get; init; } = string.Empty;

    public bool IsFilterable { get; init; }

    public bool IsDefault { get; init; }
}

public sealed class McpDatasetResponse
{
    public Guid Id { get; init; }

    public string DatasetCode { get; init; } = string.Empty;

    public string DatasetName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string DataClassification { get; init; } = string.Empty;

    public int MaxRows { get; init; }

    public bool IsEnabled { get; init; }

    public string SchemaHash { get; init; } = string.Empty;

    public McpDatasetPublicationStatus PublicationStatus { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public IReadOnlyList<McpDatasetFieldResponse> Fields { get; init; } = [];
}

public sealed class McpDatasetQueryResponse
{
    public string DatasetCode { get; init; } = string.Empty;

    public string DatasetVersion { get; init; } = string.Empty;

    public string SchemaVersion { get; init; } = "1.0";

    public string SchemaHash { get; init; } = string.Empty;

    public IReadOnlyList<string> Fields { get; init; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];

    public int RowCount { get; init; }

    public bool IsTruncated { get; init; }

    public DateTimeOffset QueriedAt { get; init; }

    public string TraceId { get; init; } = string.Empty;
}

public sealed class McpClientQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateMcpClientRequest
{
    public string ClientCode { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    public string AllowedIpList { get; init; } = string.Empty;

    public int RateLimitPerMinute { get; init; } = 60;

    public IReadOnlyList<McpDatasetGrantRequest> DatasetGrants { get; init; } = [];
}

public sealed class UpdateMcpClientRequest
{
    public string ClientName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    public string AllowedIpList { get; init; } = string.Empty;

    public int RateLimitPerMinute { get; init; } = 60;

    public IReadOnlyList<McpDatasetGrantRequest> DatasetGrants { get; init; } = [];

    public byte[]? ConcurrencyToken { get; init; }
}

public sealed class McpDatasetGrantRequest
{
    public Guid DatasetId { get; init; }

    public IReadOnlyList<string> AllowedFields { get; init; } = [];
}

public sealed class SetMcpClientEnabledRequest
{
    public bool IsEnabled { get; init; }

    public byte[]? ConcurrencyToken { get; init; }
}

public sealed class RotateMcpClientSecretRequest
{
    public byte[]? ConcurrencyToken { get; init; }
}

public sealed class McpClientResponse
{
    public Guid Id { get; init; }

    public Guid ApiClientId { get; init; }

    public string OAuthClientId { get; init; } = string.Empty;

    public string ClientCode { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    public string AllowedIpList { get; init; } = string.Empty;

    public int RateLimitPerMinute { get; init; }

    public IReadOnlyList<McpClientDatasetGrantResponse> DatasetGrants { get; init; } = [];

    public byte[] ConcurrencyToken { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class McpClientDatasetGrantResponse
{
    public Guid DatasetId { get; init; }

    public string DatasetCode { get; init; } = string.Empty;

    public string DatasetName { get; init; } = string.Empty;

    public string DatasetVersion { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedFields { get; init; } = [];

    public string ApprovedSchemaHash { get; init; } = string.Empty;

    public string CurrentSchemaHash { get; init; } = string.Empty;

    public bool IsSchemaCurrent { get; init; }
}

public sealed class McpClientCredentialResponse
{
    public McpClientResponse Client { get; init; } = new();

    public string ClientSecret { get; init; } = string.Empty;
}

public sealed class McpInvocationLogQueryRequest : PaginationRequest
{
    public Guid? ClientBindingId { get; init; }

    public string? DatasetCode { get; init; }

    public McpInvocationStatus? Status { get; init; }
}

public sealed class McpInvocationLogResponse
{
    public Guid Id { get; init; }

    public McpCallerType CallerType { get; init; }

    public Guid? ClientBindingId { get; init; }

    public string? OAuthClientId { get; init; }

    public string ToolName { get; init; } = string.Empty;

    public string? DatasetCode { get; init; }

    public string TraceId { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public McpInvocationStatus Status { get; init; }

    public int RowCount { get; init; }

    public bool IsTruncated { get; init; }

    public long DurationMilliseconds { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorSummary { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class McpOAuthClientRegistration
{
    public string ClientId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;
}

public interface IMcpClientBindingStore
{
    Task<McpServiceClientRecord?> FindByOAuthClientIdAsync(
        string oauthClientId,
        CancellationToken cancellationToken = default);
}

public interface IMcpOAuthClientProvisioner
{
    Task CreateAsync(McpOAuthClientRegistration registration, CancellationToken cancellationToken = default);

    Task RotateSecretAsync(McpOAuthClientRegistration registration, CancellationToken cancellationToken = default);
}

public interface IMcpClientAccessService
{
    Task<McpCallerAdmissionResult> ValidateTokenRequestAsync(
        string oauthClientId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<McpCallerAdmissionResult> AdmitRequestAsync(
        string oauthClientId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

public interface IMcpDatasetService
{
    Task<IReadOnlyList<McpDatasetResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<McpDatasetResponse> DescribeAsync(string datasetCode, CancellationToken cancellationToken = default);

    Task<McpDatasetQueryResponse> QueryAsync(
        string datasetCode,
        McpDatasetQueryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMcpAdministrationService
{
    Task<PagedResult<McpClientResponse>> GetClientsAsync(
        McpClientQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<McpClientResponse> GetClientAsync(Guid id, CancellationToken cancellationToken = default);

    Task<McpClientCredentialResponse> CreateClientAsync(
        CreateMcpClientRequest request,
        CancellationToken cancellationToken = default);

    Task<McpClientResponse> UpdateClientAsync(
        Guid id,
        UpdateMcpClientRequest request,
        CancellationToken cancellationToken = default);

    Task<McpClientCredentialResponse> RotateSecretAsync(
        Guid id,
        RotateMcpClientSecretRequest request,
        CancellationToken cancellationToken = default);

    Task<McpClientResponse> SetEnabledAsync(
        Guid id,
        SetMcpClientEnabledRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpDatasetResponse>> GetDatasetsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<McpInvocationLogResponse>> GetInvocationLogsAsync(
        McpInvocationLogQueryRequest request,
        CancellationToken cancellationToken = default);
}
