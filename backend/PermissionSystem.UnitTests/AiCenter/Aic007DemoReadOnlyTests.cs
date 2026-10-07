using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic007DemoReadOnlyTests
{
    [Fact]
    public void Registration_RequiresInternalHostOptInAndRemainsUnique()
    {
        var external = new ServiceCollection(); external.AddAiCenterCore();
        Assert.DoesNotContain(external, item => item.ImplementationType == typeof(DemoBusinessOrderQueryAiToolHandler));
        var internalHost = new ServiceCollection();
        internalHost.AddAiCenterCore(includeDemoBusinessOrderQueries: true);
        internalHost.AddAiCenterCore(includeDemoBusinessOrderQueries: true);
        Assert.Single(internalHost, item => item.ImplementationType == typeof(DemoBusinessOrderQueryAiToolHandler));
        Assert.False(new AiCenterOptions().EnableDemoBusinessOrderQueryTool);
        Assert.DoesNotContain(DemoBusinessOrderQueryAiToolHandler.ToolCode, AiScenarioCatalog.PermissionTools);
    }

    [Fact]
    public async Task Query_CountsCompleteMatchBeforeDisplayAndOnlyReturnsApprovedFields()
    {
        var f = new Fixture();
        for (var index = 0; index < 250; index++) await f.Add(index.ToString());
        await f.Add("other-tenant", tenant: Guid.NewGuid());
        (await f.Add("deleted")).IsDeleted = true;
        var result = await f.Query("""{"keyword":" order- ","limit":2}""");
        Assert.Equal(250, result.StructuredResult!.DemoOrders!.TotalCount);
        Assert.Equal(2, result.RowCount); Assert.True(result.IsTruncated); Assert.Null(result.Citation.AsOf);
        Assert.Equal("order-", result.StructuredResult.Context.Parameters["keyword"].GetString());
        using var json = JsonDocument.Parse(result.ContentJson);
        var keys = json.RootElement.GetProperty("items")[0].EnumerateObject().Select(item => item.Name).ToHashSet();
        Assert.True(keys.SetEquals(["id", "orderNo", "title", "approvalStatus", "departmentId", "createdAt"]));
        Assert.DoesNotContain("private-customer", result.ContentJson); Assert.DoesNotContain("private-owner", result.ContentJson);
        Assert.False(f.Ai.Current.HasPermission("demo-business-order:create"));
        Assert.Equal(0, f.Orders.UpdateCount); Assert.Equal(252, f.Orders.Items.Count);
    }

    [Theory]
    [InlineData(ApprovalStatus.Draft)]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    [InlineData(ApprovalStatus.Withdrawn)]
    [InlineData(ApprovalStatus.Cancelled)]
    public async Task Query_UsesCurrentStatusAndDoesNotSearchExcludedFields(ApprovalStatus status)
    {
        var f = new Fixture();
        foreach (var item in Enum.GetValues<ApprovalStatus>()) await f.Add(item.ToString(), status: item);
        var output = await f.Query(JsonSerializer.Serialize(new { approvalStatus = status.ToString() }));
        var row = Assert.Single(output.StructuredResult!.DemoOrders!.Items);
        Assert.Equal(status.ToString(), row.ApprovalStatus);
        Assert.Empty((await f.Query("""{"keyword":"private-customer"}""")).StructuredResult!.DemoOrders!.Items);
        Assert.Empty((await f.Query("""{"keyword":"private-owner"}""")).StructuredResult!.DemoOrders!.Items);
    }

    [Theory]
    [InlineData("all", 4)]
    [InlineData("self", 1)]
    [InlineData("department", 1)]
    [InlineData("children", 2)]
    [InlineData("custom", 1)]
    [InlineData("union", 3)]
    [InlineData("empty", 0)]
    public async Task Query_UsesOriginalCreatedByAndDepartmentScope(string type, int count)
    {
        var f = new Fixture(); var department = Guid.NewGuid(); var child = Guid.NewGuid();
        await f.Add("created-by-me", creator: TestIds.NormalUserId, owner: Guid.NewGuid());
        await f.Add("owner-me-is-not-created-by-me", creator: Guid.NewGuid(), owner: TestIds.NormalUserId);
        await f.Add("department", department: department);
        await f.Add("child", department: child);
        f.Ai.Scopes.Scope = new()
        {
            CurrentUserId = TestIds.NormalUserId,
            ScopeType = type == "all" ? DataScopeType.All : type == "self" ? DataScopeType.CurrentUser : DataScopeType.CustomDepartments,
            IncludeCurrentUser = type is "self" or "union",
            DepartmentIds = type switch { "department" => [department], "children" or "union" => [department, child], "custom" => [child], _ => [] }
        };
        var output = await f.Query(); Assert.Equal(count, output.StructuredResult!.DemoOrders!.TotalCount);
        Assert.Equal(count, output.RowCount);
    }

    [Fact]
    public async Task Query_StableOrderAndDepartmentIntersectionDoNotEnumerateDepartments()
    {
        var f = new Fixture(); var dept = Guid.NewGuid(); f.SetDepartment(dept);
        var first = await f.Add("a", department: dept); var second = await f.Add("b", department: dept);
        second.CreatedAt = first.CreatedAt;
        await f.Add("out-of-department", department: Guid.NewGuid());
        var output = await f.Query("""{"departmentScope":"CurrentDepartment"}""");
        Assert.Equal(2, output.RowCount);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), output.StructuredResult!.DemoOrders!.Items.Select(item => item.Id));
        f.Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId, CurrentDepartmentId = dept,
            ScopeType = DataScopeType.CustomDepartments, DepartmentIds = [dept] };
        Assert.Equal(0, (await f.Query(JsonSerializer.Serialize(new { departmentId = Guid.NewGuid() }))).RowCount);
        f.Ai.Departments.Items.Single().IsEnabled = false;
        await Assert.ThrowsAsync<BusinessException>(() => f.Query("""{"departmentScope":"CurrentDepartment"}"""));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("inactive")]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("revoked")]
    [InlineData("stale-department")]
    [InlineData("scope-identity")]
    [InlineData("ai-disabled")]
    public async Task Query_RejectsInvalidIdentityConfigurationOrScopeBeforeReadingOrders(string scenario)
    {
        var f = new Fixture();
        switch (scenario)
        {
            case "disabled": f.Ai.ReportConfiguration.EnableDemoBusinessOrderQueryTool = false; break;
            case "inactive": f.Ai.Identities.Active = false; break;
            case "tenant": f.Ai.Current.TenantId = Guid.NewGuid(); break;
            case "actor": f.Ai.Current.UserId = Guid.NewGuid(); break;
            case "revoked": f.Ai.Identities.Actor = f.Ai.Identities.Actor with { PermissionCodes = [] }; break;
            case "stale-department": f.Ai.Current.DepartmentId = Guid.NewGuid(); break;
            case "scope-identity": f.Ai.Scopes.Scope = new() { CurrentUserId = Guid.NewGuid(), ScopeType = DataScopeType.All }; break;
            case "ai-disabled": f.Ai.Configuration.Enabled = false; break;
        }
        await Assert.ThrowsAsync<BusinessException>(() => f.Query());
        Assert.Equal(0, f.OrderQueries.ExecutionCount);
    }

    [Theory]
    [InlineData("""{"tenantId":"fake"}""")]
    [InlineData("""{"actorUserId":"fake"}""")]
    [InlineData("""{"sql":"arbitrary"}""")]
    [InlineData("""{"keyword":"a","Keyword":"b"}""")]
    [InlineData("""{"approvalStatus":"Unknown"}""")]
    [InlineData("""{"approvalStatus":"1"}""")]
    [InlineData("""{"approvalStatus":"pending"}""")]
    [InlineData("""{"approvalStatus":1}""")]
    [InlineData("""{"departmentId":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData("""{"departmentScope":"All"}""")]
    [InlineData("""{"limit":0}""")]
    [InlineData("""{"limit":201}""")]
    [InlineData("""{"limit":"2"}""")]
    [InlineData("""{"startTime":"2026-10-01"}""")]
    [InlineData("""{"customerName":"x"}""")]
    public async Task Query_RejectsUnapprovedOrAmbiguousArguments(string arguments)
    {
        var f = new Fixture(); await Assert.ThrowsAsync<BusinessException>(() => f.Query(arguments));
        Assert.Equal(0, f.OrderQueries.ExecutionCount);
    }

    [Fact]
    public async Task Query_RechecksAuthorizationAfterReadAndPropagatesCancellation()
    {
        var f = new Fixture(); await f.Add("one");
        f.OrderQueries.AfterRead = () => f.Ai.Identities.Active = false;
        await Assert.ThrowsAsync<BusinessException>(() => f.Query());
        f.Ai.Identities.Active = true; f.OrderQueries.AfterRead = null;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Tool.ExecuteAsync(f.Ai.ToolContext, "{}", cancellation.Token));
        using var running = new CancellationTokenSource();
        await f.Tool.ExecuteAsync(f.Ai.ToolContext, "{}", running.Token);
        Assert.Equal(running.Token, f.OrderQueries.LastToken);
    }

    [Fact]
    public async Task PublicUseCase_RechecksBusinessViewPermissionWithoutAiOrCreatePermissions()
    {
        var f = new Fixture(); await f.Add("one");
        f.Ai.Identities.Actor = f.Ai.Identities.Actor with { PermissionCodes = [DemoBusinessOrderReadOnlyContract.ViewPermission] };
        Assert.Equal(1, (await f.Service.QueryAsync(new())).Data.TotalCount);
        await Assert.ThrowsAsync<BusinessException>(() => f.Query());
        f.Ai.Identities.Actor = f.Ai.Identities.Actor with { PermissionCodes = [] };
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.QueryAsync(new()));
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("changed-status")]
    [InlineData("changed-department")]
    [InlineData("changed-title")]
    [InlineData("changed-order-no")]
    [InlineData("disabled")]
    [InlineData("revoked")]
    [InlineData("scope")]
    [InlineData("expired")]
    [InlineData("tampered")]
    [InlineData("foreign-run")]
    public async Task History_HidesSnapshotsAfterRecordOrAccessChanges(string scenario)
    {
        var f = new Fixture(); var order = await f.Add("one"); var message = await f.Ai.PersistAsync(await f.Query());
        Assert.Single((await f.Reader.ReadAsync(f.Ai.Conversation.Id)).Results);
        switch (scenario)
        {
            case "deleted": order.IsDeleted = true; break;
            case "changed-status": order.ApprovalStatus = ApprovalStatus.Approved; break;
            case "changed-department": order.DepartmentId = Guid.NewGuid(); break;
            case "changed-title": order.Title = "changed"; break;
            case "changed-order-no": order.OrderNo = "changed"; break;
            case "disabled": f.Ai.ReportConfiguration.EnableDemoBusinessOrderQueryTool = false; break;
            case "revoked": f.Ai.Identities.Actor = f.Ai.Identities.Actor with { PermissionCodes = [] }; break;
            case "scope": f.Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId, ScopeType = DataScopeType.CurrentUser }; break;
            case "expired": message.CreatedAt = DateTimeOffset.UtcNow.AddDays(-31); break;
            case "tampered": message.Content = message.Content.Replace("order-one", "other-no"); break;
            case "foreign-run": f.Ai.Runs.Items.Single().ActorUserId = Guid.NewGuid(); break;
        }
        Assert.Empty((await f.Reader.ReadAsync(f.Ai.Conversation.Id)).Results);
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => f.FollowUp.ResolveAsync(f.Ai.Conversation.Id, f.Ai.Reference));
    }

    [Fact]
    public async Task FollowUp_InheritsOnlyActualConditionsAndSupportsDepartmentStatusAndClear()
    {
        var f = new Fixture(); var dept = Guid.NewGuid(); f.SetDepartment(dept);
        await f.Add("one", department: dept); await f.Add("other", status: ApprovalStatus.Approved);
        await f.Ai.PersistAsync(await f.Query("""{"keyword":"order-","limit":1}"""));
        var json = await f.FollowUp.PrepareArgumentsAsync(f.Ai.Conversation.Id, DemoBusinessOrderQueryAiToolHandler.ToolCode,
            "{}", f.Ai.Reference, expectedChange: AiFollowUpChange.CurrentDepartment);
        using var args = JsonDocument.Parse(json);
        Assert.Equal("order-", args.RootElement.GetProperty("keyword").GetString());
        Assert.Equal("CurrentDepartment", args.RootElement.GetProperty("departmentScope").GetString());
        Assert.Equal(1, (await f.Query(json)).RowCount);
        json = await f.FollowUp.PrepareArgumentsAsync(f.Ai.Conversation.Id, DemoBusinessOrderQueryAiToolHandler.ToolCode,
            """{"keyword":null,"approvalStatus":"Approved"}""", f.Ai.Reference);
        Assert.Equal("Approved", Assert.Single((await f.Query(json)).StructuredResult!.DemoOrders!.Items).ApprovalStatus);
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => f.FollowUp.PrepareArgumentsAsync(f.Ai.Conversation.Id,
            DemoBusinessOrderQueryAiToolHandler.ToolCode, """{"limit":2}""", f.Ai.Reference, expectedChange: AiFollowUpChange.CurrentDepartment));
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => f.FollowUp.PrepareArgumentsAsync(f.Ai.Conversation.Id,
            DemoBusinessOrderQueryAiToolHandler.ToolCode, "{}", f.Ai.Reference, expectedChange: AiFollowUpChange.PreviousCalendarMonth));
        var schema = AiFollowUpContextService.ExtendModelSchema(f.Tool.Definition);
        Assert.Contains("contextRef", schema); Assert.DoesNotContain("period", schema);
    }

    [Fact]
    public async Task BoundedEnvelope_TrimsRowsAndCitationWithoutChangingTotalOrExecutingText()
    {
        var f = new Fixture();
        for (var i = 0; i < 200; i++)
        {
            var row = await f.Add(i.ToString()); row.Title = string.Concat(Enumerable.Repeat("😀", 100));
        }
        var output = await f.Query("""{"limit":200}"""); var message = await f.Ai.PersistAsync(output);
        Assert.True(Encoding.UTF8.GetByteCount(message.Content) <= AiStructuredResults.MaxEnvelopeBytes);
        var result = Assert.Single((await f.Reader.ReadAsync(f.Ai.Conversation.Id)).Results);
        Assert.Equal(200, result.DemoOrders!.TotalCount);
        Assert.InRange(result.DemoOrders.Items.Count, 1, 199);
        Assert.Equal(result.DemoOrders.Items.Count, result.Citation.RowCount); Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task SuperAdmin_StillQueriesOnlyCurrentTenantAndHonorsConfiguredLimit()
    {
        var f = new Fixture(); f.Ai.Current.IsSuperAdmin = true;
        f.Ai.Identities.Actor = f.Ai.Identities.Actor with { Roles = [ClaimConstants.SuperAdminRoleCode] };
        await f.Add("visible"); await f.Add("foreign", tenant: Guid.NewGuid());
        Assert.Equal(1, (await f.Query()).StructuredResult!.DemoOrders!.TotalCount);
        f.Ai.ReportConfiguration.MaxToolRows = 1;
        await Assert.ThrowsAsync<BusinessException>(() => f.Query("""{"limit":2}"""));
        await Assert.ThrowsAsync<BusinessException>(() => f.Query(JsonSerializer.Serialize(new { keyword = new string('a', 101) })));
    }

    [Theory]
    [InlineData("null-items")]
    [InlineData("null-limitations")]
    [InlineData("unknown-version")]
    [InlineData("bad-parameters")]
    public async Task History_RejectsMalformedEnvelopesWithoutTrustingDigestAlone(string kind)
    {
        var f = new Fixture(); await f.Add("one"); var message = await f.Ai.PersistAsync(await f.Query());
        var json = JsonNode.Parse(message.Content)!;
        if (kind == "null-items") json["result"]!["demoOrders"]!["items"] = null;
        if (kind == "null-limitations") json["result"]!["limitations"] = null;
        if (kind == "unknown-version") json["result"]!["version"] = 99;
        if (kind == "bad-parameters") json["result"]!["context"]!["parameters"]!["limit"] = "invalid";
        message.Content = json.ToJsonString(AiStructuredResults.JsonOptions);
        message.ContentDigest = AiStructuredResults.Digest(message.Content);
        f.Ai.Invocations.Items.Single().OutputDigest = message.ContentDigest;
        var page = await f.Reader.ReadAsync(f.Ai.Conversation.Id);
        Assert.Empty(page.Results); Assert.True(page.HasUnavailableResults);
    }

    [Fact]
    public async Task Conversation_RunPersistsAuditReloadsAndRequeriesWithoutCreatingDrafts()
    {
        var f = new Fixture(); await f.Add("pending"); await f.Add("approved", status: ApprovalStatus.Approved);
        var gateway = new DemoGateway();
        var registry = new AiReadOnlyToolRegistry([f.Tool], f.Ai.Current, f.Ai.Tenant, new TraceContextAccessor());
        var service = new AiConversationService(f.Ai.Conversations, f.Ai.Messages, f.Ai.Runs,
            new InMemoryRepository<AiProviderConfig>(new AiProviderConfig { Id = Guid.NewGuid(), TenantId = TestIds.TenantId,
                ProviderCode = "synthetic", ProviderName = "Synthetic", BaseUrl = "https://api.example.test",
                ChatCompletionsPath = "v1/chat/completions", ApiKeyEncrypted = "protected:synthetic-fixture",
                ModelName = "synthetic", IsEnabled = true, IsDefault = true, ComplianceConfirmedAt = DateTimeOffset.UtcNow,
                AllowedHostsJson = "[\"api.example.test\"]" }),
            f.Ai.Invocations, new InMemoryRepository<AiUsageLog>(), f.Ai.Queries, f.Ai.Current, registry, gateway,
            new TestConfigValueProtector(), new CancellationProbe(), new AiRunCancellationCoordinator(),
            new NullAiRunRealtimeSender(), new TestUnitOfWork(), f.Ai.Configuration,
            structuredReader: f.Reader, followUp: f.FollowUp);
        var first = await service.SendMessageAsync(f.Ai.Conversation.Id, new() { Content = "有哪些 Demo 单据" });
        Assert.Equal(AiRunStatus.Completed, first.Status);
        var result = Assert.Single(first.StructuredResults);
        Assert.Equal(2, result.DemoOrders!.TotalCount); Assert.Empty(first.DocumentDrafts);
        var stored = Assert.Single(f.Ai.Messages.Items, item => item.Role == AiMessageRole.Tool &&
            item.Content.Contains("\"type\":\"structured-result\"", StringComparison.Ordinal));
        Assert.Equal(stored.ContentDigest, Assert.Single(f.Ai.Invocations.Items).OutputDigest);
        Assert.Single((await service.GetDetailAsync(f.Ai.Conversation.Id)).StructuredResults);
        gateway.NextArguments = """{"approvalStatus":"Approved"}""";
        var second = await service.SendMessageAsync(f.Ai.Conversation.Id,
            new() { Content = "只看已通过", ContextRef = new(result.RunId, result.InvocationId) });
        Assert.Equal(AiRunStatus.Completed, second.Status);
        Assert.Equal("Approved", Assert.Single(Assert.Single(second.StructuredResults).DemoOrders!.Items).ApprovalStatus);
        Assert.Equal(2, f.Orders.Items.Count); Assert.Equal(0, f.Orders.UpdateCount);
        Assert.All(gateway.Requests, request => Assert.All(request.Tools, tool => Assert.Equal("query_demo_business_orders", tool.Name)));
    }

    private sealed class CancellationProbe : IAiRunCancellationProbe
    {
        public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class DemoGateway : IAiModelGateway
    {
        public List<AiModelGatewayRequest> Requests { get; } = [];
        public string NextArguments { get; set; } = "{}";
        public Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Requests.Count % 2 == 1 ? new AiModelGatewayResponse
            { ToolCalls = [new() { Id = "demo-call-" + Requests.Count, Name = "query_demo_business_orders", ArgumentsJson = NextArguments }] } :
                new() { Content = "合成回答，以服务端单据结果为准。" });
        }
    }

    private sealed class Fixture
    {
        public AiQueryTestFixture Ai { get; } = new(DemoBusinessOrderReadOnlyContract.ViewPermission);
        public InMemoryRepository<DemoBusinessOrder> Orders { get; } = new();
        public ObservedQueries OrderQueries { get; } = new();
        public DemoBusinessOrderReadOnlyQueryService Service { get; }
        public DemoBusinessOrderQueryAiToolHandler Tool { get; }
        public AiStructuredResultReader Reader { get; }
        public AiFollowUpContextService FollowUp { get; }
        public Fixture()
        {
            Ai.ReportConfiguration.EnableDemoBusinessOrderQueryTool = true;
            Service = new(new DataPermissionRepository<DemoBusinessOrder>(Orders, Ai.Scopes,
                new DataPermissionFilter(), new DemoBusinessOrderDataPermissionSpecification()),
                Ai.Departments, Ai.Current, Ai.Tenant, Ai.Identities, Ai.Scopes, new DataPermissionFilter(),
                new DemoBusinessOrderDataPermissionSpecification(), OrderQueries);
            Tool = new(Service, Ai.Guard, Ai.ReportConfiguration);
            Reader = new(Ai.Messages, Ai.Runs, Ai.Conversations, Ai.Invocations, Ai.Users, Ai.Departments, Ai.Current, Ai.Tenant,
                Ai.Queries, Ai.Diagnostics, Ai.Guard, new DataPermissionFilter(), Ai.Configuration,
                toolConfiguration: Ai.ReportConfiguration, demoOrders: Service);
            FollowUp = new(Reader, Ai.Guard);
        }
        public Task<AiToolExecutionResult> Query(string json = "{}") => Tool.ExecuteAsync(Ai.ToolContext, json);
        public async Task<DemoBusinessOrder> Add(string suffix, Guid? department = null, Guid? creator = null,
            Guid? owner = null, Guid? tenant = null, ApprovalStatus status = ApprovalStatus.Pending)
        {
            var order = new DemoBusinessOrder { OrderNo = "order-" + suffix, Title = "Title " + suffix, ApprovalStatus = status,
                TenantId = tenant ?? TestIds.TenantId, DepartmentId = department, CreatedBy = creator ?? Guid.NewGuid(),
                OwnerUserId = owner ?? Guid.NewGuid(), CustomerName = "private-customer", OwnerUserName = "private-owner", Amount = 123 };
            await Orders.AddAsync(order); return order;
        }
        public void SetDepartment(Guid department)
        {
            Ai.Current.DepartmentId = department; Ai.Identities.Actor = Ai.Identities.Actor with { DepartmentId = department };
            Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId, CurrentDepartmentId = department, ScopeType = DataScopeType.All };
            Ai.Departments.AddAsync(new Department { Id = department, TenantId = TestIds.TenantId, IsEnabled = true }).GetAwaiter().GetResult();
        }
    }

    private sealed class ObservedQueries : IAsyncQueryExecutor
    {
        private readonly InMemoryAsyncQueryExecutor _inner = new();
        public Action? AfterRead { get; set; }
        public int ExecutionCount => _inner.ExecutionCount;
        public CancellationToken LastToken => _inner.LastCancellationToken;
        public async Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        { var rows = await _inner.ToListAsync(query, cancellationToken); AfterRead?.Invoke(); return rows; }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => _inner.LongCountAsync(query, cancellationToken);
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => _inner.AnyAsync(query, cancellationToken);
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => _inner.FirstOrDefaultAsync(query, cancellationToken);
    }
}
