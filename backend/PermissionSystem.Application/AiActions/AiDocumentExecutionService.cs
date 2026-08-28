using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Security;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiActions;

public sealed class AiDocumentExecutionService : IAiDocumentExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<AiDocumentDraft> _draftRepository;
    private readonly IRepository<AiDocumentDraftValidation> _validationRepository;
    private readonly IRepository<AiDocumentConfirmation> _confirmationRepository;
    private readonly IRepository<AiDocumentExecution> _executionRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiCenterConfiguration _aiConfiguration;
    private readonly IAiDraftConfiguration _draftConfiguration;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly IDemoBusinessOrderService _businessOrderService;
    private readonly IOutboxService _outboxService;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiDocumentExecutionRecoveryStore _recoveryStore;
    private readonly ILogger<AiDocumentExecutionService> _logger;

    public AiDocumentExecutionService(
        IRepository<AiDocumentDraft> draftRepository,
        IRepository<AiDocumentDraftValidation> validationRepository,
        IRepository<AiDocumentConfirmation> confirmationRepository,
        IRepository<AiDocumentExecution> executionRepository,
        IAsyncQueryExecutor queryExecutor,
        ICurrentUserService currentUserService,
        IAiCenterConfiguration aiConfiguration,
        IAiDraftConfiguration draftConfiguration,
        ISecurityPolicyService securityPolicyService,
        IDemoBusinessOrderService businessOrderService,
        IOutboxService outboxService,
        ITraceContextAccessor traceContextAccessor,
        IUnitOfWork unitOfWork,
        IAiDocumentExecutionRecoveryStore recoveryStore,
        ILogger<AiDocumentExecutionService> logger)
    {
        _draftRepository = draftRepository;
        _validationRepository = validationRepository;
        _confirmationRepository = confirmationRepository;
        _executionRepository = executionRepository;
        _queryExecutor = queryExecutor;
        _currentUserService = currentUserService;
        _aiConfiguration = aiConfiguration;
        _draftConfiguration = draftConfiguration;
        _securityPolicyService = securityPolicyService;
        _businessOrderService = businessOrderService;
        _outboxService = outboxService;
        _traceContextAccessor = traceContextAccessor;
        _unitOfWork = unitOfWork;
        _recoveryStore = recoveryStore;
        _logger = logger;
    }

    public async Task<AiDocumentConfirmationResponse> ConfirmAsync(
        Guid draftId,
        CreateAiDocumentConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        EnsureConcurrencyToken(request.DraftConcurrencyToken, "Draft concurrency token is required.");

        AiDocumentConfirmation? result = null;
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var draft = await GetOwnedDraftAsync(draftId, identity, token);
            ConcurrencyTokenGuard.EnsureMatches(draft, request.DraftConcurrencyToken);
            await EnsureReadyAndValidatedAsync(draft, token);

            await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync(
                AiCenterConstants.DocumentExecuteOperationCode,
                force: true,
                token);

            var now = DateTimeOffset.UtcNow;
            var confirmation = await _queryExecutor.FirstOrDefaultAsync(
                _confirmationRepository.Query().Where(entity =>
                    entity.TenantId == identity.TenantId &&
                    entity.DraftId == draft.Id &&
                    entity.DraftVersion == draft.DraftVersion),
                token);
            if (confirmation is null)
            {
                confirmation = new AiDocumentConfirmation
                {
                    TenantId = identity.TenantId,
                    CreatedBy = identity.UserId,
                    DraftId = draft.Id,
                    RunId = draft.RunId,
                    ActorUserId = identity.UserId,
                    DraftVersion = draft.DraftVersion,
                    ConfirmationVersion = 1
                };
                await _confirmationRepository.AddAsync(confirmation, token);
            }
            else
            {
                if (await HasSucceededAsync(confirmation.Id, confirmation.ConfirmationVersion, identity, token))
                {
                    throw new BusinessException(ErrorCode.Conflict, "This AI document draft has already been executed.");
                }

                confirmation.ConfirmationVersion++;
                confirmation.UpdatedBy = identity.UserId;
                _confirmationRepository.Update(confirmation);
            }

            confirmation.RunId = draft.RunId;
            confirmation.ActorUserId = identity.UserId;
            confirmation.PayloadHash = draft.PayloadHash;
            confirmation.HandlerVersion = draft.HandlerVersion;
            confirmation.Status = AiDocumentConfirmationStatus.Confirmed;
            confirmation.ConfirmedAt = now;
            confirmation.ExpiresAt = now.AddMinutes(_draftConfiguration.ConfirmationExpirationMinutes);
            confirmation.ConsumedAt = null;
            await _unitOfWork.SaveChangesAsync(token);
            result = confirmation;
        }, cancellationToken);

        return ToConfirmationResponse(result!);
    }

    public async Task<AiDocumentExecutionResponse> ExecuteAsync(
        Guid draftId,
        ExecuteAiDocumentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess();
        EnsureConcurrencyToken(request.ConfirmationConcurrencyToken, "Confirmation concurrency token is required.");
        EnsureConcurrencyToken(request.DraftConcurrencyToken, "Draft concurrency token is required.");
        if (request.ConfirmationId == Guid.Empty || request.ConfirmationVersion <= 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "A valid confirmation is required.");
        }

        var businessKey = BuildBusinessIdempotencyKey(request.ConfirmationId, request.ConfirmationVersion);
        var previous = await FindExecutionAsync(identity, draftId, businessKey, cancellationToken);
        if (previous is not null)
        {
            return EnsureReplayable(previous);
        }

        AiDocumentExecution? executionResult = null;
        AiDocumentDraft? draftResult = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var draft = await GetOwnedDraftAsync(draftId, identity, token);
                ConcurrencyTokenGuard.EnsureMatches(draft, request.DraftConcurrencyToken);
                await EnsureReadyAndValidatedAsync(draft, token);

                var confirmation = await _queryExecutor.FirstOrDefaultAsync(
                    _confirmationRepository.Query().Where(entity =>
                        entity.Id == request.ConfirmationId &&
                        entity.TenantId == identity.TenantId &&
                        entity.ActorUserId == identity.UserId &&
                        entity.DraftId == draft.Id),
                    token) ?? throw new BusinessException(ErrorCode.Forbidden, "AI document confirmation is invalid.");
                ConcurrencyTokenGuard.EnsureMatches(confirmation, request.ConfirmationConcurrencyToken);
                EnsureConfirmationMatches(confirmation, draft, request.ConfirmationVersion);

                var payload = DeserializePayload(draft.PayloadJson);
                if (payload.DepartmentId.HasValue && !_currentUserService.HasPermission("system:department:view"))
                {
                    throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to use the selected department.");
                }

                var now = DateTimeOffset.UtcNow;
                confirmation.Status = AiDocumentConfirmationStatus.Consumed;
                confirmation.ConsumedAt = now;
                confirmation.UpdatedBy = identity.UserId;
                _confirmationRepository.Update(confirmation);

                var traceId = string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId)
                    ? Guid.NewGuid().ToString("N")
                    : _traceContextAccessor.TraceId;
                var execution = new AiDocumentExecution
                {
                    TenantId = identity.TenantId,
                    CreatedBy = identity.UserId,
                    ConfirmationId = confirmation.Id,
                    ConfirmationVersion = confirmation.ConfirmationVersion,
                    DraftId = draft.Id,
                    RunId = draft.RunId,
                    ActorUserId = identity.UserId,
                    BusinessType = DemoBusinessOrderConstants.BusinessType,
                    BusinessIdempotencyKey = businessKey,
                    Status = AiDocumentExecutionStatus.Executing,
                    TraceId = traceId,
                    StartedAt = now
                };
                await _executionRepository.AddAsync(execution, token);

                var order = await _businessOrderService.CreateAsync(new CreateDemoBusinessOrderRequest
                {
                    TenantId = identity.TenantId,
                    Title = payload.Title ?? string.Empty,
                    CustomerName = payload.CustomerName ?? string.Empty,
                    Amount = payload.Amount ?? -1,
                    DepartmentId = payload.DepartmentId
                }, token);

                var completedAt = DateTimeOffset.UtcNow;
                execution.Status = AiDocumentExecutionStatus.Succeeded;
                execution.BusinessEntityId = order.Id;
                execution.BusinessNo = order.OrderNo;
                execution.BusinessStatus = order.ApprovalStatus.ToString();
                execution.CompletedAt = completedAt;
                execution.UpdatedBy = identity.UserId;
                _executionRepository.Update(execution);

                draft.Status = AiDocumentDraftStatus.Executed;
                draft.UpdatedBy = identity.UserId;
                _draftRepository.Update(draft);

                execution.OutboxMessageId = await _outboxService.EnqueueAsync(
                    AiDocumentExecutionMessageNames.Exchange,
                    AiDocumentExecutionMessageNames.RoutingKey,
                    new AiDocumentExecutedEvent
                    {
                        ExecutionId = execution.Id,
                        DraftId = draft.Id,
                        RunId = draft.RunId,
                        ActorUserId = identity.UserId,
                        BusinessType = execution.BusinessType,
                        BusinessEntityId = order.Id,
                        BusinessNo = order.OrderNo,
                        BusinessStatus = execution.BusinessStatus,
                        TraceId = traceId,
                        OccurredAt = completedAt
                    },
                    tenantId: identity.TenantId,
                    messageId: execution.Id.ToString("N"),
                    cancellationToken: token);
                await _unitOfWork.SaveChangesAsync(token);
                executionResult = execution;
                draftResult = draft;
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            var recovered = await TryGetSucceededExecutionAsync(identity, draftId, businessKey, cancellationToken);
            if (recovered is not null)
            {
                return EnsureReplayable(recovered);
            }

            await TryRecordFailureAsync(identity, draftId, request, businessKey, exception, cancellationToken);
            throw;
        }

        return ToExecutionResponse(executionResult!, draftResult!.Status, draftResult.RowVersion);
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
            !_currentUserService.HasPermission(AiCenterConstants.DocumentExecutePermission) ||
            !_currentUserService.HasPermission("demo-business-order:create"))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to execute AI document drafts.");
        }

        return (_currentUserService.UserId.Value, tenantId);
    }

    private async Task<AiDocumentDraft> GetOwnedDraftAsync(
        Guid draftId,
        (Guid UserId, Guid TenantId) identity,
        CancellationToken cancellationToken)
    {
        return await _queryExecutor.FirstOrDefaultAsync(
            _draftRepository.Query().Where(entity =>
                entity.Id == draftId &&
                entity.TenantId == identity.TenantId &&
                entity.ActorUserId == identity.UserId),
            cancellationToken) ?? throw new BusinessException(ErrorCode.NotFound, "AI document draft was not found.");
    }

    private async Task EnsureReadyAndValidatedAsync(AiDocumentDraft draft, CancellationToken cancellationToken)
    {
        if (draft.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new BusinessException(ErrorCode.Conflict, "The AI document draft has expired.");
        }

        if (draft.Status != AiDocumentDraftStatus.ReadyForConfirmation ||
            draft.BusinessType != DemoBusinessOrderConstants.BusinessType ||
            draft.HandlerVersion != AiBusinessActionConstants.DemoBusinessOrderHandlerVersion)
        {
            throw new BusinessException(ErrorCode.Conflict, "The AI document draft is not ready for confirmation.");
        }

        var validation = await _queryExecutor.FirstOrDefaultAsync(
            _validationRepository.Query().Where(entity =>
                entity.DraftId == draft.Id &&
                entity.DraftVersion == draft.DraftVersion &&
                entity.PayloadHash == draft.PayloadHash),
            cancellationToken);
        if (validation is null || !validation.IsValid)
        {
            throw new BusinessException(ErrorCode.Conflict, "The AI document draft validation is missing or invalid.");
        }
    }

    private static void EnsureConfirmationMatches(
        AiDocumentConfirmation confirmation,
        AiDocumentDraft draft,
        int expectedConfirmationVersion)
    {
        if (confirmation.ConfirmationVersion != expectedConfirmationVersion ||
            confirmation.Status != AiDocumentConfirmationStatus.Confirmed ||
            confirmation.ExpiresAt <= DateTimeOffset.UtcNow ||
            confirmation.DraftVersion != draft.DraftVersion ||
            confirmation.RunId != draft.RunId ||
            !string.Equals(confirmation.PayloadHash, draft.PayloadHash, StringComparison.Ordinal) ||
            !string.Equals(confirmation.HandlerVersion, draft.HandlerVersion, StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCode.Conflict, "AI document confirmation is expired, consumed, or no longer matches the draft.");
        }
    }

    private async Task<bool> HasSucceededAsync(
        Guid confirmationId,
        int confirmationVersion,
        (Guid UserId, Guid TenantId) identity,
        CancellationToken cancellationToken)
    {
        return await _queryExecutor.AnyAsync(
            _executionRepository.Query().Where(entity =>
                entity.TenantId == identity.TenantId &&
                entity.ActorUserId == identity.UserId &&
                entity.ConfirmationId == confirmationId &&
                entity.ConfirmationVersion == confirmationVersion &&
                entity.Status == AiDocumentExecutionStatus.Succeeded),
            cancellationToken);
    }

    private Task<AiDocumentExecution?> FindExecutionAsync(
        (Guid UserId, Guid TenantId) identity,
        Guid draftId,
        string businessKey,
        CancellationToken cancellationToken)
    {
        return _queryExecutor.FirstOrDefaultAsync(
            _executionRepository.Query().Where(entity =>
                entity.TenantId == identity.TenantId &&
                entity.ActorUserId == identity.UserId &&
                entity.DraftId == draftId &&
                entity.BusinessIdempotencyKey == businessKey),
            cancellationToken);
    }

    private static AiDocumentExecutionResponse EnsureReplayable(AiDocumentExecution execution)
    {
        if (execution.Status != AiDocumentExecutionStatus.Succeeded || !execution.BusinessEntityId.HasValue)
        {
            throw new BusinessException(
                ErrorCode.Conflict,
                execution.Status == AiDocumentExecutionStatus.Executing
                    ? "AI document execution is already processing."
                    : "The previous AI document execution failed. Confirm the draft again before retrying.");
        }

        return ToExecutionResponse(execution, AiDocumentDraftStatus.Executed, []);
    }

    private async Task<AiDocumentExecution?> TryGetSucceededExecutionAsync(
        (Guid UserId, Guid TenantId) identity,
        Guid draftId,
        string businessKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = await _recoveryStore.GetByBusinessIdempotencyKeyAsync(
                identity.TenantId,
                businessKey,
                cancellationToken);
            return execution?.ActorUserId == identity.UserId &&
                execution.DraftId == draftId &&
                execution.Status == AiDocumentExecutionStatus.Succeeded
                ? execution
                : null;
        }
        catch (Exception recoveryException)
        {
            _logger.LogWarning(recoveryException, "Failed to inspect AI document execution recovery state.");
            return null;
        }
    }

    private async Task TryRecordFailureAsync(
        (Guid UserId, Guid TenantId) identity,
        Guid draftId,
        ExecuteAiDocumentDraftRequest request,
        string businessKey,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var errorCode = exception is BusinessException businessException
                ? businessException.ErrorCode.ToString()
                : ErrorCode.InternalServerError.ToString();
            await _recoveryStore.RecordFailureAsync(new AiDocumentExecutionFailureRecord
            {
                TenantId = identity.TenantId,
                ConfirmationId = request.ConfirmationId,
                ConfirmationVersion = request.ConfirmationVersion,
                DraftId = draftId,
                ActorUserId = identity.UserId,
                BusinessType = DemoBusinessOrderConstants.BusinessType,
                BusinessIdempotencyKey = businessKey,
                Status = exception is BusinessException { ErrorCode: ErrorCode.Conflict }
                    ? AiDocumentExecutionStatus.Conflict
                    : AiDocumentExecutionStatus.Failed,
                TraceId = _traceContextAccessor.TraceId,
                ErrorCode = errorCode,
                ErrorSummary = Truncate(exception.Message, 1000),
                OccurredAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch (Exception recoveryException)
        {
            _logger.LogWarning(recoveryException, "Failed to persist AI document execution failure state.");
        }
    }

    private static DemoBusinessOrderDraftPayload DeserializePayload(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<DemoBusinessOrderDraftPayload>(value, JsonOptions)
                ?? throw new BusinessException(ErrorCode.Conflict, "AI document draft payload is invalid.");
        }
        catch (JsonException exception)
        {
            throw new BusinessException(ErrorCode.Conflict, "AI document draft payload is invalid.", exception);
        }
    }

    private static string BuildBusinessIdempotencyKey(Guid confirmationId, int confirmationVersion) =>
        $"ai-document:{confirmationId:N}:{confirmationVersion}";

    private static void EnsureConcurrencyToken(byte[]? token, string message)
    {
        if (token is null || token.Length == 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private static AiDocumentConfirmationResponse ToConfirmationResponse(AiDocumentConfirmation entity) => new()
    {
        Id = entity.Id,
        DraftId = entity.DraftId,
        DraftVersion = entity.DraftVersion,
        ConfirmationVersion = entity.ConfirmationVersion,
        PayloadHash = entity.PayloadHash,
        HandlerVersion = entity.HandlerVersion,
        ConfirmedAt = entity.ConfirmedAt,
        ExpiresAt = entity.ExpiresAt,
        ConcurrencyToken = entity.RowVersion
    };

    private static AiDocumentExecutionResponse ToExecutionResponse(
        AiDocumentExecution execution,
        AiDocumentDraftStatus draftStatus,
        byte[] draftConcurrencyToken) => new()
        {
            ExecutionId = execution.Id,
            DraftId = execution.DraftId,
            RunId = execution.RunId,
            BusinessEntityId = execution.BusinessEntityId!.Value,
            BusinessNo = execution.BusinessNo ?? string.Empty,
            BusinessStatus = execution.BusinessStatus ?? string.Empty,
            LinkUrl = $"/demo/business-order?keyword={Uri.EscapeDataString(execution.BusinessNo ?? string.Empty)}",
            TraceId = execution.TraceId,
            CompletedAt = execution.CompletedAt ?? execution.CreatedAt,
            DraftStatus = draftStatus,
            DraftConcurrencyToken = draftConcurrencyToken
        };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
