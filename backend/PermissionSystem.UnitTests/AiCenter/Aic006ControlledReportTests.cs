using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.Reports;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic006ControlledReportTests
{
    [Theory]
    [InlineData("{\"params\":{\"keyword\":\"a\",\"Keyword\":\"b\"}}")]
    [InlineData("{\"params\":{\"keyword\":\"a\",\"keyword\":\"b\"}}")]
    [InlineData("{\"mode\":\"Rows\",\"Mode\":\"Metrics\"}")]
    [InlineData("{\"tenantId\":\"ignored\"}")]
    [InlineData("{\"params\":null}")]
    [InlineData("{\"limit\":\"1\"}")]
    [InlineData("{\"mode\":null}")]
    public void ApiRequest_RejectsAmbiguousOrUnknownBindings(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReportQueryRequest>(json, AiStructuredResults.JsonOptions));

    [Theory]
    [InlineData("{\"startTime\":\"2026-10-01T00:00:00\"}")]
    [InlineData("{\"startTime\":\"2026-10-01\"}")]
    [InlineData("{\"startTime\":\"2026-10-01T00:00:00Z\",\"endTime\":\"2026-10-01T00:00:00Z\"}")]
    [InlineData("{\"isEnabled\":\"true\"}")]
    [InlineData("{\"departmentId\":\"unknown\"}")]
    [InlineData("{\"departmentScope\":\"All\"}")]
    [InlineData("{\"tenantId\":\"untrusted\"}")]
    [InlineData("{\"keyword\":{\"value\":\"a\"}}")]
    public void Filters_RejectInvalidTypesAndTimeRules(string json) =>
        Assert.Throws<BusinessException>(() => ReportDatasetCapabilities.NormalizeFilters(Parameters(json), []));

    [Fact]
    public void Filters_KeepSingleSidedTimeExplicitClearAndNormalizeOffset()
    {
        var filters = ReportDatasetCapabilities.NormalizeFilters(Parameters("""{"keyword":"  %_  ","isEnabled":null,"startTime":"2026-10-01T00:00:00+08:00"}"""), []);
        Assert.Equal("%_", filters.Keyword);
        Assert.Null(filters.IsEnabled); Assert.Null(filters.EndTime);
        Assert.Equal(DateTimeOffset.Parse("2026-09-30T16:00:00Z"), filters.StartTime);
        Assert.Equal(TimeSpan.Zero, filters.StartTime!.Value.Offset);
        var defaultFilter = new ReportQueryParamResponse { ParamCode = "isEnabled", ParamType = "Boolean", DefaultValue = "true" };
        Assert.True(ReportDatasetCapabilities.NormalizeFilters(Parameters("{}"), [defaultFilter]).IsEnabled);
        Assert.Null(ReportDatasetCapabilities.NormalizeFilters(Parameters("{\"isEnabled\":null}"), [defaultFilter]).IsEnabled);
        Assert.Throws<BusinessException>(() => ReportDatasetCapabilities.NormalizeFilters(Parameters("{}"),
            [new() { ParamCode = "isEnabled", ParamType = "Boolean", DefaultValue = "unexpected" }]));
    }

    [Theory]
    [InlineData("Rows", "DepartmentId", null)]
    [InlineData("Metrics", "Amount", null)]
    [InlineData("Metrics", "DepartmentId", "UserName")]
    [InlineData("Unknown", "None", null)]
    public void Query_RejectsUnsupportedDimensionsAndSorts(string mode, string dimension, string? sort) =>
        Assert.Throws<BusinessException>(() => ReportDatasetCapabilities.ValidateQuery(mode, dimension, sort, 1));

    [Theory]
    [InlineData("system-users", "reporting.SystemUsers", "UserDirectoryV1")]
    [InlineData("system-users-scoped", "reporting.SystemUsers", "UserDirectoryV1")]
    [InlineData("system-users-scoped", "reporting.AiSystemUsers", "AllOnly")]
    [InlineData("users", "reporting.Users", "Unknown")]
    public void Catalog_DoesNotInventScopeCapability(string key, string view, string capability) =>
        Assert.Throws<InvalidOperationException>(() => new ReportDatasetCatalog(Options.Create(new ReportOptions
        { Datasets = [new() { Key = key, Name = "Users", ViewName = view, Capability = capability }] })));

    [Theory]
    [InlineData(DataScopeType.CurrentUser, true, false)]
    [InlineData(DataScopeType.CurrentDepartment, false, true)]
    [InlineData(DataScopeType.CurrentDepartmentAndChildren, false, true)]
    [InlineData(DataScopeType.CustomDepartments, true, true)]
    [InlineData(DataScopeType.CustomDepartments, false, false)]
    [InlineData(DataScopeType.All, false, true)]
    public async Task GuardAndSql_ApplyResolvedUnionOrEmptyScope(DataScopeType type, bool includeUser, bool departments)
    {
        var fixture = new Fixture();
        fixture.Ai.Scopes.Scope = Scope(type, includeUser, departments ? [fixture.DepartmentId] : []);
        var context = await fixture.Access.AuthorizeAsync(fixture.Ai.ReportCatalog.GetRequired(ReportDatasetCapabilities.UserDatasetKey), false);
        using var command = new SqlCommand();
        ControlledUserReportSql.Configure(command, new() { Context = context, UserFilters = new() }, 200);
        Assert.Contains("source.[TenantId] = @__TenantId", command.CommandText);
        Assert.Equal(TestIds.TenantId, command.Parameters["__TenantId"].Value);
        if (type == DataScopeType.All) Assert.DoesNotContain("@__ActorId", command.CommandText);
        else
        {
            Assert.Equal(includeUser, command.CommandText.Contains("source.[Id] = @__ActorId"));
            Assert.Equal(departments, command.CommandText.Contains("OPENJSON"));
            if (!includeUser && !departments) Assert.Contains("1 = 0", command.CommandText);
            if (includeUser && departments) Assert.Contains(" OR ", command.CommandText);
            await Assert.ThrowsAsync<BusinessException>(() => fixture.Access.AuthorizeAsync(fixture.Ai.ReportCatalog.GetRequired("system-users"), false));
        }
    }

    [Fact]
    public void Sql_FiltersAreParameterizedAndCountsPrecedeLimits()
    {
        using var command = new SqlCommand();
        var departments = Enumerable.Range(1, 10000).Select(_ => Guid.NewGuid()).ToArray();
        ControlledUserReportSql.Configure(command, new()
        {
            Context = new() { TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId, Scope = Scope(DataScopeType.CustomDepartments, true, departments) },
            UserFilters = new() { Keyword = "%_~[", DepartmentId = Guid.NewGuid(), StartTime = DateTimeOffset.UtcNow.AddDays(-1), EndTime = DateTimeOffset.UtcNow },
            Mode = "Metrics", Dimension = "DepartmentId", Limit = 1
        }, 200);
        Assert.True(command.Parameters.Count < 10);
        Assert.Equal("%~%~_~~~[%", command.Parameters["__Keyword"].Value);
        Assert.Contains("[CreatedAt] < @__End", command.CommandText);
        Assert.Contains("GROUP BY [DepartmentId]", command.CommandText);
        Assert.Contains("COUNT_BIG(*)", command.CommandText);
        Assert.Contains("COUNT_BIG(*) FROM grouped", command.CommandText);
        Assert.Contains("[UserCount] DESC", command.CommandText);
        Assert.DoesNotContain("PhoneNumber", command.CommandText); Assert.DoesNotContain("SELECT *", command.CommandText);
        Assert.Equal(1, command.Parameters["__MaxRows"].Value);
        using var invalid = new SqlCommand();
        Assert.Throws<BusinessException>(() => ControlledUserReportSql.Configure(invalid, new()
        { Context = new() { TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId, Scope = Scope(DataScopeType.CustomDepartments, false, departments.Append(Guid.NewGuid()).ToArray()) }, UserFilters = new() }, 200));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("stale")]
    [InlineData("tenant")]
    [InlineData("revoked")]
    public async Task Query_RejectsInvalidCurrentIdentityBeforeExecution(string state)
    {
        var fixture = new Fixture();
        if (state == "inactive") fixture.Ai.Identities.Active = false;
        if (state == "stale") fixture.Ai.Current.DepartmentId = Guid.NewGuid();
        if (state == "tenant") fixture.Ai.Current.TenantId = Guid.NewGuid();
        if (state == "revoked") fixture.Ai.Identities.Actor = fixture.Ai.Identities.Actor with { PermissionCodes = [] };
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Query());
        Assert.Empty(fixture.Executor.Requests);
    }

    [Fact]
    public async Task QueryExportAndTool_ShareServerScopeAndSafeProjection()
    {
        var fixture = new Fixture();
        fixture.Ai.Scopes.Scope = Scope(DataScopeType.CurrentUser, true, []);
        fixture.Definition.ColumnsJson = """[{"key":"Email","title":"Email"},{"key":"TenantId","title":"Tenant"}]""";
        await fixture.Query();
        await fixture.Service.ExportAsync(fixture.Definition.Id, new());
        var output = await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Rows"));
        Assert.Equal(3, fixture.Executor.Requests.Count);
        Assert.All(fixture.Executor.Requests, request => Assert.Equal(TestIds.NormalUserId, request.Context!.ActorUserId));
        Assert.All(fixture.Executor.Requests, request => Assert.Equal(DataScopeType.CurrentUser, request.Context!.Scope.ScopeType));
        Assert.DoesNotContain("Email", output.ContentJson); Assert.DoesNotContain("TenantId", output.ContentJson);
        Assert.NotNull(output.StructuredResult!.Table);
        Assert.Null(output.Citation.AsOf);
        Assert.Equal(3, fixture.Logs.Items.Count);
    }

    [Fact]
    public async Task LegacyDataset_RejectsPartialScopeAndUnsafeAiProjection()
    {
        var fixture = new Fixture(); fixture.Definition.DatasetKey = "system-users";
        fixture.Ai.Scopes.Scope = Scope(DataScopeType.CurrentUser, true, []);
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Query());
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.ExportAsync(fixture.Definition.Id, new()));
        fixture.Ai.Scopes.Scope = Scope(DataScopeType.All, false, []);
        fixture.Ai.ReportConfiguration.ApprovedReportDatasetKeys = ["system-users"];
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Rows")));
        Assert.Empty(fixture.Executor.Requests);
    }

    [Fact]
    public async Task CrossTenantDefinition_IsNotQueriedOrAuditedForOtherTenant()
    {
        var fixture = new Fixture(); fixture.Definition.TenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Query());
        Assert.Empty(fixture.Logs.Items); Assert.Empty(fixture.Executor.Requests);
    }

    [Fact]
    public async Task Query_UnknownFiltersAndCancelAreAuditedAndDoNotSilentlyFallback()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.QueryAsync(fixture.Definition.Id,
            new() { Params = Parameters("{\"tenantId\":\"untrusted\"}") }));
        Assert.False(fixture.Logs.Items.Last().IsSuccess); Assert.Empty(fixture.Executor.Requests);
        Assert.DoesNotContain("untrusted", fixture.Logs.Items.Last().ParamsJson);
        fixture.Executor.Cancel = true;
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Query());
        Assert.False(fixture.Logs.Items.Last().IsSuccess);
        Assert.Contains("cancelled", fixture.Logs.Items.Last().FailureReason);
    }

    [Fact]
    public async Task CurrentDepartment_RequiresActiveDepartmentAndPreservesScopeIntersection()
    {
        var fixture = new Fixture();
        fixture.Ai.Current.DepartmentId = fixture.DepartmentId;
        fixture.Ai.Identities.Actor = fixture.Ai.Identities.Actor with { DepartmentId = fixture.DepartmentId };
        fixture.Ai.Scopes.Scope = new() { ScopeType = DataScopeType.CurrentUser, CurrentUserId = TestIds.NormalUserId,
            CurrentDepartmentId = fixture.DepartmentId };
        var request = new ReportQueryRequest { Params = Parameters("{\"departmentScope\":\"CurrentDepartment\"}") };
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.QueryAsync(fixture.Definition.Id, request));
        await fixture.Ai.Departments.AddAsync(new() { Id = fixture.DepartmentId, TenantId = TestIds.TenantId, IsEnabled = true });
        await fixture.Service.QueryAsync(fixture.Definition.Id, request);
        var execution = fixture.Executor.Requests.Single();
        using var command = new SqlCommand(); ControlledUserReportSql.Configure(command, execution, 200);
        Assert.Contains("source.[Id] = @__ActorId", command.CommandText);
        Assert.Contains("source.[DepartmentId] = @__CurrentDepartment", command.CommandText);
        fixture.Ai.Departments.Items.Single().IsEnabled = false;
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.QueryAsync(fixture.Definition.Id, request));
        Assert.Single(fixture.Executor.Requests);
    }

    [Theory]
    [InlineData("query")]
    [InlineData("export")]
    [InlineData("user")]
    public async Task Guard_RequiresCurrentPermissionsForEveryEntry(string missing)
    {
        var fixture = new Fixture();
        var code = missing == "export" ? "report:export" : missing == "user" ? "system:user:view" : "report:view";
        fixture.Ai.Identities.Actor = fixture.Ai.Identities.Actor with
        { PermissionCodes = AiQueryTestFixture.Permissions.Where(item => item != code).ToArray() };
        await Assert.ThrowsAsync<BusinessException>(() => missing == "export"
            ? fixture.Service.ExportAsync(fixture.Definition.Id, new()) : fixture.Query());
        Assert.Empty(fixture.Executor.Requests);
    }

    [Fact]
    public async Task ChangedScopeDuringExecution_DoesNotReturnOrRecordSuccess()
    {
        var fixture = new Fixture();
        fixture.Executor.AfterExecution = () => fixture.Ai.Scopes.Scope = Scope(DataScopeType.CurrentUser, true, []);
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Query());
        Assert.False(fixture.Logs.Items.Single().IsSuccess);
    }

    [Fact]
    public async Task History_ValidatesActualRowsAndReportContractEvenWithMatchingScope()
    {
        var fixture = new Fixture();
        await fixture.Ai.PersistAsync(await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Rows")));
        Assert.Single((await fixture.Ai.Reader.ReadAsync(fixture.Ai.Conversation.Id)).Results);
        fixture.Ai.Users.Items.Single().IsDeleted = true;
        Assert.Empty((await fixture.Ai.Reader.ReadAsync(fixture.Ai.Conversation.Id)).Results);
    }

    [Fact]
    public async Task BoundedMetrics_TrimGroupsWithoutChangingTotalsOrCitations()
    {
        var fixture = new Fixture();
        var result = (await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Metrics", "DepartmentId"))).StructuredResult!;
        result.Metrics!.Groups.Clear();
        result.Metrics.Groups.AddRange(Enumerable.Range(0, 5).Select(index => new ReportUserMetricGroup(new string('x', 20000) + index, new(1000, 800, 200))));
        var content = AiStructuredResults.SerializeBounded(result);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(content) <= AiStructuredResults.MaxEnvelopeBytes);
        Assert.True(result.IsTruncated); Assert.Equal(5000, result.Metrics.Totals.UserCount);
        Assert.Equal(result.Metrics.DisplayedGroupCount, result.Citation.RowCount);
        Assert.True(result.Metrics.DisplayedGroupCount < 5);
    }

    [Fact]
    public async Task FollowUp_ImplicitChangesRejectUnrelatedFiltersAndKeepExclusiveEnd()
    {
        var fixture = new Fixture();
        await fixture.Ai.PersistAsync(await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Metrics", "DepartmentId")));
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", "{\"params\":{\"keyword\":\"changed\"}}", fixture.Ai.Reference, expectedChange: AiFollowUpChange.CurrentDepartment));
        var sameDepartment = await fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", "{}", fixture.Ai.Reference, expectedChange: AiFollowUpChange.CurrentDepartment);
        using var departmentJson = JsonDocument.Parse(sameDepartment);
        Assert.Equal("CurrentDepartment", departmentJson.RootElement.GetProperty("params").GetProperty("departmentScope").GetString());
        var previous = await fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", "{}", fixture.Ai.Reference, 480, AiFollowUpChange.PreviousCalendarMonth);
        using var previousJson = JsonDocument.Parse(previous);
        var end = previousJson.RootElement.GetProperty("params").GetProperty("endTime").GetDateTimeOffset();
        Assert.Equal(1, end.Day); Assert.Equal(TimeSpan.Zero, end.TimeOfDay);
        var rows = await fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", "{\"mode\":\"Rows\"}", fixture.Ai.Reference);
        using var rowsJson = JsonDocument.Parse(rows);
        Assert.Equal("None", rowsJson.RootElement.GetProperty("dimension").GetString()); Assert.False(rowsJson.RootElement.TryGetProperty("sort", out _));
    }

    [Fact]
    public async Task FullMetrics_AreNotEstimatedFromLimitedGroupsAndHaveTraceableContract()
    {
        var fixture = new Fixture();
        var output = await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Metrics", "DepartmentId", 1));
        var result = output.StructuredResult!;
        Assert.Equal(5000, result.Metrics!.Totals.UserCount); Assert.Equal(4000, result.Metrics.Totals.EnabledUserCount);
        Assert.Single(result.Metrics.Groups); Assert.Equal(5, result.Metrics.TotalGroupCount);
        Assert.True(result.IsTruncated); Assert.Equal(1, output.RowCount);
        using var json = JsonDocument.Parse(output.ContentJson);
        Assert.Equal(0, json.RootElement.GetProperty("sourceRowCount").GetInt32());
        Assert.Equal(5000, json.RootElement.GetProperty("totalCount").GetInt64());
        Assert.All(result.Metrics.Definitions, item => Assert.Equal("人", item.Unit));
        Assert.Null(result.Citation.AsOf); Assert.Contains("非历史", result.Limitations.Single());
        await fixture.Ai.PersistAsync(output);
        Assert.Single((await fixture.Ai.Reader.ReadAsync(fixture.Ai.Conversation.Id)).Results);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("disabled")]
    [InlineData("deleted")]
    [InlineData("rebound")]
    [InlineData("unapproved")]
    [InlineData("configuration")]
    [InlineData("version")]
    [InlineData("identity")]
    public async Task History_RechecksRangeDatasetVersionAndCurrentAccess(string change)
    {
        var fixture = new Fixture();
        await fixture.Ai.PersistAsync(await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext, fixture.Arguments("Metrics")));
        if (change == "scope") fixture.Ai.Scopes.Scope = Scope(DataScopeType.CurrentUser, true, []);
        if (change == "disabled") fixture.Definition.IsEnabled = false;
        if (change == "deleted") fixture.Definition.IsDeleted = true;
        if (change == "rebound") fixture.Definition.DatasetKey = "system-users";
        if (change == "unapproved") fixture.Ai.ReportConfiguration.ApprovedReportDatasetKeys = [];
        if (change == "configuration") fixture.Ai.ReportConfiguration.EnableReportDatasetTool = false;
        if (change == "version") fixture.Definition.UpdatedAt = DateTimeOffset.UtcNow;
        if (change == "identity") fixture.Ai.Identities.Active = false;
        Assert.Empty((await fixture.Ai.Reader.ReadAsync(fixture.Ai.Conversation.Id)).Results);
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => fixture.Ai.FollowUp.ResolveAsync(fixture.Ai.Conversation.Id, fixture.Ai.Reference));
    }

    [Fact]
    public async Task FollowUp_MergesOnlyNestedChangesAndUsesExclusiveCalendarMonth()
    {
        var fixture = new Fixture();
        var output = await fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext,
            fixture.Arguments("Metrics", parameters: Parameters("""{"keyword":"actor","isEnabled":true,"startTime":"2026-01-01T00:00:00Z"}""")));
        await fixture.Ai.PersistAsync(output);
        var changed = await fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", """{"params":{"departmentScope":"CurrentDepartment","isEnabled":null}}""", fixture.Ai.Reference);
        using var document = JsonDocument.Parse(changed);
        var filters = document.RootElement.GetProperty("params");
        Assert.Equal("actor", filters.GetProperty("keyword").GetString()); Assert.Equal(JsonValueKind.Null, filters.GetProperty("isEnabled").ValueKind);
        Assert.Equal("CurrentDepartment", filters.GetProperty("departmentScope").GetString());
        Assert.Equal(fixture.Definition.Id, document.RootElement.GetProperty("reportDefinitionId").GetGuid());
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", $"{{\"reportDefinitionId\":\"{Guid.NewGuid()}\"}}", fixture.Ai.Reference));
        await Assert.ThrowsAsync<AiFollowUpClarificationException>(() => fixture.Ai.FollowUp.PrepareArgumentsAsync(fixture.Ai.Conversation.Id,
            "permission.reports.query_dataset", "{\"period\":\"PreviousCalendarMonth\"}", fixture.Ai.Reference));
        var merged = new JsonObject { ["params"] = new JsonObject { ["keyword"] = "actor" } };
        AiFollowUpContextService.NormalizeRelativePeriod("permission.reports.query_dataset", merged,
            JsonNode.Parse("{\"period\":\"PreviousCalendarMonth\",\"utcOffsetMinutes\":480}")!.AsObject(), DateTimeOffset.Parse("2026-03-05T00:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-02-01T00:00:00+08:00"), merged["params"]!["startTime"]!.GetValue<DateTimeOffset>());
        Assert.Equal(DateTimeOffset.Parse("2026-03-01T00:00:00+08:00"), merged["params"]!["endTime"]!.GetValue<DateTimeOffset>());
        Assert.Equal("actor", merged["params"]!["keyword"]!.GetValue<string>());
    }

    [Fact]
    public async Task Tool_RejectsDuplicateNestedArgumentsBeforeExecution()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<BusinessException>(() => fixture.Tool.ExecuteAsync(fixture.Ai.ToolContext,
            $"{{\"reportDefinitionId\":\"{fixture.Definition.Id}\",\"params\":{{\"keyword\":\"a\",\"Keyword\":\"b\"}}}}"));
        Assert.Empty(fixture.Executor.Requests);
    }

    private static Dictionary<string, JsonElement> Parameters(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    private static DataScopeContext Scope(DataScopeType type, bool includeUser, Guid[] departments) => new()
    { ScopeType = type, CurrentUserId = TestIds.NormalUserId, IncludeCurrentUser = includeUser, DepartmentIds = departments };

    private sealed class Fixture
    {
        public AiQueryTestFixture Ai { get; } = new();
        public Guid DepartmentId { get; } = Guid.NewGuid();
        public ReportDefinition Definition { get; } = new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId,
            DatasetKey = ReportDatasetCapabilities.UserDatasetKey, DataSourceType = "Sql", IsEnabled = true };
        public InMemoryRepository<ReportExecutionLog> Logs { get; } = new();
        public RecordingExecutor Executor { get; } = new();
        public ReportQueryAccessGuard Access { get; }
        public ReportService Service { get; }
        public ReportDatasetQueryAiToolHandler Tool { get; }
        public Fixture()
        {
            Ai.Reports.AddAsync(Definition).GetAwaiter().GetResult();
            Ai.Users.AddAsync(new() { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId }).GetAwaiter().GetResult();
            Access = new(Ai.Current, Ai.Tenant, Ai.Identities, Ai.Scopes);
            Service = new(Ai.Reports, new InMemoryRepository<ReportQueryParam>(), Logs, Executor, new TestExcelService(),
                Ai.Current, new TestUnitOfWork(), Ai.ReportCatalog, Ai.Queries, Access, Ai.Departments);
            Tool = new(Ai.Scopes, Service, Ai.Reports, Ai.ReportConfiguration, Ai.Guard, Ai.ReportCatalog);
        }
        public Task<ReportQueryResponse> Query() => Service.QueryAsync(Definition.Id, new());
        public string Arguments(string mode, string dimension = "None", int limit = 200, Dictionary<string, JsonElement>? parameters = null) =>
            JsonSerializer.Serialize(new { reportDefinitionId = Definition.Id, mode, dimension, limit, @params = parameters ?? Parameters("{}") });
    }

    private sealed class RecordingExecutor : IReportQueryExecutor
    {
        public List<ReportExecutionRequest> Requests { get; } = [];
        public bool Cancel { get; set; }
        public Action? AfterExecution { get; set; }
        public Task<ReportExecutionResult> ExecuteAsync(ReportExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Cancel) throw new OperationCanceledException();
            AfterExecution?.Invoke();
            if (request.Mode == "Metrics") return Task.FromResult(new ReportExecutionResult
            {
                TotalCount = 5000, Metrics = new() { Dimension = request.Dimension, Totals = new(5000, 4000, 1000),
                    TotalGroupCount = request.Dimension == "None" ? 0 : 5,
                    Groups = request.Dimension == "None" ? [] : [new(null, new(1000, 800, 200))] },
                IsTruncated = request.Dimension != "None"
            });
            return Task.FromResult(new ReportExecutionResult
            {
                TotalCount = 1, FieldNames = ReportDatasetCapabilities.UserColumns,
                Rows = [new Dictionary<string, object?> { ["Id"] = TestIds.NormalUserId, ["DepartmentId"] = null,
                    ["UserName"] = "actor", ["DisplayName"] = "Actor", ["IsEnabled"] = true, ["CreatedAt"] = DateTimeOffset.UtcNow,
                    ["Email"] = "must-not-project", ["TenantId"] = TestIds.TenantId }]
            });
        }
    }
}
