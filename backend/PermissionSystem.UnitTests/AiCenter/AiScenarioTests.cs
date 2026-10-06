using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.AiEvaluations;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiScenarioTests(AiScenarioEvaluationFixture fixture) : IClassFixture<AiScenarioEvaluationFixture>
{
    [Fact]
    public async Task FreezeCopyAndConcurrentDraft_ShouldPreserveOldSnapshotAndRequireToken()
    {
        var f = Create(); var scenario = await f.Service.SaveAsync(new());
        var frozen = await f.Service.FreezeAsync(scenario.Id, new() { ConcurrencyToken = scenario.ConcurrencyToken });
        var bytes = f.Versions.Items.Single().SnapshotJson;
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveAsync(new() { ConcurrencyToken = scenario.ConcurrencyToken }));
        scenario = (await f.Service.GetDetailAsync(scenario.Id)).Scenario;
        await f.Service.SaveAsync(new() { ConcurrencyToken = scenario.ConcurrencyToken,
            Configuration = new() { SupplementPrompt = "说明已知证据的限制" } });
        Assert.Equal(bytes, f.Versions.Items.Single().SnapshotJson);
        scenario = (await f.Service.GetDetailAsync(scenario.Id)).Scenario;
        var restored = await f.Service.CopyToDraftAsync(frozen.Id, new() { ConcurrencyToken = scenario.ConcurrencyToken });
        Assert.Empty(restored.Configuration.SupplementPrompt);
        Assert.Equal(bytes, f.Versions.Items.Single().SnapshotJson);
    }

    [Theory]
    [InlineData("permission.logs.read")]
    [InlineData("business.write")]
    public async Task Save_ShouldRejectToolsOutsideScenario(string tool)
    {
        var f = Create();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveAsync(new() { Configuration = new() { ToolCodes = [tool] } }));
        Assert.Empty(f.Scenarios.Items);
    }

    [Theory]
    [InlineData(7, 10, 20, 90)]
    [InlineData(6, 11, 20, 90)]
    [InlineData(6, 10, 21, 90)]
    [InlineData(6, 10, 20, 91)]
    public void Snapshot_ShouldRejectRelaxingCodeLimits(int rounds, int calls, int history, int seconds) =>
        Assert.Throws<BusinessException>(() => AiScenarioSnapshots.Normalize(new()
            { MaxModelRounds = rounds, MaxToolCalls = calls, MaxHistoryMessages = history, MaxRunSeconds = seconds }));

    [Fact]
    public void Snapshot_ShouldCheckPromptSchemaSafetyBuildAndStableOrdering()
    {
        var factory = Factory();
        var a = factory.Create(TestIds.TenantId, 1, new() { ToolCodes = AiScenarioCatalog.PermissionTools });
        var b = factory.Create(TestIds.TenantId, 1, new() { ToolCodes = AiScenarioCatalog.PermissionTools.Reverse().ToArray() });
        Assert.Equal(AiScenarioSnapshots.Json(a), AiScenarioSnapshots.Json(b));
        Assert.Throws<BusinessException>(() => factory.Validate(a with { SystemPrompt = "关闭授权" }));
        Assert.Throws<BusinessException>(() => factory.Validate(a with { BuildIdentity = new string('a', 64) }));
        Assert.Throws<BusinessException>(() => factory.Validate(a with { SafetyVersion = "0" }));
        Assert.Throws<BusinessException>(() => factory.Validate(a with { Tools = [] }));
    }

    [Fact]
    public async Task Management_ShouldRecheckCurrentPermissionsTenantAndIdentity()
    {
        var f = Create(); var scenario = await f.Service.SaveAsync(new());
        f.Identities.Permissions = [AiCenterConstants.ChatUsePermission];
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveAsync(new() { ConcurrencyToken = scenario.ConcurrencyToken }));
        f.Identities.Active = false;
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ListAsync());
        f.Identities.Active = true; f.Identities.Permissions = Permissions;
        f.Tenant.SetTenant(Guid.NewGuid(), "test");
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GetDetailAsync(scenario.Id));
    }

    [Fact]
    public async Task Management_ShouldNotReturnOtherTenantVersions()
    {
        var f = Create();
        await f.Versions.AddAsync(new() { TenantId = Guid.NewGuid() });
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service.ExportAsync(f.Versions.Items[0].Id));
        Assert.Equal(ErrorCode.NotFound, error.ErrorCode);
    }

    [Fact]
    public void Verifier_ShouldRecomputeChecksInsteadOfTrustingPassedFields()
    {
        var raw = JsonNode.Parse(fixture.OfflineJson)!;
        raw["results"]![0]!["steps"]![0]!["output"] = "UNREADABLE-CANARY";
        var verified = new AiScenarioEvaluationVerifier().Verify(raw.ToJsonString(), fixture.Snapshot, fixture.Hash);
        Assert.False(EvaluationGate.Automatic(verified).Passed);
        Assert.Contains(verified.Results[0].Steps[0].Checks, c => c.Code == "answer.forbidden-facts" && !c.Passed);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("snapshot")]
    [InlineData("prompt")]
    [InlineData("schema")]
    [InlineData("coverage")]
    [InlineData("parameters")]
    [InlineData("accounting")]
    public void Verifier_ShouldRejectWrongBindingsAndIncompleteAccounting(string fault)
    {
        var raw = JsonNode.Parse(fixture.OfflineJson)!;
        switch (fault)
        {
            case "build": raw["buildIdentity"] = new string('b', 64); break;
            case "snapshot": raw["snapshotHash"] = new string('b', 64); break;
            case "prompt": raw["results"]![0]!["steps"]![0]!["models"]![0]!["basePromptHash"] = "wrong"; break;
            case "schema": raw["results"]![0]!["steps"]![0]!["models"]![0]!["tools"]![0]!["inputSchemaHash"] = "wrong"; break;
            case "coverage": raw["manifest"]!.AsArray().RemoveAt(0); break;
            case "parameters": raw["temperature"] = 1; break;
            case "accounting": raw["callCount"] = 0; break;
        }
        Assert.Throws<BusinessException>(() => new AiScenarioEvaluationVerifier().Verify(raw.ToJsonString(), fixture.Snapshot, fixture.Hash));
    }

    [Fact]
    public void Verifier_ShouldRejectDuplicatePropertiesAndLegacyReport()
    {
        Assert.Throws<BusinessException>(() => new AiScenarioEvaluationVerifier().Verify("{\"version\":1,\"version\":1}", fixture.Snapshot, fixture.Hash));
        var raw = JsonNode.Parse(fixture.OfflineJson)!; raw["snapshotHash"] = null;
        Assert.Throws<BusinessException>(() => new AiScenarioEvaluationVerifier().Verify(raw.ToJsonString(), fixture.Snapshot, fixture.Hash));
    }

    [Fact]
    public async Task Publish_ShouldRequireHumanLiveAndExplicitInitialBaseline()
    {
        var f = Create(); var version = await Freeze(f);
        await DeniedPublish(f, version);
        var offline = await f.Service.ImportAsync(version.Id, new() { ReportJson = fixture.OfflineJson });
        await DeniedPublish(f, version);
        await Approve(f, offline); await DeniedPublish(f, version);
        var live = await f.Service.ImportAsync(version.Id, new() { ReportJson = fixture.SyntheticLiveJson });
        await DeniedPublish(f, version);
        await Approve(f, live);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PublishAsync(version.Id,
            new() { ConcurrencyToken = Token(f), Reason = "合成首版测试" }));
        await f.Service.PublishAsync(version.Id, new() { ConcurrencyToken = Token(f), Reason = "合成首版测试", ConfirmInitialBaseline = true });
        var scenario = f.Scenarios.Items.Single();
        Assert.Equal(version.Id, scenario.CurrentVersionId);
        Assert.Equal(version.Id, (await f.Service.ResolveCurrentAsync(scenario.Id)).Id);
        Assert.All(f.Events.Items.Where(e => e.Type == AiScenarioEventType.Reviewed), e => Assert.Equal(TestIds.NormalUserId, e.ActorUserId));
        Assert.NotNull(f.Events.Items.Single(e => e.Type == AiScenarioEventType.Published).QualificationJson);
    }

    [Fact]
    public async Task Republish_ShouldCompareWithEvidenceBoundToPreviousRelease()
    {
        var f = Create(); var version = await Published(f);
        var oldOffline = f.Evaluations.Items.Single(e => e.Mode == "Offline");
        var report = JsonNode.Parse(fixture.OfflineJson)!;
        report["startedAt"] = DateTimeOffset.UtcNow;
        var replacement = await f.Service.ImportAsync(version.Id, new() { ReportJson = report.ToJsonString() });
        await Approve(f, replacement);
        await f.Service.RevokeEvaluationAsync(oldOffline.Id, new() { ConcurrencyToken = Token(f), Reason = "撤销上一发布基线的合成证据" });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PublishAsync(version.Id,
            new() { ConcurrencyToken = Token(f), Reason = "同版本不得改用后来导入的基线", ConfirmInitialBaseline = true }));
        Assert.Single(f.Events.Items, e => e.Type == AiScenarioEventType.Published);
        Assert.Equal(version.Id, f.Scenarios.Items.Single().CurrentVersionId);
    }

    [Fact]
    public async Task StopOrRevoke_ShouldBlockPinnedVersionWithoutChangingItsSnapshot()
    {
        var f = Create(); var version = await Published(f); var bytes = f.Versions.Items.Single().SnapshotJson;
        var live = f.Evaluations.Items.Single(e => e.Mode == "Live");
        await f.Service.RevokeEvaluationAsync(live.Id, new() { ConcurrencyToken = Token(f), Reason = "撤销合成资格" });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ValidateAsync(version.Id));
        await f.Service.StopAsync(version.Id, new() { ConcurrencyToken = Token(f), Reason = "停用合成版本" });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ValidateAsync(version.Id));
        Assert.Null(f.Scenarios.Items.Single().CurrentVersionId);
        Assert.Equal(bytes, f.Versions.Items.Single().SnapshotJson);
    }

    [Fact]
    public async Task RouteChanges_ShouldNotGainQualificationByImportOrReviewAlone()
    {
        var f = Create(); var version = await Published(f);
        f.Provider.ModelName = "changed-model";
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ValidateAsync(version.Id));
        f.Provider.ModelName = "fixture-model";
        await f.Providers.AddAsync(new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ModelName = "unqualified-canary", IsEnabled = true,
            SupportsTools = true, ComplianceConfirmedAt = DateTimeOffset.UtcNow, BaseUrl = "https://evaluation.invalid", AllowedHostsJson = "[\"evaluation.invalid\"]" });
        await f.Routes.AddAsync(new() { TenantId = TestIds.TenantId, AgentCode = AiScenarioCatalog.PermissionAssistant,
            PrimaryProviderConfigId = f.Provider.Id, CanaryProviderConfigId = f.Providers.Items[1].Id, CanaryPercentage = 10 });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ValidateAsync(version.Id));
    }

    [Fact]
    public async Task Rollback_ShouldRequirePublishedCompatibleTargetAndPreserveHistory()
    {
        var f = Create(); var first = await Published(f);
        var scenario = (await f.Service.GetDetailAsync(f.Scenarios.Items[0].Id)).Scenario;
        var second = await f.Service.FreezeAsync(scenario.Id, new() { ConcurrencyToken = scenario.ConcurrencyToken });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PublishAsync(second.Id, new() { ConcurrencyToken = Token(f), Reason = "未发布目标" }, rollback: true));
        var offline = await f.Service.ImportAsync(second.Id, new() { ReportJson = fixture.OfflineJson }); await Approve(f, offline);
        var live = await f.Service.ImportAsync(second.Id, new() { ReportJson = fixture.SyntheticLiveJson }); await Approve(f, live);
        await f.Service.PublishAsync(second.Id, new() { ConcurrencyToken = Token(f), Reason = "合成后续发布" });
        Assert.Equal(second.Id, f.Scenarios.Items[0].CurrentVersionId);
        Assert.NotNull(await f.Service.ValidateAsync(first.Id));
        await f.Service.PublishAsync(first.Id, new() { ConcurrencyToken = Token(f), Reason = "合成回退" }, rollback: true);
        Assert.Equal(first.Id, f.Scenarios.Items[0].CurrentVersionId);
        Assert.Equal(2, f.Versions.Items.Count);
        Assert.Contains(f.Events.Items, e => e.Type == AiScenarioEventType.RolledBack && e.PreviousVersionId == second.Id);
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("route")]
    [InlineData("provider")]
    public async Task Runtime_ShouldObserveCommittedChangesDespiteTrackedGovernanceEntities(string changed)
    {
        var f = Create(); var version = await Published(f); var original = f.Scenarios.Items.Single();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new AppDbContext(options, f.Tenant, new Application.Abstractions.NullAuditContext()))
        {
            seed.AiScenarios.Add(new() { Id = original.Id, TenantId = original.TenantId, Code = original.Code,
                Name = original.Name, IsEnabled = true });
            await seed.SaveChangesAsync();
            seed.AiScenarioVersions.AddRange(f.Versions.Items);
            seed.AiScenarioEvaluations.AddRange(f.Evaluations.Items);
            seed.AiScenarioReleaseEvents.AddRange(f.Events.Items);
            seed.AiProviderConfigs.Add(f.Provider);
            seed.AiModelRoutePolicies.Add(new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId,
                AgentCode = original.Code, IsEnabled = true, PrimaryProviderConfigId = f.Provider.Id });
            await seed.SaveChangesAsync();
            seed.AiScenarios.Local.Single().CurrentVersionId = version.Id;
            await seed.SaveChangesAsync();
        }
        await using var reader = new AppDbContext(options, f.Tenant, new Application.Abstractions.NullAuditContext());
        var trackedScenario = await reader.AiScenarios.SingleAsync();
        var trackedRoute = await reader.AiModelRoutePolicies.SingleAsync();
        var trackedProvider = await reader.AiProviderConfigs.SingleAsync();
        var runtime = new AiScenarioService(new Repository<AiScenario>(reader), new Repository<AiScenarioDraft>(reader),
            new Repository<AiScenarioVersion>(reader), new Repository<AiScenarioEvaluation>(reader), new Repository<AiScenarioReleaseEvent>(reader),
            new Repository<AiModelRoutePolicy>(reader), new Repository<AiProviderConfig>(reader), new EfCoreAsyncQueryExecutor(),
            new TestUnitOfWork(), new TestCurrentUserService(permissions: Permissions), f.Tenant, f.Identities,
            new Configuration(), Factory(), new AiScenarioEvaluationVerifier());
        await runtime.ValidateAsync(version.Id);
        await using (var writer = new AppDbContext(options, f.Tenant, new Application.Abstractions.NullAuditContext()))
        {
            if (changed == "scenario") (await writer.AiScenarios.SingleAsync()).IsEnabled = false;
            else if (changed == "route") (await writer.AiModelRoutePolicies.SingleAsync()).FallbackProviderConfigId = Guid.NewGuid();
            else (await writer.AiProviderConfigs.SingleAsync()).IsEnabled = false;
            await writer.SaveChangesAsync();
        }
        Assert.True(trackedScenario.IsEnabled);
        Assert.True(trackedProvider.IsEnabled);
        Assert.Null(trackedRoute.FallbackProviderConfigId);
        await Assert.ThrowsAsync<BusinessException>(() => runtime.ValidateAsync(version.Id));
    }

    [Theory]
    [InlineData("version", false)]
    [InlineData("version", true)]
    [InlineData("evaluation", false)]
    [InlineData("evaluation", true)]
    [InlineData("event", false)]
    [InlineData("event", true)]
    public async Task Persistence_ShouldRejectUpdatingOrSoftDeletingImmutableRecords(string kind, bool delete)
    {
        var tenant = new TenantContext(); tenant.SetTenant(TestIds.TenantId, "test");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, new Application.Abstractions.NullAuditContext());
        Domain.Common.BaseEntity entity = kind switch { "version" => new AiScenarioVersion { ScenarioId = Guid.NewGuid() }, "evaluation" => new AiScenarioEvaluation { ScenarioId = Guid.NewGuid(), VersionId = Guid.NewGuid() }, _ => new AiScenarioReleaseEvent { ScenarioId = Guid.NewGuid(), VersionId = Guid.NewGuid() } };
        db.Add(entity); await db.SaveChangesAsync();
        if (delete) db.Remove(entity); else { entity.IsDeleted = true; db.Update(entity); }
        await Assert.ThrowsAsync<BusinessException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void SqlModel_ShouldUseTenantCompositeReferencesAndNullableLegacyColumns()
    {
        var tenant = new TenantContext(); tenant.SetTenant(TestIds.TenantId, "test");
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused.invalid;Database=model-only;Integrated Security=True").Options,
            tenant, new Application.Abstractions.NullAuditContext());
        foreach (var type in new[] { typeof(AiScenarioVersion), typeof(AiScenarioEvaluation), typeof(AiScenarioReleaseEvent) })
        {
            var model = db.Model.FindEntityType(type)!;
            Assert.True(model.FindProperty("RowVersion")!.IsConcurrencyToken);
            Assert.All(model.GetForeignKeys(), fk => Assert.Contains(fk.Properties, p => p.Name == "TenantId"));
        }
        Assert.True(db.Model.FindEntityType(typeof(AiRun))!.FindProperty("ScenarioVersionId")!.IsNullable);
        Assert.True(db.Model.FindEntityType(typeof(AiConversation))!.FindProperty("ScenarioVersionId")!.IsNullable);
    }

    private static readonly string[] Permissions = [AiCenterConstants.GovernanceManagePermission, AiCenterConstants.GovernanceViewPermission,
        AiCenterConstants.ChatUsePermission, AiCenterConstants.ToolQueryPermission];
    private AiScenarioSnapshotFactory Factory() => new(fixture.Snapshot.Tools.Select(t => new CatalogHandler(t)), new AiBuildIdentity());
    private State Create() => new(Factory());
    private async Task<AiScenarioVersionResponse> Freeze(State f)
    {
        var s = await f.Service.SaveAsync(new());
        var v = await f.Service.FreezeAsync(s.Id, new() { ConcurrencyToken = s.ConcurrencyToken });
        Assert.Equal(fixture.Hash, v.ContentHash); return v;
    }
    private async Task<AiScenarioVersionResponse> Published(State f)
    {
        var v = await Freeze(f);
        await Approve(f, await f.Service.ImportAsync(v.Id, new() { ReportJson = fixture.OfflineJson }));
        await Approve(f, await f.Service.ImportAsync(v.Id, new() { ReportJson = fixture.SyntheticLiveJson }));
        await f.Service.PublishAsync(v.Id, new() { ConcurrencyToken = Token(f), Reason = "合成发布状态机测试", ConfirmInitialBaseline = true });
        return v;
    }
    private static async Task DeniedPublish(State f, AiScenarioVersionResponse v)
    {
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PublishAsync(v.Id,
            new() { ConcurrencyToken = Token(f), Reason = "合成拒绝测试", ConfirmInitialBaseline = true }));
        Assert.Null(f.Scenarios.Items[0].CurrentVersionId);
    }
    private static byte[] Token(State f) => BitConverter.GetBytes(f.Scenarios.Items[0].Revision);
    private static async Task Approve(State f, AiScenarioEvaluationResponse e)
    {
        var report = await f.Service.GetEvaluationReportAsync(e.Id);
        await f.Service.ReviewAsync(e.Id, new() { ConcurrencyToken = Token(f), ReportHash = e.ReportHash, GoldenCasesApproved = true,
            Reason = "仅合成状态机测试，非业务黄金审核", Cases = report.Results.Where(r => r.Status != EvaluationStatus.NotApplicable)
                .Select(r => new CaseReview(r.Key, r.CaseHash, true, "合成观察符合固定预期，仅验证门禁逻辑")).ToList() });
    }
    private sealed class State
    {
        public InMemoryRepository<AiScenario> Scenarios { get; } = new();
        public InMemoryRepository<AiScenarioVersion> Versions { get; } = new();
        public InMemoryRepository<AiScenarioEvaluation> Evaluations { get; } = new();
        public InMemoryRepository<AiScenarioReleaseEvent> Events { get; } = new();
        public InMemoryRepository<AiModelRoutePolicy> Routes { get; } = new();
        public InMemoryRepository<AiProviderConfig> Providers { get; }
        public TenantContext Tenant { get; } = new();
        public IdentitySource Identities { get; } = new();
        public AiProviderConfig Provider { get; } = new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ModelName = "fixture-model",
            BaseUrl = "https://evaluation.invalid", AllowedHostsJson = "[\"evaluation.invalid\"]", IsDefault = true, IsEnabled = true,
            SupportsTools = true, ComplianceConfirmedAt = DateTimeOffset.UtcNow, PricingCurrency = "XXX" };
        public AiScenarioService Service { get; }
        public State(AiScenarioSnapshotFactory factory)
        {
            Tenant.SetTenant(TestIds.TenantId, "test"); Providers = new(Provider);
            Service = new(Scenarios, new InMemoryRepository<AiScenarioDraft>(), Versions, Evaluations, Events, Routes, Providers,
                new InMemoryAsyncQueryExecutor(), new TestUnitOfWork(), new TestCurrentUserService(permissions: Permissions), Tenant,
                Identities, new Configuration(), factory, new AiScenarioEvaluationVerifier());
        }
    }
    private sealed class Configuration : IAiCenterConfiguration
    {
        public bool Enabled => true; public IReadOnlyCollection<Guid> AllowedTenantIds => [TestIds.TenantId];
        public int ConversationRetentionDays => 30; public int AuditRetentionDays => 180;
    }
    private sealed class IdentitySource : IUserCredentialValidator
    {
        public bool Active { get; set; } = true;
        public string[] Permissions { get; set; } = AiScenarioTests.Permissions;
        public Task<AuthenticatedUser?> GetAuthenticationStateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthenticatedUser?>(Active ? new(userId, "synthetic-governance", tenantId, null, Guid.NewGuid(), [], Permissions) : null);
        public Task<Guid?> ResolveActiveTenantIdAsync(string tenantCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class CatalogHandler(AiScenarioToolSnapshot t) : IAiReadOnlyToolHandler
    {
        public AiToolDefinition Definition { get; } = new() { ToolCode = t.ToolCode, FunctionName = t.FunctionName, Version = t.Version,
            Description = t.Description, InputSchemaJson = t.InputSchemaJson, OutputSchemaJson = t.OutputSchemaJson,
            RequiredPermissions = t.RequiredPermissions, DataClassification = t.DataClassification, DataScopePolicy = t.DataScopePolicy,
            TimeoutSeconds = t.TimeoutSeconds, MaxRows = t.MaxRows };
        public bool IsEnabled => true;
        public Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, string argumentsJson, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public sealed class AiScenarioEvaluationFixture : IAsyncLifetime
{
    public AiScenarioSnapshot Snapshot { get; private set; } = null!;
    public string Hash { get; private set; } = "";
    public string OfflineJson { get; private set; } = "";
    public string SyntheticLiveJson { get; private set; } = "";
    public async Task InitializeAsync()
    {
        await using var environment = await IsolatedEvaluationEnvironment.CreateAsync(new(), new EvaluationBudget(new(1000, 100_000_000, 1000), 1, 1));
        Snapshot = environment.CandidateSnapshot(); Hash = AiScenarioSnapshots.Digest(AiScenarioSnapshots.Json(Snapshot));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "backend", "PermissionSystem.sln"))) root = root.Parent ?? throw new InvalidOperationException("Repository is unavailable.");
        var report = await EvaluationRunner.RunAsync(root.FullName, Path.Combine(root.FullName, "evaluations", "ai-center", "cases.json"), snapshot: Snapshot);
        Assert.True(report.AutomaticChecksPassed);
        OfflineJson = JsonSerializer.Serialize(report, EvaluationJson.Options);
        // A synthetic artifact is used only to test the privileged release state machine; it is never a live-model acceptance result.
        var live = JsonNode.Parse(OfflineJson)!; live["mode"] = "Live"; live["model"] = "fixture-model";
        var provider = new AiProviderConfig { BaseUrl = "https://evaluation.invalid", AllowedHostsJson = "[\"evaluation.invalid\"]", ModelName = "fixture-model" };
        live["modelFingerprint"] = AiScenarioSnapshots.ModelFingerprint(provider, Snapshot.Configuration);
        foreach (var result in live["results"]!.AsArray())
        {
            if (!result!["liveApplicable"]!.GetValue<bool>()) { result["status"] = "NotApplicable"; result["steps"] = new JsonArray(); continue; }
            foreach (var step in result["steps"]!.AsArray()) foreach (var model in step!["models"]!.AsArray())
            { model!["requestedModel"] = "fixture-model"; model["responseModel"] = "fixture-model"; }
        }
        var models = live["results"]!.AsArray().SelectMany(r => r!["steps"]!.AsArray()).SelectMany(s => s!["models"]!.AsArray()).ToArray();
        live["callCount"] = models.Length;
        live["accountedTokens"] = models.Sum(m => m!["reservedTokens"]!.GetValue<long>());
        live["accountedCost"] = models.Sum(m => m!["accountedCost"]!.GetValue<decimal>());
        SyntheticLiveJson = live.ToJsonString();
    }
    public Task DisposeAsync() => Task.CompletedTask;
}
