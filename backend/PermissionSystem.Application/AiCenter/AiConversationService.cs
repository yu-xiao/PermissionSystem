using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public sealed partial class AiConversationService : IAiConversationService, IAiRunSubmissionService, IAiRunExecutionService
{
    private const string AgentCode = "permission-platform-agent";
    private const string AgentVersion = "2.2";
    private const string PromptVersion = "2.2";
    private const int MaxQuestionLength = 4000;
    private const int MaxModelRounds = 6;
    private const int MaxToolCalls = 10;
    private const int MaxHistoryMessages = 20;
    private static readonly TimeSpan MaxRunDuration = TimeSpan.FromSeconds(90);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRepository<AiConversation> _conversationRepository;
    private readonly IRepository<AiMessage> _messageRepository;
    private readonly IRepository<AiRun> _runRepository;
    private readonly IRepository<AiProviderConfig> _providerRepository;
    private readonly IRepository<AiToolInvocation> _toolInvocationRepository;
    private readonly IRepository<AiUsageLog> _usageLogRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiCenterConfiguration _configuration;
    private readonly IAiReadOnlyToolRegistry _toolRegistry;
    private readonly IAiActionToolRegistry _actionToolRegistry;
    private readonly IAiDocumentDraftReader _draftReader;
    private readonly IAiModelGateway _modelGateway;
    private readonly IConfigValueProtector _valueProtector;
    private readonly IAiRunCancellationProbe _cancellationProbe;
    private readonly AiRunCancellationCoordinator _cancellationCoordinator;
    private readonly IAiRunRealtimeSender _realtimeSender;
    private readonly IAiModelRouteService? _modelRouteService;
    private readonly IAiBudgetService? _budgetService;
    private readonly IRepository<AiUserFeedback>? _feedbackRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiRunAdmissionService _admissionService;
    private readonly IAiCircuitBreaker _circuitBreaker;
    private readonly IAiPermissionDiagnosticReader? _diagnosticReader;
    private readonly IAiStructuredResultReader? _structuredReader;
    private readonly IAiFollowUpContextService? _followUp;
    private readonly IAiScenarioRuntime? _scenarioRuntime;
    private readonly IAiBuildIdentity? _buildIdentity;

    public AiConversationService(
        IRepository<AiConversation> conversationRepository,
        IRepository<AiMessage> messageRepository,
        IRepository<AiRun> runRepository,
        IRepository<AiProviderConfig> providerRepository,
        IRepository<AiToolInvocation> toolInvocationRepository,
        IRepository<AiUsageLog> usageLogRepository,
        IAsyncQueryExecutor queryExecutor,
        ICurrentUserService currentUserService,
        IAiReadOnlyToolRegistry toolRegistry,
        IAiModelGateway modelGateway,
        IConfigValueProtector valueProtector,
        IAiRunCancellationProbe cancellationProbe,
        AiRunCancellationCoordinator cancellationCoordinator,
        IAiRunRealtimeSender realtimeSender,
        IUnitOfWork unitOfWork,
        IAiCenterConfiguration? configuration = null,
        IAiActionToolRegistry? actionToolRegistry = null,
        IAiDocumentDraftReader? draftReader = null,
        IAiModelRouteService? modelRouteService = null,
        IAiBudgetService? budgetService = null,
        IRepository<AiUserFeedback>? feedbackRepository = null,
        IAiRunAdmissionService? admissionService = null,
        IAiCircuitBreaker? circuitBreaker = null,
        IAiPermissionDiagnosticReader? diagnosticReader = null,
        IAiStructuredResultReader? structuredReader = null,
        IAiFollowUpContextService? followUp = null,
        IAiScenarioRuntime? scenarioRuntime = null,
        IAiBuildIdentity? buildIdentity = null,
        IAiRunExecutionStore? executionStore = null,
        AiRunIdentityValidator? identityValidator = null,
        AiRunExecutionFence? executionFence = null,
        PermissionSystem.Application.Authentication.IUserCredentialValidator? identities = null)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _runRepository = runRepository;
        _providerRepository = providerRepository;
        _toolInvocationRepository = toolInvocationRepository;
        _usageLogRepository = usageLogRepository;
        _queryExecutor = queryExecutor;
        _currentUserService = currentUserService;
        _toolRegistry = toolRegistry;
        _actionToolRegistry = actionToolRegistry ?? new NullAiActionToolRegistry();
        _draftReader = draftReader ?? new NullAiDocumentDraftReader();
        _modelGateway = modelGateway;
        _valueProtector = valueProtector;
        _cancellationProbe = cancellationProbe;
        _cancellationCoordinator = cancellationCoordinator;
        _realtimeSender = realtimeSender;
        _modelRouteService = modelRouteService;
        _budgetService = budgetService;
        _feedbackRepository = feedbackRepository;
        _unitOfWork = unitOfWork;
        _admissionService = admissionService ?? new AiRunAdmissionServicePlaceholder();
        _circuitBreaker = circuitBreaker ?? new AllowAllAiCircuitBreaker();
        _diagnosticReader = diagnosticReader;
        _structuredReader = structuredReader;
        _followUp = followUp;
        _scenarioRuntime = scenarioRuntime;
        _buildIdentity = buildIdentity;
        _configuration = configuration ?? new DefaultAiCenterConfiguration();
        _executionStore = executionStore;
        _identityValidator = identityValidator;
        _executionFence = executionFence;
        _identities = identities;
    }

    public async Task<PagedResult<AiConversationListResponse>> GetPagedAsync(
        AiConversationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ConversationViewPermission);
        var query = _conversationRepository.Query()
            .Where(entity => entity.UserId == identity.UserId && entity.Status != AiConversationStatus.Deleted);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity => entity.Title.Contains(keyword));
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var entities = await _queryExecutor.ToListAsync(
            query.OrderByDescending(entity => entity.LastMessageAt)
                .Skip(request.Skip)
                .Take(request.PageSize),
            cancellationToken);
        return PagedResult<AiConversationListResponse>.Create(
            entities.Select(ToListResponse).ToList(),
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    public async Task<AiConversationDetailResponse> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ConversationViewPermission);
        var conversation = await GetOwnedConversationAsync(id, identity.UserId, cancellationToken);
        var messages = await _queryExecutor.ToListAsync(
            _messageRepository.Query()
                .Where(entity => entity.ConversationId == id && entity.Role != AiMessageRole.Tool)
                .OrderBy(entity => entity.Sequence),
            cancellationToken);
        var messageIds = messages.Select(entity => entity.Id).ToList();
        var responseRuns = await _queryExecutor.ToListAsync(
            _runRepository.Query().Where(entity =>
                entity.ConversationId == id &&
                entity.ResponseMessageId.HasValue &&
                messageIds.Contains(entity.ResponseMessageId.Value)),
            cancellationToken);
        var responseRunIds = responseRuns.Select(entity => entity.Id).ToList();
        var feedback = _feedbackRepository is null
            ? []
            : await _queryExecutor.ToListAsync(
                _feedbackRepository.Query().Where(entity => responseRunIds.Contains(entity.RunId)),
                cancellationToken);
        var drafts = CanReadDocumentDrafts()
            ? await _draftReader.GetByConversationAsync(id, cancellationToken)
            : [];
        var structured = _structuredReader is null ? new AiStructuredResultPage([]) :
            await _structuredReader.ReadAsync(id, cancellationToken: cancellationToken);
        var latest = await _queryExecutor.FirstOrDefaultAsync(_runRepository.Query().Where(r => r.ConversationId == id)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id), cancellationToken);
        return ToDetailResponse(
            conversation,
            messages,
            responseRuns.ToDictionary(entity => entity.ResponseMessageId!.Value, entity => entity.Id),
            feedback.ToDictionary(entity => entity.RunId, AiOperationsService.ToFeedbackResponse),
            drafts,
            _structuredReader is not null ? DiagnosticProjection(structured.Results) :
                _diagnosticReader is null ? [] : await _diagnosticReader.ReadAsync(id, cancellationToken: cancellationToken), structured,
                latest is null ? null : await ToRunResponseAsync(latest, cancellationToken));
    }

    public async Task<AiConversationDetailResponse> CreateAsync(
        CreateAiConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        AiScenarioVersion? version = null;
        if (request.ScenarioId.HasValue)
        {
            if (_scenarioRuntime is null) throw new BusinessException(ErrorCode.Conflict, "AI scenarios are unavailable.");
            version = await _scenarioRuntime.ResolveCurrentAsync(request.ScenarioId.Value, cancellationToken);
            if (version.ScenarioId != request.ScenarioId || version.TenantId != identity.TenantId)
                throw new BusinessException(ErrorCode.Forbidden, "Invalid AI scenario ownership.");
        }
        var now = DateTimeOffset.UtcNow;
        var conversation = new AiConversation
        {
            TenantId = identity.TenantId,
            UserId = identity.UserId,
            ScenarioId = version?.ScenarioId,
            ScenarioVersionId = version?.Id,
            AgentCode = version is null ? AgentCode : AiScenarioCatalog.PermissionAssistant,
            AgentVersion = version?.VersionNumber.ToString() ?? AgentVersion,
            Title = NormalizeTitle(request.Title),
            Status = AiConversationStatus.Active,
            LastMessageAt = now,
            RetentionUntil = now.AddDays(_configuration.ConversationRetentionDays)
        };
        await _conversationRepository.AddAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDetailResponse(
            conversation,
            [],
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, AiFeedbackResponse>(),
            []);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var conversation = await GetOwnedConversationAsync(id, identity.UserId, cancellationToken);
        if (await _queryExecutor.AnyAsync(
                _runRepository.Query().Where(entity =>
                    entity.ConversationId == id &&
                    (entity.Status == AiRunStatus.Pending || entity.Status == AiRunStatus.Running)),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "An active AI run must be cancelled before deleting the conversation.");
        }

        conversation.Status = AiConversationStatus.Deleted;
        _conversationRepository.Remove(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiRunResponse> SendMessageAsync(
        Guid conversationId,
        SendAiMessageRequest request,
        CancellationToken cancellationToken = default)
        => await SendMessageCoreAsync(conversationId, request, null, cancellationToken);

    private async Task<AiRunResponse> SendMessageCoreAsync(
        Guid conversationId,
        SendAiMessageRequest request,
        Guid? retryOfRunId,
        CancellationToken cancellationToken,
        string? submissionKey = null,
        bool enqueueOnly = false)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var content = NormalizeQuestion(request.Content);
        var submissionHash = _executionStore is null ? null : ComputeDigest(submissionKey?.Trim() ?? Guid.NewGuid().ToString("N"));
        var requestHash = ComputeDigest(JsonSerializer.Serialize(new { Content = content, request.ContextRef, request.UtcOffsetMinutes, retryOfRunId }, JsonOptions));
        if (submissionHash is not null)
        {
            var duplicate = await _queryExecutor.FirstOrDefaultAsync(_runRepository.Query().Where(r =>
                r.ConversationId == conversationId && r.ActorUserId == identity.UserId && r.SubmissionHash == submissionHash), cancellationToken);
            if (duplicate is not null)
            {
                if (duplicate.RequestHash != requestHash) throw new BusinessException(ErrorCode.Conflict, "The submission key was reused for a different AI request.");
                return enqueueOnly ? await ToRunResponseAsync(duplicate, cancellationToken) : await WaitAsync(duplicate.Id, cancellationToken);
            }
        }
        var actor = _executionStore is null ? null : await _identities!.GetAuthenticationStateAsync(identity.TenantId, identity.UserId, cancellationToken);
        if (_executionStore is not null && (actor is null || actor.SecurityStamp != _currentUserService.SecurityStamp ||
            string.IsNullOrWhiteSpace(_currentUserService.SessionId)))
            throw new BusinessException(ErrorCode.Unauthorized, "A valid session is required for background AI execution.");
        if (request.UtcOffsetMinutes is < -840 or > 840)
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid UTC offset.");
        if (request.ContextRef is not null && (request.ContextRef.RunId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ContextRef.InvocationId) || request.ContextRef.InvocationId.Length > 100))
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid AI query context reference.");
        var conversation = await GetOwnedConversationAsync(conversationId, identity.UserId, cancellationToken);
        if (conversation.Status != AiConversationStatus.Active)
        {
            throw new BusinessException(ErrorCode.Conflict, "The AI conversation is not active.");
        }

        if (await _queryExecutor.AnyAsync(
                _runRepository.Query().Where(entity =>
                    entity.ConversationId == conversationId &&
                    (entity.Status == AiRunStatus.Pending || entity.Status == AiRunStatus.Running)),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "The conversation already has an active AI run.");
        }

        if (conversation.ScenarioId.HasValue != conversation.ScenarioVersionId.HasValue)
            throw new BusinessException(ErrorCode.Conflict, "AI conversation version is invalid.");
        var scenarioSnapshot = conversation.ScenarioVersionId.HasValue
            ? await (_scenarioRuntime ?? throw new BusinessException(ErrorCode.Conflict, "AI scenarios are unavailable."))
                .ValidateAsync(conversation.ScenarioVersionId.Value, cancellationToken) : null;
        if (retryOfRunId.HasValue)
        {
            var original = await GetOwnedRunAsync(retryOfRunId.Value, identity.UserId, cancellationToken);
            if (original.ScenarioVersionId != conversation.ScenarioVersionId)
                throw new BusinessException(ErrorCode.Conflict, "Retry must use the original AI version.");
        }
        var executionAgentCode = scenarioSnapshot is null ? AgentCode : conversation.AgentCode;
        var routeCandidates = await ResolveRouteCandidatesAsync(executionAgentCode, conversationId, cancellationToken);
        var provider = routeCandidates[0].Provider;
        var agentCircuitTarget = new AiCircuitTarget("agent", $"{identity.TenantId:N}:{executionAgentCode}");
        if (!await _circuitBreaker.AllowAsync(agentCircuitTarget, cancellationToken))
        {
            throw new BusinessException(ErrorCode.TooManyRequests, "The AI agent circuit is temporarily open.");
        }
        var lastMessage = await _queryExecutor.FirstOrDefaultAsync(
            _messageRepository.Query()
                .Where(entity => entity.ConversationId == conversationId)
                .OrderByDescending(entity => entity.Sequence),
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var requestMessage = new AiMessage
        {
            TenantId = identity.TenantId,
            ConversationId = conversationId,
            Role = AiMessageRole.User,
            Content = content,
            Sequence = (lastMessage?.Sequence ?? 0) + 1
        };
        requestMessage.ContentDigest = ComputeDigest(requestMessage.Content);
        if (lastMessage is null && IsDefaultTitle(conversation.Title))
        {
            conversation.Title = NormalizeTitle(content);
        }

        conversation.LastMessageAt = now;
        conversation.LastRunAt = now;
        conversation.RetentionUntil = now.AddDays(_configuration.ConversationRetentionDays);

        var run = await _admissionService.ExecuteAsync(
            new AiRunAdmissionRequest(identity.TenantId, identity.UserId, executionAgentCode, provider.Id, EstimateInputTokens([new AiModelGatewayMessage { Role = "user", Content = content }]) + (scenarioSnapshot?.Configuration.MaxTokens ?? provider.MaxTokens ?? AiCenterConstants.DefaultMaxOutputTokens)),
            async () =>
            {
                AiRun? admitted = null;
                await _unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
                {
                if (await _queryExecutor.AnyAsync(_runRepository.Query().Where(r => r.ConversationId == conversationId &&
                    (r.Status == AiRunStatus.Pending || r.Status == AiRunStatus.Running)), transactionToken))
                    throw new BusinessException(ErrorCode.Conflict, "The conversation already has an active AI run.");
                _conversationRepository.Update(conversation);
                await _messageRepository.AddAsync(requestMessage, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var newRun = new AiRun
                {
                    ExecutionMode = _executionStore is null ? null : "Background",
                    ActorSessionId = _executionStore is null ? null : _currentUserService.SessionId,
                    ActorSecurityStamp = actor?.SecurityStamp,
                    QueueDeadlineAt = _executionStore is null ? null : now.AddSeconds(_configuration.RunQueueTimeoutSeconds),
                    SubmissionHash = submissionHash, RequestHash = requestHash,
                    ProgressVersion = _executionStore is null ? null : 1,
                    TenantId = identity.TenantId,
                    ConversationId = conversationId,
                    RequestMessageId = requestMessage.Id,
                    ProviderConfigId = provider.Id,
                    ActorUserId = identity.UserId,
                    ScenarioId = conversation.ScenarioId,
                    ScenarioVersionId = conversation.ScenarioVersionId,
                    ScenarioContentHash = scenarioSnapshot is null ? null : AiScenarioSnapshots.Digest(AiScenarioSnapshots.Json(scenarioSnapshot)),
                    BuildIdentity = _buildIdentity?.Identity ?? AiScenarioSnapshots.Digest(typeof(AiConversationService).Assembly.ManifestModule.ModuleVersionId.ToString()),
                    AgentCode = executionAgentCode,
                    AgentVersion = scenarioSnapshot is null ? AgentVersion : conversation.AgentVersion,
                    PromptVersion = scenarioSnapshot is null ? PromptVersion : "scenario-1",

                    ModelName = provider.ModelName,
                    RetryOfRunId = retryOfRunId,
                    Status = AiRunStatus.Pending,
                    ExecutionLeaseId = Guid.NewGuid(),
                    LastHeartbeatAt = now,
                    DeadlineAt = now.AddSeconds(scenarioSnapshot?.Configuration.MaxRunSeconds ?? 90),
                    TraceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")
                };
                StoreExecutionConfiguration(newRun, BuildExecutionConfiguration(newRun, scenarioSnapshot, [], "Admitted"));
                if (_executionStore is not null) await _identityValidator!.ValidateAsync(newRun, transactionToken);
                await _runRepository.AddAsync(newRun, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await StoreRequestContextAsync(newRun, request.ContextRef, request.UtcOffsetMinutes, transactionToken);
                admitted = newRun;
                }, cancellationToken);
                return admitted!;
            }, cancellationToken);
        await SendRunEventAsync(run, identity.UserId, "run.pending", cancellationToken);

        if (_executionStore is not null)
            return enqueueOnly ? await ToRunResponseAsync(run, cancellationToken) : await WaitAsync(run.Id, cancellationToken);
        return await ExecuteRunAsync(run, conversation, routeCandidates, identity.UserId, request.ContextRef, request.UtcOffsetMinutes, scenarioSnapshot, cancellationToken);
    }

    public async Task<AiRunResponse> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ConversationViewPermission);
        var run = await GetOwnedRunAsync(runId, identity.UserId, cancellationToken);
        return await ToRunResponseAsync(run, cancellationToken);
    }

    public async Task<IReadOnlyList<AiToolCitation>> GetCitationsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ConversationViewPermission);
        _ = await GetOwnedRunAsync(runId, identity.UserId, cancellationToken);
        return await LoadCitationsAsync(runId, cancellationToken);
    }

    public async Task CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var run = _executionStore is null
            ? await GetOwnedRunAsync(runId, identity.UserId, cancellationToken)
            : await _executionStore.FindAsync(runId, cancellationToken);
        if (run is null || run.ActorUserId != identity.UserId)
            throw new BusinessException(ErrorCode.NotFound, "AI run was not found.");
        if (run.Status is AiRunStatus.Completed or AiRunStatus.Failed or AiRunStatus.Cancelled)
        {
            return;
        }

        if (_executionStore is not null)
        {
            await _executionStore.CancelAsync(runId, cancellationToken);
            _cancellationCoordinator.RequestCancellation(runId);
            return;
        }

        run.CancellationRequestedAt ??= DateTimeOffset.UtcNow;
        if (run.Status == AiRunStatus.Pending)
        {
            CompleteRun(run, AiRunStatus.Cancelled, "run_cancelled", "The AI run was cancelled.");
        }

        _runRepository.Update(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cancellationCoordinator.RequestCancellation(run.Id);
        await SendRunEventAsync(run, identity.UserId, "run.cancellation_requested", cancellationToken);
    }

    public async Task<AiRunResponse> RetryRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var failedRun = await GetOwnedRunAsync(runId, identity.UserId, cancellationToken);
        if (failedRun.Status is not (AiRunStatus.Failed or AiRunStatus.Cancelled))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only failed or cancelled AI runs can be retried.");
        }

        var requestMessage = await _queryExecutor.FirstOrDefaultAsync(
            _messageRepository.Query().Where(message => message.Id == failedRun.RequestMessageId),
            cancellationToken)
            ?? throw new BusinessException(ErrorCode.Conflict, "The original AI request message is unavailable.");
        if (requestMessage.Content == "[expired]" || requestMessage.CreatedAt < DateTimeOffset.UtcNow.AddDays(-_configuration.ConversationRetentionDays))
            throw new BusinessException(ErrorCode.Conflict, "The original AI request has expired.");
        var context = failedRun.ScenarioVersionId.HasValue || failedRun.AgentVersion == AgentVersion ? await ReadRequestContextAsync(failedRun, cancellationToken) : null;
        return await SendMessageCoreAsync(
            failedRun.ConversationId,
            new SendAiMessageRequest { Content = requestMessage.Content, ContextRef = context?.ContextRef, UtcOffsetMinutes = context?.UtcOffsetMinutes },
            failedRun.Id,
            cancellationToken);
    }

    private async Task<AiRunResponse> ExecuteRunAsync(
        AiRun run,
        AiConversation conversation,
        IReadOnlyList<AiModelRouteCandidate> routeCandidates,
        Guid userId,
        AiContextReference? selectedReference,
        int? explicitUtcOffsetMinutes,
        AiScenarioSnapshot? scenarioSnapshot,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(scenarioSnapshot is null ? MaxRunDuration : TimeSpan.FromSeconds(scenarioSnapshot.Configuration.MaxRunSeconds));
        using var lease = _cancellationCoordinator.Begin(run.Id, timeoutSource.Token);
        var token = lease.Token;
        var stopwatch = Stopwatch.StartNew();
        var toolCallCount = 0;

        try
        {
            await ThrowIfCancellationRequestedAsync(run.Id, token);
            await StoreRequestContextAsync(run, selectedReference, explicitUtcOffsetMinutes, token);
            run.Status = AiRunStatus.Running;
            run.StartedAt ??= DateTimeOffset.UtcNow;
            run.LastHeartbeatAt = run.StartedAt;
            _runRepository.Update(run);
            await _unitOfWork.SaveChangesAsync(token);
            await SendRunEventAsync(run, userId, "run.running", token);

            var tools = _toolRegistry.GetAvailableTools()
                .Concat(run.ExecutionMode == "Background" ? [] : _actionToolRegistry.GetAvailableTools())
                .ToList();
            if (scenarioSnapshot is not null)
            {
                tools = tools.Where(t => scenarioSnapshot.Configuration.ToolCodes.Contains(t.ToolCode, StringComparer.Ordinal)).ToList();
                var authorized = new List<AiToolDefinition>();
                foreach (var tool in tools)
                {
                    try { await _scenarioRuntime!.ValidateToolAsync(run.ScenarioVersionId!.Value, tool.ToolCode, token); authorized.Add(tool); }
                    catch (BusinessException e) when (e.ErrorCode == ErrorCode.Forbidden) { }
                }
                tools = authorized;
            }
            ValidateToolCatalog(tools);
            AiStructuredResult? selectedResult = null;
            AiStructuredResultPage contexts = new([]);
            var requestMessage = await _messageRepository.GetByIdAsync(run.RequestMessageId, token);
            var question = requestMessage?.Content ?? string.Empty;
            var expectedChange = question.Trim() switch
            {
                "只看本部门" => AiFollowUpChange.CurrentDepartment,
                "再看上个月" => AiFollowUpChange.PreviousCalendarMonth,
                _ => AiFollowUpChange.None
            };
            if (expectedChange == AiFollowUpChange.PreviousCalendarMonth && !explicitUtcOffsetMinutes.HasValue)
                throw new AiFollowUpClarificationException("“上个月”需要明确时区，请在输入区选择自然月查询时区后再查询。");
            if (_followUp is not null)
            {
                if (selectedReference is not null) selectedResult = await _followUp.ResolveAsync(conversation.Id, selectedReference, token);
                else contexts = await _followUp.ReadAsync(conversation.Id, token);
            }
            else if (selectedReference is not null) throw new AiFollowUpClarificationException("追问上下文不可用，请重新明确查询条件。");
            if (selectedReference is null && _followUp is not null)
            {
                if (question.StartsWith("只看本部门", StringComparison.Ordinal) || question.StartsWith("再看上个月", StringComparison.Ordinal) ||
                    question is "重新查询" or "重新诊断" or "再查一次")
                {
                    if (contexts.Results.Count != 1 || contexts.HasUnavailableResults || contexts.IsWindowLimited)
                        throw new AiFollowUpClarificationException("请先选择要追问的结果，再明确查询条件。");
                    selectedResult = contexts.Results[0];
                    selectedReference = new(selectedResult.RunId, selectedResult.InvocationId);
                    await StoreRequestContextAsync(run, selectedReference, explicitUtcOffsetMinutes, token);
                }
            }
            if (selectedResult is not null) tools = tools.Where(item => item.ToolCode == selectedResult.ToolCode).ToList();
            var modelTools = tools.Select(t => scenarioSnapshot is null ? ToModelTool(t) : new AiModelToolDefinition
            {
                Name = t.FunctionName, Description = t.Description,
                ParametersJson = scenarioSnapshot.Tools.Single(d => d.ToolCode == t.ToolCode).ModelSchemaJson
            }).ToList();
            var toolDefinitions = tools.ToDictionary(item => item.FunctionName, StringComparer.Ordinal);
            var modelMessages = await BuildModelMessagesAsync(conversation.Id, scenarioSnapshot, token);
            var candidates = selectedResult is not null ? new[] { selectedResult } : contexts.Results.ToArray();
            if (candidates.Length > 0 || contexts.HasUnavailableResults || contexts.IsWindowLimited)
                modelMessages.Add(new AiModelGatewayMessage
                {
                    Role = "user", Content = JsonSerializer.Serialize(new
                    {
                        type = "server-query-context-data", selected = selectedReference, explicitUtcOffsetMinutes,
                        requiresClarification = selectedReference is null && (candidates.Length != 1 || contexts.HasUnavailableResults || contexts.IsWindowLimited),
                        contexts = candidates.Select(item => new { contextRef = new AiContextReference(item.RunId, item.InvocationId), item.ToolCode,
                            item.Type, item.Version, item.Context.Parameters, allowedParameters = AiStructuredResults.AllowedParameters(item.ToolCode) })
                    }, JsonOptions)
                });
            var totalInputTokens = 0;
            var totalOutputTokens = 0;
            var totalEstimatedCost = 0m;
            var allCompletedInvocationsPriced = true;
            var executionConfiguration = BuildExecutionConfiguration(run, scenarioSnapshot, tools, "ModelRequestPrepared");
            StoreExecutionConfiguration(run, executionConfiguration);
            var usageSequence = 0;
            var activeRouteIndex = 0;

            for (var round = 1; round <= executionConfiguration.MaxModelRounds; round++)
            {
                await ThrowIfCancellationRequestedAsync(run.Id, token);
                AiModelGatewayResponse? modelResponse = null;
                AiUsageLog? completedUsage = null;
                for (var routeIndex = activeRouteIndex; routeIndex < routeCandidates.Count; routeIndex++)
                {
                    run.LastHeartbeatAt = DateTimeOffset.UtcNow;
                    _runRepository.Update(run);
                    await _unitOfWork.SaveChangesAsync(token);
                    var candidate = routeCandidates[routeIndex];
                    var provider = candidate.Provider;
                    await ValidateScenarioRunAsync(run, token);
                    var maxOutputTokens = scenarioSnapshot?.Configuration.MaxTokens ?? provider.MaxTokens ?? AiCenterConstants.DefaultMaxOutputTokens;
                    if (run.ScenarioVersionId.HasValue)
                        await _scenarioRuntime!.ValidateModelAsync(run.ScenarioVersionId.Value, provider, token);
                    var circuitTarget = new AiCircuitTarget("provider", $"{run.TenantId:N}:{provider.Id:N}");
                    if (!await _circuitBreaker.AllowAsync(circuitTarget, token))
                    {
                        continue;
                    }
                    var usage = new AiUsageLog
                    {
                        TenantId = run.TenantId,
                        RunId = run.Id,
                        ProviderConfigId = provider.Id,
                        Sequence = ++usageSequence,
                        Round = round,
                        Attempt = routeIndex - activeRouteIndex + 1,
                        RouteRole = candidate.Role,
                        ModelName = provider.ModelName,
                        Status = AiInvocationStatus.Running,
                        StartedAt = DateTimeOffset.UtcNow
                    };
                    await ReserveUsageAsync(
                        usage,
                        provider,
                        userId,
                        EstimateInputTokens(modelMessages),
                        maxOutputTokens,
                        token);

                    executionConfiguration.Requests.Add(new(usage.Sequence, round, provider.Id, provider.ModelName,
                        candidate.Role.ToString(), scenarioSnapshot?.Configuration.Temperature ?? provider.Temperature,
                        maxOutputTokens, AiScenarioSnapshots.ModelFingerprint(ToConnectionSettings(provider),
                            scenarioSnapshot?.Configuration.Temperature ?? provider.Temperature, maxOutputTokens),
                        AiScenarioSnapshots.Digest(string.Join("\n", modelMessages.Where(m => m.Role == "system").Select(m => m.Content)))));
                    StoreExecutionConfiguration(run, executionConfiguration);
                    _runRepository.Update(run);
                    await _unitOfWork.SaveChangesAsync(token);
                    var modelStopwatch = Stopwatch.StartNew();
                    try
                    {
                        await ValidateScenarioRunAsync(run, token);
                        modelResponse = await _modelGateway.CompleteAsync(
                            ToConnectionSettings(provider),
                            new AiModelGatewayRequest
                            {
                                Messages = modelMessages,
                                Tools = modelTools,
                                Temperature = scenarioSnapshot?.Configuration.Temperature ?? provider.Temperature,
                                MaxTokens = maxOutputTokens
                            },
                            token);
                        await _circuitBreaker.RecordSuccessAsync(circuitTarget, CancellationToken.None);
                        usage.Status = AiInvocationStatus.Completed;
                        usage.ProviderRequestId = modelResponse.ProviderRequestId;
                        usage.InputTokens = modelResponse.InputTokens;
                        usage.OutputTokens = modelResponse.OutputTokens;
                        usage.TotalTokens = modelResponse.TotalTokens;
                        usage.FinishReason = modelResponse.FinishReason;
                        completedUsage = usage;
                        activeRouteIndex = routeIndex;
                        run.FinalProviderConfigId = provider.Id;
                        run.ModelName = provider.ModelName;
                        await ValidateScenarioRunAsync(run, token);
                    }
                    catch (AiModelGatewayException exception)
                    {
                        if (exception.IsTransient)
                        {
                            await _circuitBreaker.RecordFailureAsync(circuitTarget, exception.ErrorType, CancellationToken.None);
                        }
                        usage.Status = AiInvocationStatus.Failed;
                        usage.ErrorCode = exception.ErrorType;
                        if (run.ExecutionMode == "Background" && exception.ErrorType == "provider_rate_limited")
                        { usage.InputTokens = 0; usage.OutputTokens = 0; usage.TotalTokens = 0; }
                        if (!exception.IsTransient || routeIndex + 1 >= routeCandidates.Count ||
                            (run.ExecutionMode == "Background" && exception.ErrorType is not ("rate_limited" or "provider_rate_limited")))
                        {
                            throw;
                        }

                        run.FallbackCount++;
                    }
                    finally
                    {
                        modelStopwatch.Stop();
                        usage.CompletedAt = DateTimeOffset.UtcNow;
                        usage.DurationMilliseconds = modelStopwatch.ElapsedMilliseconds;
                        await SettleUsageAsync(usage, CancellationToken.None);
                    }

                    if (modelResponse is not null)
                    {
                        break;
                    }
                }

                if (modelResponse is null || completedUsage is null)
                {
                    throw new AiRunLimitException("provider_route_exhausted", "No AI model route candidate completed the request.");
                }

                totalInputTokens += modelResponse.InputTokens ?? 0;
                totalOutputTokens += modelResponse.OutputTokens ?? 0;
                if (completedUsage.EstimatedCost.HasValue)
                {
                    totalEstimatedCost += completedUsage.EstimatedCost.Value;
                }
                else
                {
                    allCompletedInvocationsPriced = false;
                }
                run.InputTokens = totalInputTokens;
                run.OutputTokens = totalOutputTokens;
                run.EstimatedCost = allCompletedInvocationsPriced ? totalEstimatedCost : null;

                await ThrowIfCancellationRequestedAsync(run.Id, token);
                if (modelResponse.ToolCalls.Count > 0)
                {
                    run.LastHeartbeatAt = DateTimeOffset.UtcNow;
                    _runRepository.Update(run);
                    await _unitOfWork.SaveChangesAsync(token);
                    if (toolCallCount + modelResponse.ToolCalls.Count > executionConfiguration.MaxToolCalls)
                    {
                        throw new AiRunLimitException("tool_call_limit_exceeded", "The AI run exceeded the tool call limit.");
                    }

                    modelMessages.Add(new AiModelGatewayMessage
                    {
                        Role = "assistant",
                        Content = modelResponse.Content,
                        ToolCalls = modelResponse.ToolCalls
                    });
                    foreach (var toolCall in modelResponse.ToolCalls)
                    {
                        await ThrowIfCancellationRequestedAsync(run.Id, token);
                        if (!toolDefinitions.TryGetValue(toolCall.Name, out var definition))
                        {
                            throw new AiRunLimitException("unknown_tool", "The AI provider requested an unavailable tool.");
                        }

                        toolCallCount++;
                        var toolResult = await ExecuteToolAsync(run, userId, toolCall, definition, selectedReference, explicitUtcOffsetMinutes, expectedChange, token);
                        modelMessages.Add(new AiModelGatewayMessage
                        {
                            Role = "tool",
                            ToolCallId = toolCall.Id,
                            Content = toolResult.StructuredResult is null ? toolResult.ContentJson :
                                JsonSerializer.Serialize(toolResult.StructuredResult, JsonOptions)
                        });
                    }

                    continue;
                }

                var responseContent = toolCallCount == 0
                    ? "当前回答没有经过系统工具验证，无法提供数据结论。请明确要追问的结果、查询对象和过滤条件；按自然月查询还需提供时区或 UTC 偏移。"
                    : NormalizeModelResponse(modelResponse.Content);
                await ValidateScenarioRunAsync(run, token);
                await _unitOfWork.ExecuteInTransactionAsync(async commitToken =>
                {
                var responseMessage = await AddAssistantMessageAsync(
                    conversation,
                    responseContent,
                    modelResponse.OutputTokens,
                    commitToken);
                run.ResponseMessageId = responseMessage.Id;
                run.InputTokens = totalInputTokens;
                run.OutputTokens = totalOutputTokens;
                run.EstimatedCost = allCompletedInvocationsPriced ? totalEstimatedCost : null;
                CompleteRun(run, AiRunStatus.Completed, null, null, stopwatch.ElapsedMilliseconds);
                _runRepository.Update(run);
                await _unitOfWork.SaveChangesAsync(commitToken);
                }, token);
                await _circuitBreaker.RecordSuccessAsync(
                    new AiCircuitTarget("agent", $"{run.TenantId:N}:{run.AgentCode}"),
                    CancellationToken.None);
                await SendRunEventAsync(run, userId, "run.completed", token);
                return await ToRunResponseAsync(run, token);
            }

            throw new AiRunLimitException("model_round_limit_exceeded", "The AI run exceeded the model round limit.");
        }
        catch (AiFollowUpClarificationException exception)
        {
            try
            {
                await ValidateScenarioRunAsync(run, CancellationToken.None);
                await _unitOfWork.ExecuteInTransactionAsync(async commitToken =>
                {
                var responseMessage = await AddAssistantMessageAsync(conversation, exception.Message, null, CancellationToken.None);
                responseMessage.ModelGenerated = false;
                _messageRepository.Update(responseMessage);
                run.ResponseMessageId = responseMessage.Id;
                CompleteRun(run, AiRunStatus.Completed, null, null, stopwatch.ElapsedMilliseconds);
                _runRepository.Update(run);
                await _unitOfWork.SaveChangesAsync(commitToken);
                }, CancellationToken.None);
                return await ToRunResponseAsync(run, CancellationToken.None);
            }
            catch (BusinessException)
            {
                CompleteRun(run, AiRunStatus.Failed, "scenario_unavailable", "The AI scenario is no longer available.", stopwatch.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            var explicitlyCancelled = run.CancellationRequestedAt.HasValue ||
                await _cancellationProbe.IsCancellationRequestedAsync(run.Id, CancellationToken.None);
            CompleteRun(
                run,
                explicitlyCancelled ? AiRunStatus.Cancelled : AiRunStatus.Failed,
                explicitlyCancelled ? "run_cancelled" : cancellationToken.IsCancellationRequested ? "run_interrupted" : "run_timeout",
                explicitlyCancelled ? "The AI run was cancelled." : "The AI run exceeded the execution time limit.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (AiModelGatewayException exception)
        {
            CompleteRun(run, AiRunStatus.Failed, exception.ErrorType, "The AI provider request failed.", stopwatch.ElapsedMilliseconds);
        }
        catch (AiRunLimitException exception)
        {
            CompleteRun(run, AiRunStatus.Failed, exception.Code, exception.Message, stopwatch.ElapsedMilliseconds);
        }
        catch (BusinessException exception)
        {
            var budgetExhausted = exception.ErrorCode == ErrorCode.TooManyRequests;
            CompleteRun(
                run,
                AiRunStatus.Failed,
                budgetExhausted ? "ai_budget_exhausted" : "tool_execution_failed",
                budgetExhausted ? "The configured AI budget has been exhausted." : "The AI tool execution failed.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (AiRunLeaseLostException) { throw; }
        catch (Exception exception) when (IsConcurrencyException(exception))
        {
            // The watchdog may have reclaimed this run on another instance.
            // Do not attempt a second write with a stale RowVersion/lease.
            run.Status = AiRunStatus.Failed;
            run.ErrorCode = "run_reclaimed";
            run.ErrorSummary = "The AI run was reclaimed by the watchdog.";
            run.CompletedAt ??= DateTimeOffset.UtcNow;
            return await ToRunResponseAsync(run, CancellationToken.None);
        }
        catch (Exception)
        {
            CompleteRun(run, AiRunStatus.Failed, "run_failed", "The AI run failed.", stopwatch.ElapsedMilliseconds);
        }

        try
        {
            if (_executionFence is not null) _executionFence.IsSettlement = true;
            run.ResponseMessageId = null;
            var unfinishedUsage = await _queryExecutor.ToListAsync(_usageLogRepository.Query()
                .Where(u => u.RunId == run.Id && u.Status == AiInvocationStatus.Running), CancellationToken.None);
            foreach (var usage in unfinishedUsage)
            {
                usage.SettleCost(); usage.Status = AiInvocationStatus.Failed; usage.ErrorCode = run.ErrorCode;
                usage.CompletedAt ??= DateTimeOffset.UtcNow; _usageLogRepository.Update(usage);
            }
            _runRepository.Update(run);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception) when (IsConcurrencyException(exception))
        {
            run.Status = AiRunStatus.Failed;
            run.ErrorCode = "run_reclaimed";
            run.ErrorSummary = "The AI run was reclaimed by the watchdog.";
            run.CompletedAt ??= DateTimeOffset.UtcNow;
            return await ToRunResponseAsync(run, CancellationToken.None);
        }
        if (run.Status == AiRunStatus.Failed && ShouldRecordAgentFailure(run.ErrorCode))
        {
            await _circuitBreaker.RecordFailureAsync(
                new AiCircuitTarget("agent", $"{run.TenantId:N}:{run.AgentCode}"),
                run.ErrorCode ?? "run_failed",
                CancellationToken.None);
        }
        await SendRunEventAsync(run, userId, run.Status switch
        {
            AiRunStatus.Completed => "run.completed", AiRunStatus.Cancelled => "run.cancelled", _ => "run.failed"
        }, CancellationToken.None);
        return await ToRunResponseAsync(run, CancellationToken.None);
    }

    private async Task<AiToolExecutionResult> ExecuteToolAsync(
        AiRun run,
        Guid userId,
        AiModelToolCall toolCall,
        AiToolDefinition definition,
        AiContextReference? selectedReference,
        int? explicitUtcOffsetMinutes,
        AiFollowUpChange expectedChange,
        CancellationToken cancellationToken)
    {
        await ValidateScenarioRunAsync(run, cancellationToken);
        if (run.ScenarioVersionId.HasValue)
            await _scenarioRuntime!.ValidateToolAsync(run.ScenarioVersionId.Value, definition.ToolCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(toolCall.Id) || toolCall.Id.Length > 100)
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid AI tool invocation ID.");
        var argumentsJson = _followUp is null ? toolCall.ArgumentsJson : await _followUp.PrepareArgumentsAsync(
            run.ConversationId, definition.ToolCode, toolCall.ArgumentsJson, selectedReference, explicitUtcOffsetMinutes, expectedChange, cancellationToken);
        var invocation = new AiToolInvocation
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            InvocationId = toolCall.Id,
            ToolCode = definition.ToolCode,
            ToolVersion = definition.Version,
            Status = AiInvocationStatus.Running,
            InputDigest = ComputeDigest(argumentsJson),
            SourceSystem = "PermissionSystem",
            StartedAt = DateTimeOffset.UtcNow
        };
        await _toolInvocationRepository.AddAsync(invocation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SendToolEventAsync(run, userId, invocation, "tool.running", cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var circuitTarget = new AiCircuitTarget("tool", $"{run.TenantId:N}:{definition.ToolCode}");
        var circuitAllowed = false;
        try
        {
            if (!await _circuitBreaker.AllowAsync(circuitTarget, cancellationToken))
            {
                throw new BusinessException(ErrorCode.TooManyRequests, "The AI tool circuit is temporarily open.");
            }
            circuitAllowed = true;
            AiToolExecutionResult result;
            if (_actionToolRegistry.IsActionTool(definition.ToolCode))
            {
                var actionResult = await _actionToolRegistry.ExecuteAsync(
                    definition.ToolCode,
                    new AiActionDraftContext
                    {
                        TenantId = run.TenantId,
                        ActorUserId = userId,
                        ConversationId = run.ConversationId,
                        RunId = run.Id,
                        InvocationId = toolCall.Id
                    },
                    argumentsJson,
                    cancellationToken);
                result = new AiToolExecutionResult
                {
                    ContentJson = actionResult.ContentJson,
                    RowCount = 1,
                    IncludeCitation = false,
                    Citation = new AiToolCitation
                    {
                        ToolCode = definition.ToolCode,
                        ToolVersion = definition.Version,
                        QueriedAt = DateTimeOffset.UtcNow,
                        RowCount = 0
                    }
                };
            }
            else
            {
                result = await _toolRegistry.ExecuteAsync(
                    definition.ToolCode,
                    argumentsJson,
                    cancellationToken);
            }
            await ValidateScenarioRunAsync(run, cancellationToken);
            invocation.Status = AiInvocationStatus.Completed;
            await _circuitBreaker.RecordSuccessAsync(circuitTarget, CancellationToken.None);
            invocation.OutputDigest = ComputeDigest(result.ContentJson);
            invocation.SourceSystem = result.Citation.SourceSystem;
            invocation.DatasetCode = result.Citation.DatasetCode;
            invocation.DatasetVersion = result.Citation.DatasetVersion;
            invocation.RowCount = result.RowCount;
            invocation.IsTruncated = result.IsTruncated;
            invocation.CitationJson = result.IncludeCitation
                ? JsonSerializer.Serialize(result.Citation, JsonOptions)
                : null;
            var storedContent = result.ContentJson;
            if (result.StructuredResult is not null && AiStructuredResults.IsSupported(definition.ToolCode))
            {
                result.StructuredResult.RunId = run.Id;
                result.StructuredResult.InvocationId = invocation.InvocationId;
                storedContent = AiStructuredResults.SerializeBounded(result.StructuredResult);
                invocation.OutputDigest = ComputeDigest(storedContent);
                invocation.IsTruncated = result.StructuredResult.IsTruncated;
                invocation.RowCount = result.StructuredResult.Citation.RowCount;
                invocation.CitationJson = JsonSerializer.Serialize(result.StructuredResult.Citation, JsonOptions);
            }
            else if (definition.ToolCode == PermissionDiagnosticAiToolHandler.ToolCode && result.PermissionDiagnostic is not null)
            {
                storedContent = JsonSerializer.Serialize(new AiPermissionDiagnosticEnvelope
                {
                    RunId = run.Id,
                    InvocationId = invocation.InvocationId,
                    Data = result.PermissionDiagnostic
                }, JsonOptions);
            }
            await AddToolMessageAsync(run.ConversationId, run.TenantId, storedContent, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            invocation.Status = AiInvocationStatus.Cancelled;
            invocation.ErrorCode = "tool_cancelled";
            throw;
        }
        catch (Exception)
        {
            if (circuitAllowed)
            {
                await _circuitBreaker.RecordFailureAsync(circuitTarget, "tool_execution_failed", CancellationToken.None);
            }
            invocation.Status = AiInvocationStatus.Failed;
            invocation.ErrorCode = "tool_execution_failed";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            invocation.CompletedAt = DateTimeOffset.UtcNow;
            invocation.DurationMilliseconds = stopwatch.ElapsedMilliseconds;
            _toolInvocationRepository.Update(invocation);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            await SendToolEventAsync(run, userId, invocation, $"tool.{invocation.Status.ToString().ToLowerInvariant()}", CancellationToken.None);
        }
    }

    private async Task<List<AiModelGatewayMessage>> BuildModelMessagesAsync(
        Guid conversationId,
        AiScenarioSnapshot? scenarioSnapshot,
        CancellationToken cancellationToken)
    {
        var history = await _queryExecutor.ToListAsync(
            _messageRepository.Query()
                .Where(entity =>
                    entity.ConversationId == conversationId &&
                    entity.Role == AiMessageRole.User)
                .OrderByDescending(entity => entity.Sequence)
                .Take(scenarioSnapshot?.Configuration.MaxHistoryMessages ?? MaxHistoryMessages),
            cancellationToken);
        var messages = new List<AiModelGatewayMessage>
        {
            new()
            {
                Role = "system",
                Content = scenarioSnapshot?.SystemPrompt ?? AiScenarioCatalog.SafetyPrompt
            }
        };
        messages.AddRange(history
            .OrderBy(entity => entity.Sequence)
            .Select(entity => new AiModelGatewayMessage
            {
                Role = "user",
                Content = entity.Content
            }));
        return messages;
    }

    private async Task AddToolMessageAsync(
        Guid conversationId,
        Guid tenantId,
        string content,
        CancellationToken cancellationToken)
    {
        var last = await _queryExecutor.FirstOrDefaultAsync(
            _messageRepository.Query()
                .Where(entity => entity.ConversationId == conversationId)
                .OrderByDescending(entity => entity.Sequence),
            cancellationToken);
        await _messageRepository.AddAsync(new AiMessage
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Role = AiMessageRole.Tool,
            Content = content,
            ContentClassification = AiContentClassification.Confidential,
            ContentDigest = ComputeDigest(content),
            Sequence = (last?.Sequence ?? 0) + 1
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AiMessage> AddAssistantMessageAsync(
        AiConversation conversation,
        string content,
        int? tokenCount,
        CancellationToken cancellationToken)
    {
        var last = await _queryExecutor.FirstOrDefaultAsync(
            _messageRepository.Query()
                .Where(entity => entity.ConversationId == conversation.Id)
                .OrderByDescending(entity => entity.Sequence),
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var message = new AiMessage
        {
            TenantId = conversation.TenantId,
            ConversationId = conversation.Id,
            Role = AiMessageRole.Assistant,
            Content = content,
            ContentDigest = ComputeDigest(content),
            TokenCount = tokenCount,
            Sequence = (last?.Sequence ?? 0) + 1,
            ModelGenerated = true
        };
        conversation.LastMessageAt = now;
        conversation.RetentionUntil = now.AddDays(_configuration.ConversationRetentionDays);
        _conversationRepository.Update(conversation);
        await _messageRepository.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return message;
    }

    private async Task<AiConversation> GetOwnedConversationAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _queryExecutor.FirstOrDefaultAsync(
            _conversationRepository.Query().Where(entity =>
                entity.Id == id && entity.UserId == userId && entity.Status != AiConversationStatus.Deleted),
            cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "AI conversation was not found.");
    }

    private async Task<AiRun> GetOwnedRunAsync(Guid runId, Guid userId, CancellationToken cancellationToken)
    {
        return await _queryExecutor.FirstOrDefaultAsync(
            _runRepository.Query().Where(entity => entity.Id == runId && entity.ActorUserId == userId),
            cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "AI run was not found.");
    }

    private async Task<AiRunResponse> ToRunResponseAsync(AiRun run, CancellationToken cancellationToken)
    {
        AiMessage? responseMessage = null;
        if (run.ResponseMessageId.HasValue)
        {
            responseMessage = await _messageRepository.GetByIdAsync(run.ResponseMessageId.Value, cancellationToken);
        }

        var structured = _structuredReader is null ? new AiStructuredResultPage([]) :
            await _structuredReader.ReadAsync(run.ConversationId, run.Id, cancellationToken);
        return new AiRunResponse
        {
            ProgressVersion = run.ProgressVersion,
            ToolProgress = await _queryExecutor.ToListAsync(_toolInvocationRepository.Query().Where(t => t.RunId == run.Id)
                .OrderBy(t => t.CreatedAt).Take(100).Select(t => new AiToolProgress(t.InvocationId, t.ToolCode, t.Status, t.CompletedAt)), cancellationToken),
            Id = run.Id,
            ScenarioVersionId = run.ScenarioVersionId,
            ScenarioContentHash = run.ScenarioContentHash,
            BuildIdentity = run.BuildIdentity,
            ExecutionConfigurationHash = run.ExecutionConfigurationHash,
            HistoricalConfigurationIncomplete = run.ExecutionConfigurationJson is null,
            ConversationId = run.ConversationId,
            RequestMessageId = run.RequestMessageId,
            ResponseMessageId = run.ResponseMessageId,
            Status = run.Status,
            ModelName = run.ModelName,
            TraceId = run.TraceId,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            DurationMilliseconds = run.DurationMilliseconds,
            InputTokens = run.InputTokens,
            OutputTokens = run.OutputTokens,
            EstimatedCost = run.EstimatedCost,
            FallbackCount = run.FallbackCount,
            ErrorCode = run.ErrorCode,
            ErrorSummary = run.ErrorSummary,
            CancellationRequestedAt = run.CancellationRequestedAt,
            ResponseMessage = responseMessage is null ? null : ToMessageResponse(responseMessage),
            Citations = await LoadCitationsAsync(run.Id, cancellationToken),
            StructuredResults = structured.Results,
            StructuredResultsUnavailable = structured.HasUnavailableResults,
            StructuredResultsWindowLimited = structured.IsWindowLimited,
            PermissionDiagnostics = _structuredReader is not null ? DiagnosticProjection(structured.Results) : _diagnosticReader is null ? [] :
                await _diagnosticReader.ReadAsync(run.ConversationId, run.Id, cancellationToken),
            DocumentDrafts = CanReadDocumentDrafts()
                ? await _draftReader.GetByRunAsync(run.Id, cancellationToken)
                : []
        };
    }

    private async Task<IReadOnlyList<AiToolCitation>> LoadCitationsAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var invocations = await _queryExecutor.ToListAsync(
            _toolInvocationRepository.Query()
                .Where(entity => entity.RunId == runId && entity.CitationJson != null)
                .OrderBy(entity => entity.CreatedAt),
            cancellationToken);
        var citations = new List<AiToolCitation>(invocations.Count);
        foreach (var invocation in invocations)
        {
            try
            {
                var citation = JsonSerializer.Deserialize<AiToolCitation>(invocation.CitationJson!, JsonOptions);
                if (citation is not null)
                {
                    citations.Add(citation);
                }
            }
            catch (JsonException)
            {
                throw new BusinessException(ErrorCode.InternalServerError, "AI citation audit data is invalid.");
            }
        }

        return citations;
    }

    private async Task ThrowIfCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await _cancellationProbe.IsCancellationRequestedAsync(runId, cancellationToken))
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private (Guid UserId, Guid TenantId) EnsureAccess(string permission)
    {
        if (!_configuration.Enabled)
        {
            throw new BusinessException(ErrorCode.Forbidden, "AI center is disabled by the global kill switch.");
        }

        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue ||
            !_currentUserService.TenantId.HasValue)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "A valid user and tenant context is required.");
        }

        var tenantId = _currentUserService.TenantId.Value;
        if (!_configuration.AllowedTenantIds.Contains(tenantId) || !_currentUserService.HasPermission(permission))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to use this AI capability.");
        }

        return (_currentUserService.UserId.Value, tenantId);
    }

    private bool CanReadDocumentDrafts()
    {
        return _currentUserService.HasPermission(AiCenterConstants.DocumentDraftPermission) &&
            _currentUserService.HasPermission("demo-business-order:create");
    }

    private async Task ValidateScenarioRunAsync(AiRun run, CancellationToken cancellationToken)
    {
        if (run.ExecutionMode == "Background")
        {
            var actor = await _identityValidator!.ValidateAsync(run, cancellationToken);
            if (_currentUserService is AiRunExecutionIdentity background) background.Set(actor, run.ActorSessionId!);
            await ThrowIfCancellationRequestedAsync(run.Id, cancellationToken);
        }
        if (run.ScenarioVersionId.HasValue)
        {
            var snapshot = await (_scenarioRuntime ?? throw new BusinessException(ErrorCode.Conflict, "AI scenarios are unavailable."))
                .ValidateAsync(run.ScenarioVersionId.Value, cancellationToken);
            if (run.ScenarioContentHash != AiScenarioSnapshots.Digest(AiScenarioSnapshots.Json(snapshot)))
                throw new BusinessException(ErrorCode.Conflict, "AI run version integrity check failed.");
        }
    }

    private static AiExecutionConfiguration BuildExecutionConfiguration(AiRun run, AiScenarioSnapshot? snapshot,
        IEnumerable<AiToolDefinition> tools, string stage) => new()
    {
        Stage = stage,
        Kind = snapshot is null ? "BuiltinCompatibility" : "PublishedScenario",
        BuildIdentity = run.BuildIdentity!, ScenarioContentHash = run.ScenarioContentHash,
        BasePromptHash = AiScenarioSnapshots.Digest(snapshot?.SystemPrompt ?? AiScenarioCatalog.SafetyPrompt),
        Tools = tools.Select(AiScenarioSnapshots.Tool).ToArray(),
        MaxModelRounds = snapshot?.Configuration.MaxModelRounds ?? MaxModelRounds,
        MaxToolCalls = snapshot?.Configuration.MaxToolCalls ?? MaxToolCalls,
        MaxHistoryMessages = snapshot?.Configuration.MaxHistoryMessages ?? MaxHistoryMessages,
        MaxRunSeconds = snapshot?.Configuration.MaxRunSeconds ?? 90
    };

    private static void StoreExecutionConfiguration(AiRun run, AiExecutionConfiguration configuration)
    {
        run.ExecutionConfigurationJson = AiScenarioSnapshots.Json(configuration);
        run.ExecutionConfigurationHash = AiScenarioSnapshots.Digest(run.ExecutionConfigurationJson);
    }

    private static AiModelToolDefinition ToModelTool(AiToolDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.FunctionName))
        {
            throw new BusinessException(ErrorCode.InternalServerError, "AI tool function name is missing.");
        }

        return new AiModelToolDefinition
        {
            Name = definition.FunctionName,
            Description = definition.Description,
            ParametersJson = AiFollowUpContextService.ExtendModelSchema(definition)
        };
    }

    private static void ValidateToolCatalog(IReadOnlyCollection<AiToolDefinition> tools)
    {
        var duplicateToolCode = tools
            .GroupBy(definition => definition.ToolCode, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        var duplicateFunctionName = tools
            .GroupBy(definition => definition.FunctionName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateToolCode is not null || duplicateFunctionName is not null)
        {
            throw new BusinessException(
                ErrorCode.InternalServerError,
                "The AI tool catalog contains duplicate identifiers.");
        }
    }

    private AiProviderConnectionSettings ToConnectionSettings(AiProviderConfig provider)
    {
        IReadOnlyCollection<string> allowedHosts;
        try
        {
            allowedHosts = JsonSerializer.Deserialize<string[]>(provider.AllowedHostsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new BusinessException(ErrorCode.InternalServerError, "AI provider host policy is invalid.");
        }

        return new AiProviderConnectionSettings
        {
            ProviderType = provider.ProviderType,
            BaseUrl = provider.BaseUrl,
            ChatCompletionsPath = provider.ChatCompletionsPath,
            ApiKey = _valueProtector.Unprotect(provider.ApiKeyEncrypted),
            ModelName = provider.ModelName,
            TimeoutSeconds = provider.TimeoutSeconds,
            AllowInsecureHttp = provider.AllowInsecureHttp,
            AllowPrivateNetwork = provider.AllowPrivateNetwork,
            AllowedHosts = allowedHosts
        };
    }

    private async Task<IReadOnlyList<AiModelRouteCandidate>> ResolveRouteCandidatesAsync(
        string agentCode,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (_modelRouteService is not null)
        {
            return await _modelRouteService.ResolveAsync(agentCode, conversationId, cancellationToken);
        }

        var provider = await _queryExecutor.FirstOrDefaultAsync(
            _providerRepository.Query().Where(entity => entity.IsDefault && entity.IsEnabled),
            cancellationToken)
            ?? throw new BusinessException(ErrorCode.Conflict, "No enabled default AI provider is configured.");
        AiProviderService.EnsureComplianceConfirmed(provider);
        return [new AiModelRouteCandidate(provider, AiModelRouteRole.Primary)];
    }

    private async Task ReserveUsageAsync(
        AiUsageLog usage,
        AiProviderConfig provider,
        Guid userId,
        int estimatedInputTokens,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        if (_budgetService is not null)
        {
            await _budgetService.ReserveInvocationAsync(
                usage,
                provider,
                userId,
                estimatedInputTokens,
                maxOutputTokens,
                cancellationToken);
            return;
        }

        usage.InputTokenPricePerMillion = provider.InputTokenPricePerMillion;
        usage.EstimatedInputTokens = Math.Max(estimatedInputTokens, 0);
        usage.EstimatedOutputTokens = Math.Max(maxOutputTokens, 0);
        usage.OutputTokenPricePerMillion = provider.OutputTokenPricePerMillion;
        usage.PricingCurrency = provider.PricingCurrency;
        await _usageLogRepository.AddAsync(usage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SettleUsageAsync(AiUsageLog usage, CancellationToken cancellationToken)
    {
        if (_budgetService is not null)
        {
            await _budgetService.SettleInvocationAsync(usage, cancellationToken);
            return;
        }

        usage.SettleCost();
        _usageLogRepository.Update(usage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static int EstimateInputTokens(IReadOnlyCollection<AiModelGatewayMessage> messages)
    {
        var characters = messages.Sum(message =>
            (message.Content?.Length ?? 0) +
            message.ToolCalls.Sum(call => call.Name.Length + call.ArgumentsJson.Length));
        return Math.Max(1, characters);
    }

    private async Task SendRunEventAsync(
        AiRun run,
        Guid userId,
        string eventType,
        CancellationToken cancellationToken)
    {
        await _realtimeSender.SendToUserAsync(userId, new AiRunRealtimeMessage
        {
            RunId = run.Id,
            ConversationId = run.ConversationId,
            EventType = eventType,
            Status = run.Status,
            ErrorCode = run.ErrorCode,
            OccurredAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private async Task SendToolEventAsync(
        AiRun run,
        Guid userId,
        AiToolInvocation invocation,
        string eventType,
        CancellationToken cancellationToken)
    {
        await _realtimeSender.SendToUserAsync(userId, new AiRunRealtimeMessage
        {
            RunId = run.Id,
            ConversationId = run.ConversationId,
            EventType = eventType,
            Status = run.Status,
            ToolCode = invocation.ToolCode,
            ToolStatus = invocation.Status,
            ErrorCode = invocation.ErrorCode,
            OccurredAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static void CompleteRun(
        AiRun run,
        AiRunStatus status,
        string? errorCode,
        string? errorSummary,
        long? durationMilliseconds = null)
    {
        run.Status = status;
        run.ErrorCode = errorCode;
        run.ErrorSummary = errorSummary;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.DurationMilliseconds = durationMilliseconds;
    }

    private static string NormalizeQuestion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI message content is required.");
        }

        var content = value.Trim();
        if (content.Length > MaxQuestionLength)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"AI message content cannot exceed {MaxQuestionLength} characters.");
        }

        return content;
    }

    private static string NormalizeModelResponse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AiRunLimitException("empty_model_response", "The AI provider returned an empty final response.");
        }

        return value.Trim();
    }

    private static string NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "新会话";
        }

        var title = value.Trim().ReplaceLineEndings(" ");
        return title.Length <= 200 ? title : title[..200];
    }

    private static bool IsDefaultTitle(string value) => string.Equals(value, "新会话", StringComparison.Ordinal);

    private static string ComputeDigest(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static bool IsConcurrencyException(Exception exception) =>
        string.Equals(exception.GetType().Name, "DbUpdateConcurrencyException", StringComparison.Ordinal) ||
        (exception.InnerException is not null && IsConcurrencyException(exception.InnerException));

    private static bool ShouldRecordAgentFailure(string? errorCode) =>
        !string.Equals(errorCode, "unknown_tool", StringComparison.Ordinal) &&
        !string.Equals(errorCode, "tool_execution_failed", StringComparison.Ordinal) &&
        !string.Equals(errorCode, "tool_call_limit_exceeded", StringComparison.Ordinal) &&
        !string.Equals(errorCode, "model_round_limit_exceeded", StringComparison.Ordinal) &&
        !string.Equals(errorCode, "ai_budget_exhausted", StringComparison.Ordinal);

    private static AiConversationListResponse ToListResponse(AiConversation entity)
    {
        return new AiConversationListResponse
        {
            Id = entity.Id,
            ScenarioId = entity.ScenarioId,
            ScenarioVersionId = entity.ScenarioVersionId,
            HistoricalConfigurationIncomplete = !entity.ScenarioVersionId.HasValue,
            Title = entity.Title,
            Status = entity.Status,
            LastMessageAt = entity.LastMessageAt,
            LastRunAt = entity.LastRunAt
        };
    }

    private static AiConversationDetailResponse ToDetailResponse(
        AiConversation entity,
        IReadOnlyCollection<AiMessage> messages,
        IReadOnlyDictionary<Guid, Guid> responseRunIds,
        IReadOnlyDictionary<Guid, AiFeedbackResponse> feedbackByRun,
        IReadOnlyList<AiDocumentDraftResponse> drafts,
        IReadOnlyList<AiPermissionDiagnosticResult>? diagnostics = null, AiStructuredResultPage? structured = null,
        AiRunResponse? latestRun = null)
    {
        return new AiConversationDetailResponse
        {
            LatestRun = latestRun,
            Id = entity.Id,
            ScenarioId = entity.ScenarioId,
            ScenarioVersionId = entity.ScenarioVersionId,
            HistoricalConfigurationIncomplete = !entity.ScenarioVersionId.HasValue,
            Title = entity.Title,
            Status = entity.Status,
            LastMessageAt = entity.LastMessageAt,
            LastRunAt = entity.LastRunAt,
            AgentCode = entity.AgentCode,
            AgentVersion = entity.AgentVersion,
            Messages = messages
                .Where(message => message.Role != AiMessageRole.Tool)
                .OrderBy(message => message.Sequence)
                .Select(message => ToMessageResponse(
                    message,
                    responseRunIds.TryGetValue(message.Id, out var runId) ? runId : null,
                    responseRunIds.TryGetValue(message.Id, out runId) && feedbackByRun.TryGetValue(runId, out var item)
                        ? item
                        : null))
                .ToList(),
            DocumentDrafts = drafts,
            PermissionDiagnostics = diagnostics ?? [],
            StructuredResults = structured?.Results ?? [],
            StructuredResultsUnavailable = structured?.HasUnavailableResults ?? false,
            StructuredResultsWindowLimited = structured?.IsWindowLimited ?? false
        };
    }

    private static AiMessageResponse ToMessageResponse(
        AiMessage entity,
        Guid? runId = null,
        AiFeedbackResponse? feedback = null)
    {
        return new AiMessageResponse
        {
            Id = entity.Id,
            Role = entity.Role,
            Content = entity.Content,
            Sequence = entity.Sequence,
            ModelGenerated = entity.ModelGenerated,
            CreatedAt = entity.CreatedAt,
            RunId = runId,
            Feedback = feedback
        };
    }

    private static IReadOnlyList<AiPermissionDiagnosticResult> DiagnosticProjection(IReadOnlyList<AiStructuredResult> results) =>
        results.Where(item => item.Diagnostic is not null).Select(item =>
            new AiPermissionDiagnosticResult(item.RunId, item.InvocationId, item.Diagnostic!)).ToList();

    private async Task StoreRequestContextAsync(AiRun run, AiContextReference? reference, int? utcOffsetMinutes, CancellationToken cancellationToken)
    {
        var content = JsonSerializer.Serialize(new StoredRequestContext
        {
            RunId = run.Id, RequestMessageId = run.RequestMessageId, ContextRef = reference, UtcOffsetMinutes = utcOffsetMinutes
        }, JsonOptions);
        var runText = run.Id.ToString();
        var existing = await _queryExecutor.FirstOrDefaultAsync(_messageRepository.Query().Where(message =>
            message.TenantId == run.TenantId && message.ConversationId == run.ConversationId && message.Role == AiMessageRole.Tool &&
            !message.ModelGenerated && message.Content.StartsWith("{\"type\":\"ai-request-context\",") && message.Content.Contains(runText))
            .OrderByDescending(message => message.Sequence), cancellationToken);
        if (existing is not null)
        {
            if (existing.ContentDigest != ComputeDigest(existing.Content))
                throw new BusinessException(ErrorCode.Conflict, "The AI request context is invalid.");
            var context = JsonSerializer.Deserialize<StoredRequestContext>(existing.Content, JsonOptions);
            if (context?.RunId != run.Id || context.RequestMessageId != run.RequestMessageId)
                throw new BusinessException(ErrorCode.Conflict, "The AI request context is invalid.");
            existing.Content = content;
            existing.ContentDigest = ComputeDigest(content);
            _messageRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }
        await AddToolMessageAsync(run.ConversationId, run.TenantId, content, cancellationToken);
    }

    private async Task<StoredRequestContext> ReadRequestContextAsync(AiRun run, CancellationToken cancellationToken)
    {
        var runText = run.Id.ToString();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_configuration.ConversationRetentionDays);
        var candidates = await _queryExecutor.ToListAsync(_messageRepository.Query().Where(message =>
            message.TenantId == run.TenantId && message.ConversationId == run.ConversationId && message.Role == AiMessageRole.Tool &&
            !message.ModelGenerated && message.CreatedAt >= cutoff && message.Content.Contains(runText) && message.Content.Length <= 1024)
            .OrderByDescending(message => message.Sequence).Take(20), cancellationToken);
        foreach (var message in candidates)
        {
            if (message.ContentDigest != ComputeDigest(message.Content)) continue;
            StoredRequestContext? context;
            try { context = JsonSerializer.Deserialize<StoredRequestContext>(message.Content, JsonOptions); }
            catch (JsonException) { continue; }
            if (context?.Type == "ai-request-context" && context.Version == 1 && context.RunId == run.Id && context.RequestMessageId == run.RequestMessageId)
                return context;
        }
        throw new BusinessException(ErrorCode.Conflict, "The original AI query context is unavailable; submit a new explicit query.");
    }

    private sealed class StoredRequestContext
    {
        public string Type { get; init; } = "ai-request-context";
        public int Version { get; init; } = 1;
        public Guid RunId { get; init; }
        public Guid RequestMessageId { get; init; }
        public AiContextReference? ContextRef { get; init; }
        public int? UtcOffsetMinutes { get; init; }
    }

    private sealed class AiRunLimitException : Exception
    {
        public AiRunLimitException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
