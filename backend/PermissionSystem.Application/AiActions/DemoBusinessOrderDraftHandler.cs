using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiActions;

public sealed class DemoBusinessOrderDraftHandler : IAiBusinessActionHandler, IAiDocumentDraftService
{
    private const decimal MaxAmount = 9999999999999999.99m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<AiDocumentDraft> _draftRepository;
    private readonly IRepository<AiDocumentDraftValidation> _validationRepository;
    private readonly IRepository<AiDocumentExecution> _executionRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiCenterConfiguration _aiConfiguration;
    private readonly IAiDraftConfiguration _draftConfiguration;

    public DemoBusinessOrderDraftHandler(
        IRepository<AiDocumentDraft> draftRepository,
        IRepository<AiDocumentDraftValidation> validationRepository,
        IRepository<AiDocumentExecution> executionRepository,
        IRepository<Department> departmentRepository,
        IAsyncQueryExecutor queryExecutor,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAiCenterConfiguration aiConfiguration,
        IAiDraftConfiguration draftConfiguration)
    {
        _draftRepository = draftRepository;
        _validationRepository = validationRepository;
        _executionRepository = executionRepository;
        _departmentRepository = departmentRepository;
        _queryExecutor = queryExecutor;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _aiConfiguration = aiConfiguration;
        _draftConfiguration = draftConfiguration;
    }

    public string BusinessType => DemoBusinessOrderConstants.BusinessType;

    public string HandlerVersion => AiBusinessActionConstants.DemoBusinessOrderHandlerVersion;

    public AiToolDefinition ToolDefinition { get; } = new()
    {
        ToolCode = AiBusinessActionConstants.DemoBusinessOrderToolCode,
        FunctionName = AiBusinessActionConstants.DemoBusinessOrderFunctionName,
        Version = "1.0",
        DisplayName = "Prepare Demo business order draft",
        Description = "Prepare one DemoBusinessOrder draft. Omit unknown fields instead of guessing. This tool never creates a formal business order.",
        DataClassification = "Internal",
        DataScopePolicy = AiToolDataScopePolicies.ActorOwnedDraft,
        RequiredPermissions =
        [
            AiCenterConstants.DocumentDraftPermission,
            "demo-business-order:create"
        ],
        TimeoutSeconds = 60,
        MaxRows = 1,
        InputSchemaJson = """{"type":"object","required":["title","customerName","amount"],"properties":{"title":{"type":"string","description":"Business order title supplied by the user.","maxLength":200},"customerName":{"type":"string","description":"Customer display name. This is free text because DemoBusinessOrder has no customer master-data relation.","maxLength":200},"amount":{"type":"number","description":"Non-negative total amount with at most two decimal places.","minimum":0,"maximum":9999999999999999.99,"multipleOf":0.01},"departmentId":{"type":"string","description":"Optional identifier of an enabled department in the current tenant.","format":"uuid"},"departmentReference":{"type":"string","description":"Optional exact department code or name. Omit when unknown and never guess.","maxLength":200}},"additionalProperties":false}""",
        OutputSchemaJson = """{"type":"object","required":["type","draft","instruction"],"properties":{"type":{"const":"document_draft"},"draft":{"type":"object"},"instruction":{"type":"string"}},"additionalProperties":false}"""
    };

    public async Task<AiActionToolExecutionResult> PrepareDraftAsync(
        AiActionDraftContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        if (context.TenantId != identity.TenantId || context.ActorUserId != identity.UserId ||
            string.IsNullOrWhiteSpace(context.InvocationId))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The AI action context is invalid.");
        }

        var existing = await _queryExecutor.FirstOrDefaultAsync(
            _draftRepository.Query().Where(entity =>
                entity.TenantId == identity.TenantId && entity.SourceInvocationId == context.InvocationId),
            cancellationToken);
        if (existing is not null)
        {
            var existingResponse = await ToResponseAsync(existing, cancellationToken);
            return CreateToolResult(existingResponse);
        }

        PrepareDemoBusinessOrderDraftRequest request;
        try
        {
            request = JsonSerializer.Deserialize<PrepareDemoBusinessOrderDraftRequest>(
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson,
                JsonOptions) ?? new PrepareDemoBusinessOrderDraftRequest();
        }
        catch (JsonException exception)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI draft arguments are invalid.", exception);
        }

        var validation = await NormalizeAndValidateAsync(request, identity.TenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var draft = new AiDocumentDraft
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            CreatedBy = identity.UserId,
            ConversationId = context.ConversationId,
            RunId = context.RunId,
            SourceInvocationId = context.InvocationId,
            ActorUserId = identity.UserId,
            BusinessType = BusinessType,
            HandlerVersion = HandlerVersion,
            Status = validation.Status,
            DraftVersion = 1,
            PayloadJson = SerializePayload(validation.Payload),
            PayloadHash = ComputePayloadHash(validation.Payload),
            ExpiresAt = now.AddMinutes(_draftConfiguration.DraftExpirationMinutes),
            LastValidatedAt = now
        };
        await _draftRepository.AddAsync(draft, cancellationToken);
        await AddValidationAsync(draft, validation.Errors, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = ToResponse(draft, validation.Payload, validation.Errors);
        return CreateToolResult(response);
    }

    public Task<AiBusinessActionSchemaResponse> GetDemoBusinessOrderSchemaAsync(
        CancellationToken cancellationToken = default)
    {
        _ = EnsureAccess();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AiBusinessActionSchemaResponse
        {
            BusinessType = BusinessType,
            HandlerVersion = HandlerVersion,
            InputSchemaJson = ToolDefinition.InputSchemaJson
        });
    }

    public async Task<AiDocumentDraftResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        var draft = await GetOwnedDraftAsync(id, identity.UserId, identity.TenantId, cancellationToken);
        return await ToResponseAsync(draft, cancellationToken);
    }

    public async Task<IReadOnlyList<AiDocumentDraftResponse>> GetByConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        var drafts = await _queryExecutor.ToListAsync(
            _draftRepository.Query()
                .Where(entity =>
                    entity.TenantId == identity.TenantId &&
                    entity.ConversationId == conversationId &&
                    entity.ActorUserId == identity.UserId)
                .OrderBy(entity => entity.CreatedAt),
            cancellationToken);
        return await ToResponsesAsync(drafts, cancellationToken);
    }

    public async Task<IReadOnlyList<AiDocumentDraftResponse>> GetByRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        var drafts = await _queryExecutor.ToListAsync(
            _draftRepository.Query()
                .Where(entity =>
                    entity.TenantId == identity.TenantId &&
                    entity.RunId == runId &&
                    entity.ActorUserId == identity.UserId)
                .OrderBy(entity => entity.CreatedAt),
            cancellationToken);
        return await ToResponsesAsync(drafts, cancellationToken);
    }

    public async Task<AiDocumentDraftResponse> UpdateAsync(
        Guid id,
        UpdateAiDocumentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        var draft = await GetOwnedDraftAsync(id, identity.UserId, identity.TenantId, cancellationToken);
        EnsureEditable(draft);
        EnsureConcurrencyToken(request.ConcurrencyToken);
        ConcurrencyTokenGuard.EnsureMatches(draft, request.ConcurrencyToken);

        var validation = await NormalizeAndValidateAsync(request, identity.TenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        draft.DraftVersion++;
        draft.Status = validation.Status;
        draft.PayloadJson = SerializePayload(validation.Payload);
        draft.PayloadHash = ComputePayloadHash(validation.Payload);
        draft.ExpiresAt = now.AddMinutes(_draftConfiguration.DraftExpirationMinutes);
        draft.LastValidatedAt = now;
        draft.UpdatedBy = identity.UserId;
        _draftRepository.Update(draft);
        await AddValidationAsync(draft, validation.Errors, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(draft, validation.Payload, validation.Errors);
    }

    public async Task<AiDocumentDraftResponse> CancelAsync(
        Guid id,
        CancelAiDocumentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        var draft = await GetOwnedDraftAsync(id, identity.UserId, identity.TenantId, cancellationToken);
        if (draft.Status == AiDocumentDraftStatus.Cancelled)
        {
            return await ToResponseAsync(draft, cancellationToken);
        }

        if (draft.Status == AiDocumentDraftStatus.Executed)
        {
            throw new BusinessException(ErrorCode.Conflict, "Executed AI document drafts cannot be cancelled.");
        }

        EnsureNotExpired(draft);
        EnsureConcurrencyToken(request.ConcurrencyToken);
        ConcurrencyTokenGuard.EnsureMatches(draft, request.ConcurrencyToken);
        draft.Status = AiDocumentDraftStatus.Cancelled;
        draft.UpdatedBy = identity.UserId;
        _draftRepository.Update(draft);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(draft, cancellationToken);
    }

    private async Task<ValidationResult> NormalizeAndValidateAsync(
        PrepareDemoBusinessOrderDraftRequest request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var errors = new List<AiDraftValidationError>();
        var title = NormalizeText(request.Title);
        var customerName = NormalizeText(request.CustomerName);
        var departmentReference = NormalizeText(request.DepartmentReference);

        ValidateRequiredText(title, nameof(request.Title), 200, "Title", errors);
        ValidateRequiredText(customerName, nameof(request.CustomerName), 200, "Customer name", errors);

        if (!request.Amount.HasValue)
        {
            AddError(errors, nameof(request.Amount), "required", "Amount is required.");
        }
        else if (request.Amount < 0 || request.Amount > MaxAmount || decimal.Round(request.Amount.Value, 2) != request.Amount.Value)
        {
            AddError(errors, nameof(request.Amount), "invalid", "Amount must be between 0 and 9999999999999999.99 with at most two decimal places.");
        }

        if (departmentReference?.Length > 200)
        {
            AddError(errors, nameof(request.DepartmentReference), "max_length", "Department reference cannot exceed 200 characters.");
        }

        Department? department = null;
        var hasDepartmentInput = request.DepartmentId.HasValue || !string.IsNullOrWhiteSpace(departmentReference);
        if (hasDepartmentInput && !_currentUserService.HasPermission("system:department:view"))
        {
            AddError(errors, nameof(request.DepartmentReference), "forbidden", "Current user is not allowed to resolve departments.");
        }
        else if (request.DepartmentId.HasValue)
        {
            department = await _queryExecutor.FirstOrDefaultAsync(
                _departmentRepository.Query().Where(entity =>
                    entity.Id == request.DepartmentId.Value && entity.TenantId == tenantId && entity.IsEnabled),
                cancellationToken);
            if (department is null)
            {
                AddError(errors, nameof(request.DepartmentId), "not_found", "The selected department is unavailable in the current tenant.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(departmentReference) && departmentReference.Length <= 200)
        {
            var activeDepartments = await _queryExecutor.ToListAsync(
                _departmentRepository.Query().Where(entity => entity.TenantId == tenantId && entity.IsEnabled),
                cancellationToken);
            var matches = activeDepartments
                .Where(entity => string.Equals(entity.Code, departmentReference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entity.Name, departmentReference, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entity => entity.Code)
                .ToList();
            if (matches.Count == 1)
            {
                department = matches[0];
            }
            else
            {
                errors.Add(new AiDraftValidationError
                {
                    Field = nameof(request.DepartmentReference),
                    Code = matches.Count == 0 ? "not_found" : "ambiguous",
                    Message = matches.Count == 0
                        ? "No enabled department matches the supplied reference in the current tenant."
                        : "Multiple enabled departments match the supplied reference.",
                    Candidates = matches.Take(10).Select(entity => new AiDraftAssociationCandidate
                    {
                        Id = entity.Id,
                        Code = entity.Code,
                        Name = entity.Name
                    }).ToList()
                });
            }
        }

        var payload = new DemoBusinessOrderDraftPayload
        {
            Title = title,
            CustomerName = customerName,
            Amount = request.Amount,
            DepartmentId = department?.Id,
            DepartmentCode = department?.Code,
            DepartmentName = department?.Name,
            DepartmentReference = departmentReference
        };
        var status = errors.Count == 0
            ? AiDocumentDraftStatus.ReadyForConfirmation
            : errors.Any(error => error.Code == "required")
                ? AiDocumentDraftStatus.Incomplete
                : AiDocumentDraftStatus.Invalid;
        return new ValidationResult(payload, errors, status);
    }

    private async Task AddValidationAsync(
        AiDocumentDraft draft,
        IReadOnlyCollection<AiDraftValidationError> errors,
        DateTimeOffset validatedAt,
        CancellationToken cancellationToken)
    {
        await _validationRepository.AddAsync(new AiDocumentDraftValidation
        {
            TenantId = draft.TenantId,
            CreatedBy = draft.ActorUserId,
            DraftId = draft.Id,
            DraftVersion = draft.DraftVersion,
            PayloadHash = draft.PayloadHash,
            IsValid = errors.Count == 0,
            ErrorsJson = JsonSerializer.Serialize(errors, JsonOptions),
            ValidatedAt = validatedAt
        }, cancellationToken);
    }

    private async Task<AiDocumentDraft> GetOwnedDraftAsync(
        Guid id,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _queryExecutor.FirstOrDefaultAsync(
            _draftRepository.Query().Where(entity =>
                entity.Id == id && entity.ActorUserId == userId && entity.TenantId == tenantId),
            cancellationToken) ?? throw new BusinessException(ErrorCode.NotFound, "AI document draft was not found.");
    }

    private async Task<IReadOnlyList<AiDocumentDraftResponse>> ToResponsesAsync(
        IReadOnlyList<AiDocumentDraft> drafts,
        CancellationToken cancellationToken)
    {
        var responses = new List<AiDocumentDraftResponse>(drafts.Count);
        foreach (var draft in drafts)
        {
            responses.Add(await ToResponseAsync(draft, cancellationToken));
        }

        return responses;
    }

    private async Task<AiDocumentDraftResponse> ToResponseAsync(
        AiDocumentDraft draft,
        CancellationToken cancellationToken)
    {
        var validation = await _queryExecutor.FirstOrDefaultAsync(
            _validationRepository.Query().Where(entity =>
                entity.DraftId == draft.Id && entity.DraftVersion == draft.DraftVersion),
            cancellationToken);
        var execution = await _queryExecutor.FirstOrDefaultAsync(
            _executionRepository.Query()
                .Where(entity =>
                    entity.DraftId == draft.Id &&
                    entity.Status == AiDocumentExecutionStatus.Succeeded)
                .OrderByDescending(entity => entity.CompletedAt),
            cancellationToken);
        return ToResponse(
            draft,
            DeserializePayload(draft.PayloadJson),
            DeserializeErrors(validation?.ErrorsJson),
            execution);
    }

    private static AiDocumentDraftResponse ToResponse(
        AiDocumentDraft draft,
        DemoBusinessOrderDraftPayload payload,
        IReadOnlyList<AiDraftValidationError> errors,
        AiDocumentExecution? execution = null)
    {
        var status = draft.Status is not (AiDocumentDraftStatus.Cancelled or AiDocumentDraftStatus.Executed) &&
            draft.ExpiresAt <= DateTimeOffset.UtcNow
            ? AiDocumentDraftStatus.Expired
            : draft.Status;
        return new AiDocumentDraftResponse
        {
            Id = draft.Id,
            ConversationId = draft.ConversationId,
            RunId = draft.RunId,
            BusinessType = draft.BusinessType,
            HandlerVersion = draft.HandlerVersion,
            Status = status,
            DraftVersion = draft.DraftVersion,
            Payload = payload,
            PayloadHash = draft.PayloadHash,
            ValidationErrors = errors,
            ExpiresAt = draft.ExpiresAt,
            LastValidatedAt = draft.LastValidatedAt,
            ConcurrencyToken = draft.RowVersion,
            Execution = execution is null || !execution.BusinessEntityId.HasValue
                ? null
                : new AiDocumentExecutionResponse
                {
                    ExecutionId = execution.Id,
                    DraftId = execution.DraftId,
                    RunId = execution.RunId,
                    BusinessEntityId = execution.BusinessEntityId.Value,
                    BusinessNo = execution.BusinessNo ?? string.Empty,
                    BusinessStatus = execution.BusinessStatus ?? string.Empty,
                    LinkUrl = $"/demo/business-order?keyword={Uri.EscapeDataString(execution.BusinessNo ?? string.Empty)}",
                    TraceId = execution.TraceId,
                    CompletedAt = execution.CompletedAt ?? execution.CreatedAt,
                    DraftStatus = AiDocumentDraftStatus.Executed,
                    DraftConcurrencyToken = draft.RowVersion
                }
        };
    }

    private static AiActionToolExecutionResult CreateToolResult(AiDocumentDraftResponse response)
    {
        return new AiActionToolExecutionResult
        {
            Draft = response,
            ContentJson = JsonSerializer.Serialize(new
            {
                type = "document_draft",
                draft = response,
                instruction = response.Status == AiDocumentDraftStatus.ReadyForConfirmation
                    ? "The draft is validated and ready for user preview. It has not created a formal business order."
                    : "Ask the user to correct the listed validation errors. Do not invent missing values. This has not created a formal business order."
            }, JsonOptions)
        };
    }

    private (Guid UserId, Guid TenantId) EnsureAccess()
    {
        if (!_aiConfiguration.Enabled || !_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue || !_currentUserService.TenantId.HasValue)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "A valid user and tenant context is required.");
        }

        var tenantId = _currentUserService.TenantId.Value;
        if (!_aiConfiguration.AllowedTenantIds.Contains(tenantId) ||
            !_currentUserService.HasPermission(AiCenterConstants.DocumentDraftPermission) ||
            !_currentUserService.HasPermission("demo-business-order:create"))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to manage AI document drafts.");
        }

        return (_currentUserService.UserId.Value, tenantId);
    }

    private static void EnsureEditable(AiDocumentDraft draft)
    {
        EnsureNotExpired(draft);
        if (draft.Status is AiDocumentDraftStatus.Cancelled or AiDocumentDraftStatus.Executed)
        {
            throw new BusinessException(ErrorCode.Conflict, "Cancelled or executed AI document drafts cannot be edited.");
        }
    }

    private static void EnsureNotExpired(AiDocumentDraft draft)
    {
        if (draft.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new BusinessException(ErrorCode.Conflict, "The AI document draft has expired.");
        }
    }

    private static void EnsureConcurrencyToken(byte[]? concurrencyToken)
    {
        if (concurrencyToken is null || concurrencyToken.Length == 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Concurrency token is required.");
        }
    }

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateRequiredText(
        string? value,
        string field,
        int maxLength,
        string displayName,
        ICollection<AiDraftValidationError> errors)
    {
        if (value is null)
        {
            AddError(errors, field, "required", $"{displayName} is required.");
        }
        else if (value.Length > maxLength)
        {
            AddError(errors, field, "max_length", $"{displayName} cannot exceed {maxLength} characters.");
        }
    }

    private static void AddError(
        ICollection<AiDraftValidationError> errors,
        string field,
        string code,
        string message)
    {
        errors.Add(new AiDraftValidationError { Field = field, Code = code, Message = message });
    }

    private static string SerializePayload(DemoBusinessOrderDraftPayload payload) =>
        JsonSerializer.Serialize(payload, CanonicalJsonOptions);

    private static DemoBusinessOrderDraftPayload DeserializePayload(string json) =>
        JsonSerializer.Deserialize<DemoBusinessOrderDraftPayload>(json, JsonOptions) ?? new DemoBusinessOrderDraftPayload();

    private static IReadOnlyList<AiDraftValidationError> DeserializeErrors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<AiDraftValidationError>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ComputePayloadHash(DemoBusinessOrderDraftPayload payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SerializePayload(payload))));

    private sealed record ValidationResult(
        DemoBusinessOrderDraftPayload Payload,
        IReadOnlyList<AiDraftValidationError> Errors,
        AiDocumentDraftStatus Status);
}
