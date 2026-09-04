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

public sealed class AiConversationService : IAiConversationService
{
    private const string AgentCode = "permission-platform-agent";
    private const string AgentVersion = "2.0";
    private const string PromptVersion = "2.0";
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
        IAiCircuitBreaker? circuitBreaker = null)
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
        _configuration = configuration ?? new DefaultAiCenterConfiguration();
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
                .Where(entity => entity.ConversationId == id)
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
        return ToDetailResponse(
            conversation,
            messages,
            responseRuns.ToDictionary(entity => entity.ResponseMessageId!.Value, entity => entity.Id),
            feedback.ToDictionary(entity => entity.RunId, AiOperationsService.ToFeedbackResponse),
            drafts);
    }

    public async Task<AiConversationDetailResponse> CreateAsync(
        CreateAiConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var now = DateTimeOffset.UtcNow;
        var conversation = new AiConversation
        {
            TenantId = identity.TenantId,
            UserId = identity.UserId,
            AgentCode = AgentCode,
            AgentVersion = AgentVersion,
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
        CancellationToken cancellationToken)
    {
        var identity = EnsureAccess(AiCenterConstants.ChatUsePermission);
        var content = NormalizeQuestion(request.Content);
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

        var routeCandidates = await ResolveRouteCandidatesAsync(conversationId, cancellationToken);
        var provider = routeCandidates[0].Provider;
        var agentCircuitTarget = new AiCircuitTarget("agent", $"{identity.TenantId:N}:{AgentCode}");
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
            ContentDigest = ComputeDigest(content),
            Sequence = (lastMessage?.Sequence ?? 0) + 1
        };
        if (lastMessage is null && IsDefaultTitle(conversation.Title))
        {
            conversation.Title = NormalizeTitle(content);
        }

        conversation.LastMessageAt = now;
        conversation.LastRunAt = now;
        conversation.RetentionUntil = now.AddDays(_configuration.ConversationRetentionDays);

        var run = await _admissionService.ExecuteAsync(
            new AiRunAdmissionRequest(identity.TenantId, identity.UserId, AgentCode, provider.Id, EstimateInputTokens([new AiModelGatewayMessage { Role = "user", Content = content }]) + (provider.MaxTokens ?? 4096)),
            async () =>
            {
                _conversationRepository.Update(conversation);
                await _messageRepository.AddAsync(requestMessage, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var newRun = new AiRun
                {
                    TenantId = identity.TenantId,
                    ConversationId = conversationId,
                    RequestMessageId = requestMessage.Id,
                    ProviderConfigId = provider.Id,
                    ActorUserId = identity.UserId,
                    AgentCode = AgentCode,
                    AgentVersion = AgentVersion,
                    PromptVersion = PromptVersion,
                    ModelName = provider.ModelName,
                    RetryOfRunId = retryOfRunId,
                    Status = AiRunStatus.Pending,
                    ExecutionLeaseId = Guid.NewGuid(),
                    LastHeartbeatAt = now,
                    DeadlineAt = now.AddSeconds(90),
                    TraceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")
                };
                await _runRepository.AddAsync(newRun, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return newRun;
            }, cancellationToken);
        await SendRunEventAsync(run, identity.UserId, "run.pending", cancellationToken);

        return await ExecuteRunAsync(run, conversation, routeCandidates, identity.UserId, cancellationToken);
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
        var run = await GetOwnedRunAsync(runId, identity.UserId, cancellationToken);
        if (run.Status is AiRunStatus.Completed or AiRunStatus.Failed or AiRunStatus.Cancelled)
        {
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
        return await SendMessageCoreAsync(
            failedRun.ConversationId,
            new SendAiMessageRequest { Content = requestMessage.Content },
            failedRun.Id,
            cancellationToken);
    }

    private async Task<AiRunResponse> ExecuteRunAsync(
        AiRun run,
        AiConversation conversation,
        IReadOnlyList<AiModelRouteCandidate> routeCandidates,
        Guid userId,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(MaxRunDuration);
        using var lease = _cancellationCoordinator.Begin(run.Id, timeoutSource.Token);
        var token = lease.Token;
        var stopwatch = Stopwatch.StartNew();
        var toolCallCount = 0;

        try
        {
            await ThrowIfCancellationRequestedAsync(run.Id, token);
            run.Status = AiRunStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            run.LastHeartbeatAt = run.StartedAt;
            _runRepository.Update(run);
            await _unitOfWork.SaveChangesAsync(token);
            await SendRunEventAsync(run, userId, "run.running", token);

            var tools = _toolRegistry.GetAvailableTools()
                .Concat(_actionToolRegistry.GetAvailableTools())
                .ToList();
            ValidateToolCatalog(tools);
            var modelTools = tools.Select(ToModelTool).ToList();
            var toolDefinitions = tools.ToDictionary(item => item.FunctionName, StringComparer.Ordinal);
            var modelMessages = await BuildModelMessagesAsync(conversation.Id, token);
            var totalInputTokens = 0;
            var totalOutputTokens = 0;
            var totalEstimatedCost = 0m;
            var allCompletedInvocationsPriced = true;
            var usageSequence = 0;
            var activeRouteIndex = 0;

            for (var round = 1; round <= MaxModelRounds; round++)
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
                        provider.MaxTokens ?? 4096,
                        token);

                    var modelStopwatch = Stopwatch.StartNew();
                    try
                    {
                        modelResponse = await _modelGateway.CompleteAsync(
                            ToConnectionSettings(provider),
                            new AiModelGatewayRequest
                            {
                                Messages = modelMessages,
                                Tools = modelTools,
                                Temperature = provider.Temperature,
                                MaxTokens = provider.MaxTokens
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
                    }
                    catch (AiModelGatewayException exception)
                    {
                        if (exception.IsTransient)
                        {
                            await _circuitBreaker.RecordFailureAsync(circuitTarget, exception.ErrorType, CancellationToken.None);
                        }
                        usage.Status = AiInvocationStatus.Failed;
                        usage.ErrorCode = exception.ErrorType;
                        if (!exception.IsTransient || routeIndex + 1 >= routeCandidates.Count)
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

                await ThrowIfCancellationRequestedAsync(run.Id, token);
                if (modelResponse.ToolCalls.Count > 0)
                {
                    run.LastHeartbeatAt = DateTimeOffset.UtcNow;
                    _runRepository.Update(run);
                    await _unitOfWork.SaveChangesAsync(token);
                    if (toolCallCount + modelResponse.ToolCalls.Count > MaxToolCalls)
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
                        var toolResult = await ExecuteToolAsync(run, userId, toolCall, definition, token);
                        modelMessages.Add(new AiModelGatewayMessage
                        {
                            Role = "tool",
                            ToolCallId = toolCall.Id,
                            Content = toolResult.ContentJson
                        });
                    }

                    continue;
                }

                var responseContent = toolCallCount == 0
                    ? "当前回答没有经过系统工具验证，无法提供数据结论或业务草稿。请补充要查询的范围或待生成单据的明确字段。"
                    : NormalizeModelResponse(modelResponse.Content);
                var responseMessage = await AddAssistantMessageAsync(
                    conversation,
                    responseContent,
                    modelResponse.OutputTokens,
                    token);
                run.ResponseMessageId = responseMessage.Id;
                run.InputTokens = totalInputTokens;
                run.OutputTokens = totalOutputTokens;
                run.EstimatedCost = allCompletedInvocationsPriced ? totalEstimatedCost : null;
                CompleteRun(run, AiRunStatus.Completed, null, null, stopwatch.ElapsedMilliseconds);
                _runRepository.Update(run);
                await _unitOfWork.SaveChangesAsync(token);
                await _circuitBreaker.RecordSuccessAsync(
                    new AiCircuitTarget("agent", $"{run.TenantId:N}:{run.AgentCode}"),
                    CancellationToken.None);
                await SendRunEventAsync(run, userId, "run.completed", token);
                return await ToRunResponseAsync(run, token);
            }

            throw new AiRunLimitException("model_round_limit_exceeded", "The AI run exceeded the model round limit.");
        }
        catch (OperationCanceledException)
        {
            var explicitlyCancelled = run.CancellationRequestedAt.HasValue ||
                await _cancellationProbe.IsCancellationRequestedAsync(run.Id, CancellationToken.None);
            CompleteRun(
                run,
                explicitlyCancelled ? AiRunStatus.Cancelled : AiRunStatus.Failed,
                explicitlyCancelled ? "run_cancelled" : "run_timeout",
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
        await SendRunEventAsync(run, userId, run.Status == AiRunStatus.Cancelled ? "run.cancelled" : "run.failed", CancellationToken.None);
        return await ToRunResponseAsync(run, CancellationToken.None);
    }

    private async Task<AiToolExecutionResult> ExecuteToolAsync(
        AiRun run,
        Guid userId,
        AiModelToolCall toolCall,
        AiToolDefinition definition,
        CancellationToken cancellationToken)
    {
        var invocation = new AiToolInvocation
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            InvocationId = toolCall.Id,
            ToolCode = definition.ToolCode,
            ToolVersion = definition.Version,
            Status = AiInvocationStatus.Running,
            InputDigest = ComputeDigest(toolCall.ArgumentsJson),
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
                    toolCall.ArgumentsJson,
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
                    toolCall.ArgumentsJson,
                    cancellationToken);
            }
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
            await AddToolMessageAsync(run.ConversationId, run.TenantId, result.ContentJson, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var history = await _queryExecutor.ToListAsync(
            _messageRepository.Query()
                .Where(entity =>
                    entity.ConversationId == conversationId &&
                    (entity.Role == AiMessageRole.User || entity.Role == AiMessageRole.Assistant))
                .OrderByDescending(entity => entity.Sequence)
                .Take(MaxHistoryMessages),
            cancellationToken);
        var messages = new List<AiModelGatewayMessage>
        {
            new()
            {
                Role = "system",
                Content = "You are the PermissionSystem platform assistant. Use only the supplied tools for system facts and business drafts. " +
                    "Never invent records, counts, permissions, identities, log details, or report values. " +
                    "For DemoBusinessOrder requests, call the draft tool and omit unknown fields instead of guessing. A draft never means a formal order was created. " +
                    "Never claim that a draft was confirmed, submitted, approved, or persisted as a formal business order. " +
                    "If tools do not provide sufficient evidence, state that the answer cannot be verified. " +
                    "Do not request or expose secrets, tokens, passwords, personal contact data, IP addresses, user agents, or raw request/response bodies. " +
                    "Answer in the user's language and keep factual conclusions traceable to tool results."
            }
        };
        messages.AddRange(history
            .OrderBy(entity => entity.Sequence)
            .Select(entity => new AiModelGatewayMessage
            {
                Role = entity.Role == AiMessageRole.User ? "user" : "assistant",
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

        return new AiRunResponse
        {
            Id = run.Id,
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
            ParametersJson = definition.InputSchemaJson
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
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (_modelRouteService is not null)
        {
            return await _modelRouteService.ResolveAsync(AgentCode, conversationId, cancellationToken);
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

        if (usage.InputTokens.HasValue &&
            usage.OutputTokens.HasValue &&
            usage.InputTokenPricePerMillion.HasValue &&
            usage.OutputTokenPricePerMillion.HasValue &&
            !string.IsNullOrWhiteSpace(usage.PricingCurrency))
        {
            usage.EstimatedCost = decimal.Round(
                usage.InputTokens.Value * usage.InputTokenPricePerMillion.Value / 1_000_000m +
                usage.OutputTokens.Value * usage.OutputTokenPricePerMillion.Value / 1_000_000m,
                6,
                MidpointRounding.AwayFromZero);
        }

        usage.ReservedCost = null;
        usage.ReservationExpiresAt = null;
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
        IReadOnlyList<AiDocumentDraftResponse> drafts)
    {
        return new AiConversationDetailResponse
        {
            Id = entity.Id,
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
            DocumentDrafts = drafts
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
