using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class DemoBusinessOrderDraftHandlerTests
{
    [Fact]
    public async Task PrepareDraft_NormalizesFieldsAndResolvesCurrentTenantDepartment()
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "SALES",
            Name = "Sales",
            IsEnabled = true
        };
        var fixture = new Fixture(departments: [department]);

        var result = await fixture.Handler.PrepareDraftAsync(
            fixture.CreateContext(),
            """{"title":"  August order  ","customerName":"  Contoso  ","amount":123.45,"departmentReference":"sales"}""");

        Assert.Equal(AiDocumentDraftStatus.ReadyForConfirmation, result.Draft.Status);
        Assert.Equal("August order", result.Draft.Payload.Title);
        Assert.Equal("Contoso", result.Draft.Payload.CustomerName);
        Assert.Equal(department.Id, result.Draft.Payload.DepartmentId);
        Assert.Equal("SALES", result.Draft.Payload.DepartmentCode);
        Assert.Equal(64, result.Draft.PayloadHash.Length);
        Assert.Empty(result.Draft.ValidationErrors);
        Assert.Single(fixture.Drafts.Items);
        Assert.True(Assert.Single(fixture.Validations.Items).IsValid);
    }

    [Fact]
    public async Task PrepareDraft_MissingRequiredFieldsPersistsIncompleteDraftForClarification()
    {
        var fixture = new Fixture();

        var result = await fixture.Handler.PrepareDraftAsync(fixture.CreateContext(), "{}");

        Assert.Equal(AiDocumentDraftStatus.Incomplete, result.Draft.Status);
        Assert.Contains(result.Draft.ValidationErrors, error => error.Field == "Title" && error.Code == "required");
        Assert.Contains(result.Draft.ValidationErrors, error => error.Field == "CustomerName" && error.Code == "required");
        Assert.Contains(result.Draft.ValidationErrors, error => error.Field == "Amount" && error.Code == "required");
        Assert.Contains("has not created a formal business order", result.ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepareDraft_AmbiguousDepartmentDoesNotSelectCandidate()
    {
        var fixture = new Fixture(departments:
        [
            CreateDepartment("A", "Shared"),
            CreateDepartment("B", "Shared")
        ]);

        var result = await fixture.Handler.PrepareDraftAsync(
            fixture.CreateContext(),
            """{"title":"Order","customerName":"Customer","amount":10,"departmentReference":"Shared"}""");

        Assert.Equal(AiDocumentDraftStatus.Invalid, result.Draft.Status);
        Assert.Null(result.Draft.Payload.DepartmentId);
        var error = Assert.Single(result.Draft.ValidationErrors);
        Assert.Equal("ambiguous", error.Code);
        Assert.Equal(2, error.Candidates.Count);
    }

    [Fact]
    public async Task PrepareDraft_WithoutDepartmentPermissionDoesNotResolveAssociation()
    {
        var fixture = new Fixture(
            departments: [CreateDepartment("SALES", "Sales")],
            includeDepartmentPermission: false);

        var result = await fixture.Handler.PrepareDraftAsync(
            fixture.CreateContext(),
            """{"title":"Order","customerName":"Customer","amount":10,"departmentReference":"SALES"}""");

        Assert.Equal(AiDocumentDraftStatus.Invalid, result.Draft.Status);
        Assert.Null(result.Draft.Payload.DepartmentId);
        Assert.Contains(result.Draft.ValidationErrors, error => error.Code == "forbidden");
    }

    [Fact]
    public async Task UpdateDraft_RevalidatesAndInvalidatesPreviousPayloadHash()
    {
        var fixture = new Fixture();
        var created = await fixture.Handler.PrepareDraftAsync(
            fixture.CreateContext(),
            """{"title":"Order","customerName":"Customer","amount":10}""");
        var draftEntity = Assert.Single(fixture.Drafts.Items);
        draftEntity.RowVersion = [1, 2, 3];

        var updated = await fixture.Handler.UpdateAsync(created.Draft.Id, new UpdateAiDocumentDraftRequest
        {
            Title = "Updated order",
            CustomerName = "Customer",
            Amount = 20,
            ConcurrencyToken = [1, 2, 3]
        });

        Assert.Equal(2, updated.DraftVersion);
        Assert.NotEqual(created.Draft.PayloadHash, updated.PayloadHash);
        Assert.Equal(AiDocumentDraftStatus.ReadyForConfirmation, updated.Status);
        Assert.Equal(2, fixture.Validations.Items.Count);
        Assert.Equal(new[] { 1, 2 }, fixture.Validations.Items.Select(item => item.DraftVersion).Order().ToArray());
    }

    [Fact]
    public async Task UpdateDraft_RequiresConcurrencyToken()
    {
        var fixture = new Fixture();
        var created = await fixture.Handler.PrepareDraftAsync(
            fixture.CreateContext(),
            """{"title":"Order","customerName":"Customer","amount":10}""");

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Handler.UpdateAsync(created.Draft.Id, new UpdateAiDocumentDraftRequest
            {
                Title = "Updated order",
                CustomerName = "Customer",
                Amount = 20
            }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
        Assert.Single(fixture.Validations.Items);
    }

    [Fact]
    public async Task GetById_CrossTenantDraftIsNotVisible()
    {
        var fixture = new Fixture();
        var foreignDraft = new AiDocumentDraft
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ActorUserId = TestIds.NormalUserId,
            BusinessType = "DemoBusinessOrder",
            HandlerVersion = "1.0",
            PayloadJson = "{}",
            PayloadHash = new string('0', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        await fixture.Drafts.AddAsync(foreignDraft);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Handler.GetByIdAsync(foreignDraft.Id));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task GetById_ExecutedDraftIncludesPersistedBusinessResult()
    {
        var fixture = new Fixture();
        var created = await fixture.Handler.PrepareDraftAsync(
            fixture.CreateContext(),
            """{"title":"Order","customerName":"Customer","amount":10}""");
        var draft = Assert.Single(fixture.Drafts.Items);
        draft.Status = AiDocumentDraftStatus.Executed;
        var orderId = Guid.NewGuid();
        await fixture.Executions.AddAsync(new AiDocumentExecution
        {
            TenantId = TestIds.TenantId,
            DraftId = draft.Id,
            RunId = draft.RunId,
            ActorUserId = TestIds.NormalUserId,
            ConfirmationId = Guid.NewGuid(),
            ConfirmationVersion = 1,
            BusinessType = DemoBusinessOrderConstants.BusinessType,
            BusinessIdempotencyKey = "test",
            Status = AiDocumentExecutionStatus.Succeeded,
            BusinessEntityId = orderId,
            BusinessNo = "DBO-0001",
            BusinessStatus = ApprovalStatus.Draft.ToString(),
            TraceId = "trace-p3",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });

        var result = await fixture.Handler.GetByIdAsync(created.Draft.Id);

        Assert.NotNull(result.Execution);
        Assert.Equal(orderId, result.Execution.BusinessEntityId);
        Assert.Equal("DBO-0001", result.Execution.BusinessNo);
    }

    private static Department CreateDepartment(string code, string name)
    {
        return new Department
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = code,
            Name = name,
            IsEnabled = true
        };
    }

    private sealed class Fixture
    {
        public Fixture(Department[]? departments = null, bool includeDepartmentPermission = true)
        {
            Drafts = new InMemoryRepository<AiDocumentDraft>();
            Validations = new InMemoryRepository<AiDocumentDraftValidation>();
            Executions = new InMemoryRepository<AiDocumentExecution>();
            var configuration = new TestConfiguration();
            var permissions = new List<string>
            {
                AiCenterConstants.DocumentDraftPermission,
                "demo-business-order:create"
            };
            if (includeDepartmentPermission)
            {
                permissions.Add("system:department:view");
            }

            Handler = new DemoBusinessOrderDraftHandler(
                Drafts,
                Validations,
                Executions,
                new InMemoryRepository<Department>(departments ?? []),
                new InMemoryAsyncQueryExecutor(),
                new TestCurrentUserService(permissions: permissions),
                new TestUnitOfWork(),
                configuration,
                configuration);
        }

        public DemoBusinessOrderDraftHandler Handler { get; }

        public InMemoryRepository<AiDocumentDraft> Drafts { get; }

        public InMemoryRepository<AiDocumentDraftValidation> Validations { get; }

        public InMemoryRepository<AiDocumentExecution> Executions { get; }

        public AiActionDraftContext CreateContext()
        {
            return new AiActionDraftContext
            {
                TenantId = TestIds.TenantId,
                ActorUserId = TestIds.NormalUserId,
                ConversationId = Guid.NewGuid(),
                RunId = Guid.NewGuid(),
                InvocationId = $"call-{Guid.NewGuid():N}"
            };
        }
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
}
