using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.PrintTemplates;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiDocumentExecutionServiceTests
{
    [Fact]
    public async Task Confirm_BindsValidatedDraftAndForcesStepUpVerification()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.ConfirmAsync(fixture.Draft.Id, new CreateAiDocumentConfirmationRequest
        {
            DraftConcurrencyToken = fixture.Draft.RowVersion
        });

        var confirmation = Assert.Single(fixture.Confirmations.Items);
        Assert.Equal(fixture.Draft.Id, result.DraftId);
        Assert.Equal(fixture.Draft.DraftVersion, confirmation.DraftVersion);
        Assert.Equal(fixture.Draft.PayloadHash, confirmation.PayloadHash);
        Assert.Equal(AiDocumentConfirmationStatus.Confirmed, confirmation.Status);
        Assert.True(confirmation.ExpiresAt > confirmation.ConfirmedAt);
        Assert.Contains(
            fixture.Security.VerificationRequests,
            request => request == (AiCenterConstants.DocumentExecuteOperationCode, true));
    }

    [Fact]
    public async Task Execute_ConsumesConfirmationAndCreatesOrderAndOutbox()
    {
        var fixture = new Fixture();
        var confirmation = fixture.AddConfirmation();

        var result = await fixture.Service.ExecuteAsync(fixture.Draft.Id, fixture.CreateExecutionRequest(confirmation));

        var execution = Assert.Single(fixture.Executions.Items);
        Assert.Equal(AiDocumentExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(fixture.BusinessOrders.CreatedOrder.Id, result.BusinessEntityId);
        Assert.Equal(fixture.BusinessOrders.CreatedOrder.OrderNo, result.BusinessNo);
        Assert.Equal(AiDocumentDraftStatus.Executed, fixture.Draft.Status);
        Assert.Equal(AiDocumentConfirmationStatus.Consumed, confirmation.Status);
        Assert.Single(fixture.Outbox.Messages);
    }

    [Fact]
    public async Task Execute_WhenSucceededRequestIsRepeatedForSameDraftReturnsPreviousResult()
    {
        var fixture = new Fixture();
        var confirmation = fixture.AddConfirmation();
        var request = fixture.CreateExecutionRequest(confirmation);

        var first = await fixture.Service.ExecuteAsync(fixture.Draft.Id, request);
        var replay = await fixture.Service.ExecuteAsync(fixture.Draft.Id, request);

        Assert.Equal(first.ExecutionId, replay.ExecutionId);
        Assert.Equal(first.BusinessEntityId, replay.BusinessEntityId);
        Assert.Equal(1, fixture.BusinessOrders.CreateCount);
        Assert.Single(fixture.Executions.Items);
        Assert.Single(fixture.Outbox.Messages);
    }

    [Fact]
    public async Task Execute_WhenSucceededConfirmationIsReplayedForDifferentDraftRejectsRequest()
    {
        var fixture = new Fixture();
        var confirmation = fixture.AddConfirmation();
        var request = fixture.CreateExecutionRequest(confirmation);
        await fixture.Service.ExecuteAsync(fixture.Draft.Id, request);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.ExecuteAsync(Guid.NewGuid(), request));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(1, fixture.BusinessOrders.CreateCount);
        Assert.Single(fixture.Executions.Items);
        Assert.Single(fixture.Outbox.Messages);
    }

    [Fact]
    public async Task Execute_WhenDraftPayloadChangedRejectsBeforeBusinessCreation()
    {
        var fixture = new Fixture();
        var confirmation = fixture.AddConfirmation();
        fixture.Draft.PayloadHash = new string('B', 64);
        fixture.Validations.Items[0].PayloadHash = fixture.Draft.PayloadHash;

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.ExecuteAsync(fixture.Draft.Id, fixture.CreateExecutionRequest(confirmation)));

        Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
        Assert.Equal(0, fixture.BusinessOrders.CreateCount);
        Assert.Empty(fixture.Outbox.Messages);
    }

    [Fact]
    public async Task Execute_WhenPermissionWasRemovedIsRejected()
    {
        var fixture = new Fixture(includeExecutePermission: false);
        var confirmation = fixture.AddConfirmation();

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.ExecuteAsync(fixture.Draft.Id, fixture.CreateExecutionRequest(confirmation)));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, fixture.BusinessOrders.CreateCount);
    }

    private sealed class Fixture
    {
        public Fixture(bool includeExecutePermission = true)
        {
            Draft = new AiDocumentDraft
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                ConversationId = Guid.NewGuid(),
                RunId = Guid.NewGuid(),
                SourceInvocationId = "call-1",
                ActorUserId = TestIds.NormalUserId,
                BusinessType = DemoBusinessOrderConstants.BusinessType,
                HandlerVersion = AiBusinessActionConstants.DemoBusinessOrderHandlerVersion,
                Status = AiDocumentDraftStatus.ReadyForConfirmation,
                DraftVersion = 1,
                PayloadJson = """{"title":"Order","customerName":"Customer","amount":10}""",
                PayloadHash = new string('A', 64),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                RowVersion = [1, 2, 3]
            };
            Drafts = new InMemoryRepository<AiDocumentDraft>(Draft);
            Validations = new InMemoryRepository<AiDocumentDraftValidation>(new AiDocumentDraftValidation
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DraftId = Draft.Id,
                DraftVersion = Draft.DraftVersion,
                PayloadHash = Draft.PayloadHash,
                IsValid = true,
                ErrorsJson = "[]",
                ValidatedAt = DateTimeOffset.UtcNow
            });
            Confirmations = new InMemoryRepository<AiDocumentConfirmation>();
            Executions = new InMemoryRepository<AiDocumentExecution>();
            var permissions = new List<string>
            {
                AiCenterConstants.DocumentDraftPermission,
                "demo-business-order:create",
                "system:department:view"
            };
            if (includeExecutePermission)
            {
                permissions.Add(AiCenterConstants.DocumentExecutePermission);
            }

            Security = new TestSecurityPolicyService();
            BusinessOrders = new StubDemoBusinessOrderService();
            Outbox = new RecordingOutboxService();
            Recovery = new InMemoryRecoveryStore(Executions);
            var configuration = new TestConfiguration();
            Service = new AiDocumentExecutionService(
                Drafts,
                Validations,
                Confirmations,
                Executions,
                new InMemoryAsyncQueryExecutor(),
                new TestCurrentUserService(permissions: permissions),
                configuration,
                configuration,
                Security,
                BusinessOrders,
                Outbox,
                new TraceContextAccessor { TraceId = "trace-p3" },
                new TestUnitOfWork(),
                Recovery,
                NullLogger<AiDocumentExecutionService>.Instance);
        }

        public AiDocumentExecutionService Service { get; }
        public AiDocumentDraft Draft { get; }
        public InMemoryRepository<AiDocumentDraft> Drafts { get; }
        public InMemoryRepository<AiDocumentDraftValidation> Validations { get; }
        public InMemoryRepository<AiDocumentConfirmation> Confirmations { get; }
        public InMemoryRepository<AiDocumentExecution> Executions { get; }
        public TestSecurityPolicyService Security { get; }
        public StubDemoBusinessOrderService BusinessOrders { get; }
        public RecordingOutboxService Outbox { get; }
        public InMemoryRecoveryStore Recovery { get; }

        public AiDocumentConfirmation AddConfirmation()
        {
            var confirmation = new AiDocumentConfirmation
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                DraftId = Draft.Id,
                RunId = Draft.RunId,
                ActorUserId = TestIds.NormalUserId,
                DraftVersion = Draft.DraftVersion,
                ConfirmationVersion = 1,
                PayloadHash = Draft.PayloadHash,
                HandlerVersion = Draft.HandlerVersion,
                Status = AiDocumentConfirmationStatus.Confirmed,
                ConfirmedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                RowVersion = [4, 5, 6]
            };
            Confirmations.AddAsync(confirmation).GetAwaiter().GetResult();
            return confirmation;
        }

        public ExecuteAiDocumentDraftRequest CreateExecutionRequest(AiDocumentConfirmation confirmation) => new()
        {
            ConfirmationId = confirmation.Id,
            ConfirmationVersion = confirmation.ConfirmationVersion,
            ConfirmationConcurrencyToken = confirmation.RowVersion,
            DraftConcurrencyToken = Draft.RowVersion
        };
    }

    private sealed class TestConfiguration : IAiCenterConfiguration, IAiDraftConfiguration
    {
        public bool Enabled => true;
        public IReadOnlyCollection<Guid> AllowedTenantIds => [TestIds.TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
        public int DraftExpirationMinutes => 30;
        public int ConfirmationExpirationMinutes => 2;
        public int DraftRetentionDays => 30;
    }

    private sealed class InMemoryRecoveryStore : IAiDocumentExecutionRecoveryStore
    {
        private readonly InMemoryRepository<AiDocumentExecution> _executions;

        public InMemoryRecoveryStore(InMemoryRepository<AiDocumentExecution> executions)
        {
            _executions = executions;
        }

        public Task<AiDocumentExecution?> GetByBusinessIdempotencyKeyAsync(
            Guid tenantId,
            string businessIdempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_executions.Items.FirstOrDefault(entity =>
                entity.TenantId == tenantId && entity.BusinessIdempotencyKey == businessIdempotencyKey));
        }

        public Task RecordFailureAsync(
            AiDocumentExecutionFailureRecord record,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxService : IOutboxService
    {
        public List<object> Messages { get; } = [];

        public Task<string> EnqueueAsync(CreateOutboxMessageRequest request, CancellationToken cancellationToken = default)
        {
            Messages.Add(request);
            return Task.FromResult(request.MessageId ?? Guid.NewGuid().ToString("N"));
        }

        public Task<string> EnqueueAsync<TMessage>(
            string exchange,
            string routingKey,
            TMessage message,
            IReadOnlyDictionary<string, string>? headers = null,
            Guid? tenantId = null,
            string? messageId = null,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message!);
            return Task.FromResult(messageId ?? Guid.NewGuid().ToString("N"));
        }

        public Task<PagedResult<OutboxMessageResponse>> GetPagedAsync(
            OutboxMessageQueryRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OutboxMessageDetailResponse> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubDemoBusinessOrderService : IDemoBusinessOrderService
    {
        public int CreateCount { get; private set; }

        public DemoBusinessOrderResponse CreatedOrder { get; } = new()
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            OrderNo = "DBO-0001",
            Title = "Order",
            CustomerName = "Customer",
            Amount = 10,
            OwnerUserId = TestIds.NormalUserId,
            OwnerUserName = "tester",
            ApprovalStatus = ApprovalStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };

        public Task<DemoBusinessOrderResponse> CreateAsync(
            CreateDemoBusinessOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return Task.FromResult(CreatedOrder);
        }

        public Task<PagedResult<DemoBusinessOrderResponse>> GetPagedAsync(DemoBusinessOrderQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DemoBusinessOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DemoBusinessOrderResponse> UpdateAsync(Guid id, UpdateDemoBusinessOrderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DemoBusinessOrderResponse> SubmitAsync(Guid id, SubmitDemoBusinessOrderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DemoBusinessOrderResponse> WithdrawAsync(Guid id, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DemoBusinessOrderResponse> CancelAsync(Guid id, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportAsync(DemoBusinessOrderQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> CreateImportTemplateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ImportResult<DemoBusinessOrderImportRow>> ImportPreviewAsync(Stream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<FileResourceResponse>> GetAttachmentsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileResourceResponse> UploadAttachmentAsync(Guid id, Stream content, string originalName, string? contentType, long size, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PrintTemplateResponse>> GetPrintTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DemoBusinessOrderPrintResponse> RenderPrintAsync(Guid id, Guid templateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<OperationLogResponse>> GetOperationLogsAsync(Guid id, PaginationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DemoBusinessOrderChangeHistoryResponse>> GetChangeHistoriesAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task NotifyOwnerAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
