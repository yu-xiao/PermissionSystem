using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Security;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Mcp;

public sealed partial class McpAdministrationService : IMcpAdministrationService
{
    private readonly IRepository<ApiClient> _apiClientRepository;
    private readonly IRepository<McpClientBinding> _bindingRepository;
    private readonly IRepository<McpDatasetDefinition> _datasetRepository;
    private readonly IRepository<McpDatasetField> _fieldRepository;
    private readonly IRepository<McpClientDatasetGrant> _grantRepository;
    private readonly IRepository<McpInvocationLog> _logRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly IMcpOAuthClientProvisioner _oauthProvisioner;
    private readonly IMcpDatasetProvisioner _datasetProvisioner;
    private readonly IUnitOfWork _unitOfWork;

    public McpAdministrationService(
        IRepository<ApiClient> apiClientRepository,
        IRepository<McpClientBinding> bindingRepository,
        IRepository<McpDatasetDefinition> datasetRepository,
        IRepository<McpDatasetField> fieldRepository,
        IRepository<McpClientDatasetGrant> grantRepository,
        IRepository<McpInvocationLog> logRepository,
        IAsyncQueryExecutor queryExecutor,
        ICurrentUserService currentUserService,
        ISecurityPolicyService securityPolicyService,
        IMcpOAuthClientProvisioner oauthProvisioner,
        IMcpDatasetProvisioner datasetProvisioner,
        IUnitOfWork unitOfWork)
    {
        _apiClientRepository = apiClientRepository;
        _bindingRepository = bindingRepository;
        _datasetRepository = datasetRepository;
        _fieldRepository = fieldRepository;
        _grantRepository = grantRepository;
        _logRepository = logRepository;
        _queryExecutor = queryExecutor;
        _currentUserService = currentUserService;
        _securityPolicyService = securityPolicyService;
        _oauthProvisioner = oauthProvisioner;
        _datasetProvisioner = datasetProvisioner;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<McpClientResponse>> GetClientsAsync(
        McpClientQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientViewPermission);
        var query =
            from binding in _bindingRepository.QueryForTenant(tenantId)
            join client in _apiClientRepository.QueryForTenant(tenantId)
                on binding.ApiClientId equals client.Id
            select new ClientRow(binding, client);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(row =>
                row.Client.ClientCode.Contains(keyword) ||
                row.Client.ClientName.Contains(keyword) ||
                row.Binding.OAuthClientId.Contains(keyword));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(row =>
                row.Binding.IsEnabled == request.IsEnabled.Value &&
                row.Client.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var rows = await _queryExecutor.ToListAsync(
            query.OrderByDescending(row => row.Binding.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize),
            cancellationToken);
        var items = new List<McpClientResponse>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await ToResponseAsync(row.Binding, row.Client, cancellationToken));
        }

        return PagedResult<McpClientResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<McpClientResponse> GetClientAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientViewPermission);
        var binding = await GetBindingAsync(id, tenantId, cancellationToken);
        var client = await GetApiClientAsync(binding.ApiClientId, tenantId, cancellationToken);
        return await ToResponseAsync(binding, client, cancellationToken);
    }

    public async Task<McpClientCredentialResponse> CreateClientAsync(
        CreateMcpClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientManagePermission);
        EnsureAccess(AiCenterConstants.McpClientSecretPermission);
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync(
            AiCenterConstants.McpClientCreateOperationCode,
            force: true,
            cancellationToken);
        await _datasetProvisioner.EnsureTenantDatasetsAsync(tenantId, cancellationToken);

        var clientCode = NormalizeClientCode(request.ClientCode);
        if (await _queryExecutor.AnyAsync(
                _apiClientRepository.QueryForTenant(tenantId).Where(entity => entity.ClientCode == clientCode),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "An API client with the same code already exists.");
        }

        var scopes = NormalizeScopes(request.AllowedScopes);
        var ipList = NormalizeRequired(request.AllowedIpList, "At least one allowed IP pattern is required.", 1000);
        var grants = await ValidateGrantsAsync(tenantId, request.DatasetGrants, cancellationToken);
        var oauthClientId = $"mcp-{Guid.NewGuid():N}";
        var clientSecret = GenerateSecret();
        McpClientBinding? createdBinding = null;
        ApiClient? createdClient = null;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            createdClient = new ApiClient
            {
                TenantId = tenantId,
                ClientCode = clientCode,
                ClientName = NormalizeRequired(request.ClientName, "Client name is required.", 200),
                Description = NormalizeOptional(request.Description, 500),
                IsEnabled = true,
                AllowedScopes = string.Join(',', scopes),
                AllowedIpList = ipList,
                RateLimitPerMinute = NormalizeRateLimit(request.RateLimitPerMinute)
            };
            await _apiClientRepository.AddAsync(createdClient, token);
            await _unitOfWork.SaveChangesAsync(token);

            createdBinding = new McpClientBinding
            {
                TenantId = tenantId,
                ApiClientId = createdClient.Id,
                OAuthClientId = oauthClientId,
                IsEnabled = true
            };
            await _bindingRepository.AddAsync(createdBinding, token);
            await _unitOfWork.SaveChangesAsync(token);
            await ReplaceGrantsAsync(createdBinding.Id, tenantId, grants, token);
            await _oauthProvisioner.CreateAsync(new McpOAuthClientRegistration
            {
                ClientId = oauthClientId,
                ClientSecret = clientSecret,
                DisplayName = createdClient.ClientName
            }, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return new McpClientCredentialResponse
        {
            Client = await ToResponseAsync(createdBinding!, createdClient!, cancellationToken),
            ClientSecret = clientSecret
        };
    }

    public async Task<McpClientResponse> UpdateClientAsync(
        Guid id,
        UpdateMcpClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientManagePermission);
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync(
            AiCenterConstants.McpClientUpdateOperationCode,
            force: true,
            cancellationToken);
        var binding = await GetBindingAsync(id, tenantId, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(binding, request.ConcurrencyToken);
        var client = await GetApiClientAsync(binding.ApiClientId, tenantId, cancellationToken);
        var grants = await ValidateGrantsAsync(tenantId, request.DatasetGrants, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            client.ClientName = NormalizeRequired(request.ClientName, "Client name is required.", 200);
            client.Description = NormalizeOptional(request.Description, 500);
            client.AllowedScopes = string.Join(',', NormalizeScopes(request.AllowedScopes));
            client.AllowedIpList = NormalizeRequired(
                request.AllowedIpList,
                "At least one allowed IP pattern is required.",
                1000);
            client.RateLimitPerMinute = NormalizeRateLimit(request.RateLimitPerMinute);
            client.UpdatedBy = _currentUserService.UserId;
            _apiClientRepository.Update(client);
            await ReplaceGrantsAsync(binding.Id, tenantId, grants, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return await ToResponseAsync(binding, client, cancellationToken);
    }

    public async Task<McpClientCredentialResponse> RotateSecretAsync(
        Guid id,
        RotateMcpClientSecretRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientSecretPermission);
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync(
            AiCenterConstants.McpClientSecretOperationCode,
            force: true,
            cancellationToken);
        var binding = await GetBindingAsync(id, tenantId, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(binding, request.ConcurrencyToken);
        var client = await GetApiClientAsync(binding.ApiClientId, tenantId, cancellationToken);
        var secret = GenerateSecret();
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _oauthProvisioner.RotateSecretAsync(new McpOAuthClientRegistration
            {
                ClientId = binding.OAuthClientId,
                ClientSecret = secret,
                DisplayName = client.ClientName
            }, token);
            binding.UpdatedBy = _currentUserService.UserId;
            _bindingRepository.Update(binding);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return new McpClientCredentialResponse
        {
            Client = await ToResponseAsync(binding, client, cancellationToken),
            ClientSecret = secret
        };
    }

    public async Task<McpClientResponse> SetEnabledAsync(
        Guid id,
        SetMcpClientEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientManagePermission);
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync(
            AiCenterConstants.McpClientStatusOperationCode,
            force: true,
            cancellationToken);
        var binding = await GetBindingAsync(id, tenantId, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(binding, request.ConcurrencyToken);
        var client = await GetApiClientAsync(binding.ApiClientId, tenantId, cancellationToken);

        binding.IsEnabled = request.IsEnabled;
        binding.UpdatedBy = _currentUserService.UserId;
        client.IsEnabled = request.IsEnabled;
        client.UpdatedBy = _currentUserService.UserId;
        _bindingRepository.Update(binding);
        _apiClientRepository.Update(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(binding, client, cancellationToken);
    }

    public async Task<IReadOnlyList<McpDatasetResponse>> GetDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpClientViewPermission);
        var datasets = await _queryExecutor.ToListAsync(
            _datasetRepository.QueryForTenant(tenantId)
                .Where(entity => entity.IsEnabled)
                .OrderBy(entity => entity.DatasetCode),
            cancellationToken);
        var result = new List<McpDatasetResponse>(datasets.Count);
        foreach (var dataset in datasets)
        {
            var fields = await GetFieldsAsync(tenantId, dataset.Id, cancellationToken);
            result.Add(ToDatasetResponse(dataset, fields));
        }

        return result;
    }

    public async Task<PagedResult<McpInvocationLogResponse>> GetInvocationLogsAsync(
        McpInvocationLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EnsureAccess(AiCenterConstants.McpAuditViewPermission);
        var query = _logRepository.QueryForTenant(tenantId);
        if (request.ClientBindingId.HasValue)
        {
            query = query.Where(entity => entity.ClientBindingId == request.ClientBindingId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.DatasetCode))
        {
            var datasetCode = request.DatasetCode.Trim();
            query = query.Where(entity => entity.DatasetCode == datasetCode);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(entity => entity.Status == request.Status.Value);
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var rows = await _queryExecutor.ToListAsync(
            query.OrderByDescending(entity => entity.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize),
            cancellationToken);
        return PagedResult<McpInvocationLogResponse>.Create(
            rows.Select(ToLogResponse).ToList(),
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    private Guid EnsureAccess(string permission)
    {
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.TenantId.HasValue ||
            !_currentUserService.UserId.HasValue)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "A user and tenant context is required.");
        }

        if (!_currentUserService.HasPermission(permission))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to manage MCP clients.");
        }

        return _currentUserService.TenantId.Value;
    }

    private async Task<McpClientBinding> GetBindingAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _queryExecutor.FirstOrDefaultAsync(
            _bindingRepository.QueryForTenant(tenantId).Where(entity => entity.Id == id),
            cancellationToken) ?? throw new BusinessException(ErrorCode.NotFound, "The MCP client was not found.");
    }

    private async Task<ApiClient> GetApiClientAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _queryExecutor.FirstOrDefaultAsync(
            _apiClientRepository.QueryForTenant(tenantId).Where(entity => entity.Id == id),
            cancellationToken) ?? throw new BusinessException(ErrorCode.NotFound, "The API client binding was not found.");
    }

    private async Task<IReadOnlyList<ValidatedGrant>> ValidateGrantsAsync(
        Guid tenantId,
        IReadOnlyList<McpDatasetGrantRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0 || requests.Any(request => request.DatasetId == Guid.Empty))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "At least one valid dataset grant is required.");
        }

        if (requests.Select(request => request.DatasetId).Distinct().Count() != requests.Count)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Dataset grants cannot contain duplicates.");
        }

        var grants = new List<ValidatedGrant>(requests.Count);
        foreach (var request in requests)
        {
            var dataset = await _queryExecutor.FirstOrDefaultAsync(
                _datasetRepository.QueryForTenant(tenantId).Where(entity =>
                    entity.Id == request.DatasetId && entity.IsEnabled),
                cancellationToken) ?? throw new BusinessException(ErrorCode.ValidationFailed, "The selected dataset is unavailable.");
            var fields = await GetFieldsAsync(tenantId, dataset.Id, cancellationToken);
            var allowed = request.AllowedFields
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(field => field.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(field => field, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (allowed.Length == 0 || allowed.Any(field =>
                    fields.All(definition => !string.Equals(definition.FieldCode, field, StringComparison.OrdinalIgnoreCase))))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Dataset grants contain unavailable fields.");
            }

            grants.Add(new ValidatedGrant(dataset, allowed));
        }

        return grants;
    }

    private async Task ReplaceGrantsAsync(
        Guid bindingId,
        Guid tenantId,
        IReadOnlyList<ValidatedGrant> grants,
        CancellationToken cancellationToken)
    {
        var existing = await _queryExecutor.ToListAsync(
            _grantRepository.QueryForTenant(tenantId).Where(entity => entity.ClientBindingId == bindingId),
            cancellationToken);
        foreach (var grant in existing)
        {
            _grantRepository.Remove(grant);
        }

        foreach (var grant in grants)
        {
            await _grantRepository.AddAsync(new McpClientDatasetGrant
            {
                TenantId = tenantId,
                ClientBindingId = bindingId,
                DatasetId = grant.Dataset.Id,
                AllowedFieldsJson = JsonSerializer.Serialize(grant.AllowedFields),
                IsEnabled = true
            }, cancellationToken);
        }
    }

    private async Task<McpClientResponse> ToResponseAsync(
        McpClientBinding binding,
        ApiClient client,
        CancellationToken cancellationToken)
    {
        var grantEntities = await _queryExecutor.ToListAsync(
            _grantRepository.QueryForTenant(binding.TenantId)
                .Where(entity => entity.ClientBindingId == binding.Id && entity.IsEnabled),
            cancellationToken);
        var datasets = await _queryExecutor.ToListAsync(
            _datasetRepository.QueryForTenant(binding.TenantId)
                .Where(entity => grantEntities.Select(grant => grant.DatasetId).Contains(entity.Id)),
            cancellationToken);
        var grants = grantEntities.Select(grant =>
        {
            var dataset = datasets.First(entity => entity.Id == grant.DatasetId);
            return new McpClientDatasetGrantResponse
            {
                DatasetId = dataset.Id,
                DatasetCode = dataset.DatasetCode,
                DatasetName = dataset.DatasetName,
                AllowedFields = DeserializeFields(grant.AllowedFieldsJson)
            };
        }).ToList();

        return new McpClientResponse
        {
            Id = binding.Id,
            ApiClientId = client.Id,
            OAuthClientId = binding.OAuthClientId,
            ClientCode = client.ClientCode,
            ClientName = client.ClientName,
            Description = client.Description,
            IsEnabled = binding.IsEnabled && client.IsEnabled,
            AllowedScopes = ParseScopes(client.AllowedScopes),
            AllowedIpList = client.AllowedIpList ?? string.Empty,
            RateLimitPerMinute = client.RateLimitPerMinute,
            DatasetGrants = grants,
            ConcurrencyToken = binding.RowVersion,
            CreatedAt = binding.CreatedAt
        };
    }

    private Task<IReadOnlyList<McpDatasetField>> GetFieldsAsync(
        Guid tenantId,
        Guid datasetId,
        CancellationToken cancellationToken)
    {
        return _queryExecutor.ToListAsync(
            _fieldRepository.QueryForTenant(tenantId)
                .Where(entity => entity.DatasetId == datasetId)
                .OrderBy(entity => entity.FieldCode),
            cancellationToken);
    }

    private static McpDatasetResponse ToDatasetResponse(
        McpDatasetDefinition dataset,
        IReadOnlyList<McpDatasetField> fields) => new()
        {
            Id = dataset.Id,
            DatasetCode = dataset.DatasetCode,
            DatasetName = dataset.DatasetName,
            Version = dataset.Version,
            Description = dataset.Description,
            DataClassification = dataset.DataClassification,
            MaxRows = dataset.MaxRows,
            Fields = fields.Select(field => new McpDatasetFieldResponse
            {
                FieldCode = field.FieldCode,
                DisplayName = field.DisplayName,
                DataType = field.DataType,
                DataClassification = field.DataClassification,
                IsFilterable = field.IsFilterable,
                IsDefault = field.IsDefault
            }).ToList()
        };

    private static McpInvocationLogResponse ToLogResponse(McpInvocationLog entity) => new()
    {
        Id = entity.Id,
        CallerType = entity.CallerType,
        ClientBindingId = entity.ClientBindingId,
        OAuthClientId = entity.OAuthClientId,
        ToolName = entity.ToolName,
        DatasetCode = entity.DatasetCode,
        TraceId = entity.TraceId,
        IpAddress = entity.IpAddress,
        Status = entity.Status,
        RowCount = entity.RowCount,
        IsTruncated = entity.IsTruncated,
        DurationMilliseconds = entity.DurationMilliseconds,
        ErrorCode = entity.ErrorCode,
        ErrorSummary = entity.ErrorSummary,
        CreatedAt = entity.CreatedAt
    };

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string> scopes)
    {
        var normalized = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0 || normalized.Any(scope =>
                !McpToolScopes.All.Contains(scope, StringComparer.OrdinalIgnoreCase)))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "MCP client scopes are invalid.");
        }

        return normalized;
    }

    private static IReadOnlyList<string> ParseScopes(string? scopes)
    {
        return string.IsNullOrWhiteSpace(scopes)
            ? []
            : scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> DeserializeFields(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeClientCode(string value)
    {
        var normalized = NormalizeRequired(value, "Client code is required.", 40).ToLowerInvariant();
        if (!ClientCodePattern().IsMatch(normalized))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "Client code must contain 3-40 lowercase letters, digits, dots, underscores, or hyphens.");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static int NormalizeRateLimit(int value)
    {
        if (value is < 1 or > 1000)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Rate limit must be between 1 and 1000 per minute.");
        }

        return value;
    }

    private static string GenerateSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex ClientCodePattern();

    private sealed record ClientRow(McpClientBinding Binding, ApiClient Client);

    private sealed record ValidatedGrant(McpDatasetDefinition Dataset, IReadOnlyList<string> AllowedFields);
}
