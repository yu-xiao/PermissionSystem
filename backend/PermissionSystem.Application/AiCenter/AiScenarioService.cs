using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using PermissionSystem.AiEvaluations;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiScenarioService(
    IRepository<AiScenario> scenarios, IRepository<AiScenarioDraft> drafts, IRepository<AiScenarioVersion> versions,
    IRepository<AiScenarioEvaluation> evaluations, IRepository<AiScenarioReleaseEvent> events,
    IRepository<AiModelRoutePolicy> routes, IRepository<AiProviderConfig> providers,
    IAsyncQueryExecutor queries, IUnitOfWork unit, ICurrentUserService current, ITenantContext tenant,
    IUserCredentialValidator identities, IAiCenterConfiguration configuration,
    AiScenarioSnapshotFactory snapshots, AiScenarioEvaluationVerifier verifier) : IAiScenarioService
{
    public async Task<IReadOnlyList<AiScenarioResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceViewPermission, cancellationToken);
        var items = await queries.ToListAsync(scenarios.Query().Where(s => s.TenantId == tenant.TenantId).OrderBy(s => s.Code), cancellationToken);
        var result = new List<AiScenarioResponse>();
        foreach (var item in items) result.Add(await ResponseAsync(item, cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<AiScenarioOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.ChatUsePermission, cancellationToken);
        var items = await queries.ToListAsync(RuntimeScenarioQuery().Where(s => s.TenantId == tenant.TenantId && s.IsEnabled && s.CurrentVersionId.HasValue), cancellationToken);
        var result = new List<AiScenarioOption>();
        foreach (var item in items)
        {
            try
            {
                var version = await ResolveCurrentAsync(item.Id, cancellationToken);
                result.Add(new(item.Id, item.Code, item.Name, version.Id, version.VersionNumber));
            }
            catch (BusinessException e) when (e.ErrorCode is ErrorCode.Conflict or ErrorCode.ValidationFailed) { }
        }
        return result;
    }

    public async Task<AiScenarioDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceViewPermission, cancellationToken);
        var scenario = await ScenarioAsync(id, cancellationToken);
        var versionList = await queries.ToListAsync(versions.Query().Where(v => v.TenantId == scenario.TenantId && v.ScenarioId == id)
            .OrderByDescending(v => v.VersionNumber), cancellationToken);
        var eventList = await EventListAsync(id, cancellationToken);
        var evaluationList = await queries.ToListAsync(evaluations.Query().Where(e => e.TenantId == scenario.TenantId && e.ScenarioId == id)
            .OrderByDescending(e => e.CreatedAt), cancellationToken);
        return new(await ResponseAsync(scenario, cancellationToken), versionList.Select(v => VersionResponse(v, eventList)).ToArray(),
            evaluationList.Select(e => EvaluationResponse(e, eventList)).ToArray(), eventList.Select(e => new AiScenarioEventResponse(
                e.Id, e.VersionId, e.PreviousVersionId, e.EvaluationId, e.Type, e.ActorUserId, e.Reason, e.CreatedAt)).ToArray());
    }

    public async Task<AiScenarioResponse> SaveAsync(SaveAiScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var identity = await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken);
        if (request.Code != AiScenarioCatalog.PermissionAssistant || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100 ||
            request.Description is null || request.Description.Length > 1000) Invalid("Invalid scenario metadata.");
        var normalized = AiScenarioSnapshots.Normalize(request.Configuration);
        AiScenario? scenario = null;
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            scenario = await queries.FirstOrDefaultAsync(scenarios.Query().Where(s => s.TenantId == identity.TenantId && s.Code == request.Code), ct);
            if (scenario is null)
            {
                if (request.ConcurrencyToken is { Length: > 0 }) Conflict("Scenario no longer exists.");
                scenario = new() { Id = Guid.NewGuid(), TenantId = identity.TenantId, Code = request.Code };
                await scenarios.AddAsync(scenario, ct);
            }
            else { Match(scenario, request.ConcurrencyToken); scenarios.Update(scenario); }
            scenario.Name = request.Name.Trim(); scenario.Description = request.Description.Trim(); scenario.Revision++;
            var draft = await queries.FirstOrDefaultAsync(drafts.Query().Where(d => d.TenantId == scenario.TenantId && d.ScenarioId == scenario.Id), ct);
            if (draft is null)
            {
                draft = new() { ScenarioId = scenario.Id, TenantId = scenario.TenantId };
                await drafts.AddAsync(draft, ct);
            }
            else drafts.Update(draft);
            draft.ConfigurationJson = AiScenarioSnapshots.Json(normalized); draft.Revision++;
            await unit.SaveChangesAsync(ct);
        }, cancellationToken);
        return await ResponseAsync(scenario!, cancellationToken);
    }

    public async Task<AiScenarioVersionResponse> FreezeAsync(Guid id, AiScenarioChangeRequest request, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken);
        AiScenarioVersion? version = null;
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var scenario = await ScenarioAsync(id, ct); Match(scenario, request.ConcurrencyToken);
            var draft = await DraftAsync(scenario, ct);
            var last = await queries.FirstOrDefaultAsync(versions.Query().Where(v => v.TenantId == scenario.TenantId && v.ScenarioId == id)
                .OrderByDescending(v => v.VersionNumber), ct);
            var snapshot = snapshots.Create(scenario.TenantId, draft.Revision, AiScenarioSnapshots.Read<AiScenarioConfiguration>(draft.ConfigurationJson));
            var json = AiScenarioSnapshots.Json(snapshot);
            version = new() { TenantId = scenario.TenantId, ScenarioId = id, VersionNumber = (last?.VersionNumber ?? 0) + 1,
                SnapshotJson = json, ContentHash = AiScenarioSnapshots.Digest(json), BuildIdentity = snapshot.BuildIdentity };
            await versions.AddAsync(version, ct); Touch(scenario); await unit.SaveChangesAsync(ct);
        }, cancellationToken);
        return VersionResponse(version!, []);
    }

    public async Task<AiScenarioResponse> CopyToDraftAsync(Guid versionId, AiScenarioChangeRequest request, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken);
        AiScenario? scenario = null;
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var version = await VersionAsync(versionId, ct); scenario = await ScenarioAsync(version.ScenarioId, ct);
            Match(scenario, request.ConcurrencyToken);
            var draft = await DraftAsync(scenario, ct);
            draft.ConfigurationJson = AiScenarioSnapshots.Json(ReadSnapshot(version).Configuration); draft.Revision++;
            drafts.Update(draft); Touch(scenario); await unit.SaveChangesAsync(ct);
        }, cancellationToken);
        return await ResponseAsync(scenario!, cancellationToken);
    }

    public async Task<AiScenarioSnapshot> ExportAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceViewPermission, cancellationToken);
        var snapshot = ReadSnapshot(await VersionAsync(versionId, cancellationToken)); snapshots.Validate(snapshot);
        return snapshot;
    }

    public async Task<AiScenarioEvaluationResponse> ImportAsync(Guid versionId, ImportAiScenarioEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken);
        var version = await VersionAsync(versionId, cancellationToken); var snapshot = ReadSnapshot(version); snapshots.Validate(snapshot);
        var report = verifier.Verify(request.ReportJson, snapshot, version.ContentHash);
        var existing = await queries.FirstOrDefaultAsync(evaluations.Query().Where(e => e.TenantId == version.TenantId && e.VersionId == versionId &&
            e.ReportHash == EvaluationJson.Digest(request.ReportJson)), cancellationToken);
        if (existing is not null) return EvaluationResponse(existing, await EventListAsync(version.ScenarioId, cancellationToken));
        var evidence = new AiScenarioEvaluation
        {
            TenantId = version.TenantId, ScenarioId = version.ScenarioId, VersionId = versionId, ReportHash = EvaluationJson.Digest(request.ReportJson),
            ReportJson = JsonSerializer.Serialize(report, EvaluationJson.Options), Mode = report.Mode.ToString(),
            ModelFingerprint = report.ModelFingerprint ?? "offline", AutomaticPassed = EvaluationGate.Automatic(report).Passed
        };
        await evaluations.AddAsync(evidence, cancellationToken); await unit.SaveChangesAsync(cancellationToken);
        return EvaluationResponse(evidence, []);
    }

    public async Task<EvaluationReport> GetEvaluationReportAsync(Guid evaluationId, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceViewPermission, cancellationToken);
        var evaluation = await EvaluationAsync(evaluationId, cancellationToken);
        return Report(evaluation);
    }

    public async Task ReviewAsync(Guid evaluationId, ReviewAiScenarioEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var identity = await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken);
        var reason = Reason(request.Reason);
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var evaluation = await EvaluationAsync(evaluationId, ct); var scenario = await ScenarioAsync(evaluation.ScenarioId, ct);
            Match(scenario, request.ConcurrencyToken);
            if (request.ReportHash != evaluation.ReportHash || request.Cases is null || request.Cases.Count > 100 ||
                request.Cases.Any(c => c is null || c.Notes is null || c.Notes.Length is < 1 or > 2000)) Invalid("Review must reference the exact report and bounded case rationale.");
            var version = await VersionAsync(evaluation.VersionId, ct);
            var report = verifier.Verify(evaluation.ReportJson, ReadSnapshot(version), version.ContentHash);
            var review = new ReviewDocument { Reviewer = identity.UserId.ToString("N"), ReviewedAt = DateTimeOffset.UtcNow,
                ReportHash = evaluation.ReportHash, SuiteHash = report.SuiteHash, GoldenCasesApproved = request.GoldenCasesApproved, Cases = request.Cases };
            // Both approving and rejecting reviews remain immutable and attributable to the authenticated reviewer.
            await AppendAsync(scenario, evaluation.VersionId, AiScenarioEventType.Reviewed, reason, ct,
                evaluationId: evaluationId, reviewJson: JsonSerializer.Serialize(review, EvaluationJson.Options));
            await unit.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task PublishAsync(Guid versionId, AiScenarioChangeRequest request, bool rollback = false, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken); var reason = Reason(request.Reason);
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var version = await VersionAsync(versionId, ct); var scenario = await ScenarioAsync(version.ScenarioId, ct);
            Match(scenario, request.ConcurrencyToken); var snapshot = ReadSnapshot(version); snapshots.Validate(snapshot);
            var history = await EventListAsync(scenario.Id, ct);
            var state = Lifecycle(history, versionId);
            if (state?.Type == AiScenarioEventType.Stopped) Conflict("Stopped versions cannot be reactivated. Copy to a new draft.");
            if (rollback && state is null) Conflict("Rollback requires a previously published version.");
            var previousId = scenario.CurrentVersionId;
            var eligibleModels = await RouteProvidersAsync(scenario.Code, ct);
            var evidence = await EvaluationListAsync(version, ct);
            var baselineId = previousId ?? (state is null ? null : (Guid?)versionId);
            var baselineVersion = baselineId.HasValue ? await VersionAsync(baselineId.Value, ct) : null;
            var baselineRelease = baselineId.HasValue ? Lifecycle(history, baselineId.Value) : null;
            var baselineIds = baselineRelease?.QualificationJson is null ? [] : AiScenarioSnapshots.Read<Guid[]>(baselineRelease.QualificationJson);
            var baselineEvidence = baselineVersion is null ? [] : (await EvaluationListAsync(baselineVersion, ct))
                .Where(e => baselineIds.Contains(e.Id)).ToArray();
            var qualifications = new List<Guid>();
            foreach (var mode in new[] { EvaluationMode.Offline, EvaluationMode.Live })
            {
                var fingerprints = mode == EvaluationMode.Offline ? new[] { "offline" } : eligibleModels
                    .Select(p => AiScenarioSnapshots.ModelFingerprint(p, snapshot.Configuration)).Distinct().ToArray();
                foreach (var fingerprint in fingerprints)
                {
                    var candidate = FindApproved(evidence, history, mode, fingerprint);
                    if (candidate is null) Conflict("Reviewed offline and live evidence for every model route is required.");
                    qualifications.Add(candidate.Id);
                    if (baselineVersion is null)
                    {
                        if (state is null && !request.ConfirmInitialBaseline) Conflict("Explicit initial baseline confirmation is required.");
                    }
                    else if (!rollback)
                    {
                        var baseline = FindApproved(baselineEvidence, history, mode, fingerprint);
                        if (baseline is null) Conflict("A comparable reviewed baseline for this model is required.");
                        var comparison = EvaluationGate.Compare(Report(baseline!), Report(candidate!), candidate!.ReportHash,
                            Review(history, candidate), Review(history, baseline!), baseline!.ReportHash);
                        if (!comparison.Comparable || !comparison.NoRegression || !comparison.CandidateGate.Passed) Conflict("Evaluation is incomparable or has new regressions.");
                    }
                }
            }
            await AppendAsync(scenario, versionId, rollback ? AiScenarioEventType.RolledBack : AiScenarioEventType.Published,
                reason, ct, previousVersionId: previousId, qualificationJson: AiScenarioSnapshots.Json(qualifications));
            scenario.CurrentVersionId = versionId; scenario.IsEnabled = true; await unit.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task StopAsync(Guid versionId, AiScenarioChangeRequest request, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken); var reason = Reason(request.Reason);
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var version = await VersionAsync(versionId, ct); var scenario = await ScenarioAsync(version.ScenarioId, ct);
            Match(scenario, request.ConcurrencyToken);
            if (Lifecycle(await EventListAsync(scenario.Id, ct), versionId) is null) Conflict("Only a published version can be stopped.");
            await AppendAsync(scenario, versionId, AiScenarioEventType.Stopped, reason, ct);
            if (scenario.CurrentVersionId == versionId) { scenario.CurrentVersionId = null; scenario.IsEnabled = false; }
            await unit.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task RevokeEvaluationAsync(Guid evaluationId, AiScenarioChangeRequest request, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.GovernanceManagePermission, cancellationToken); var reason = Reason(request.Reason);
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var evaluation = await EvaluationAsync(evaluationId, ct); var scenario = await ScenarioAsync(evaluation.ScenarioId, ct);
            Match(scenario, request.ConcurrencyToken);
            await AppendAsync(scenario, evaluation.VersionId, AiScenarioEventType.EvaluationRevoked, reason, ct, evaluationId: evaluationId);
            await unit.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task<AiScenarioVersion> ResolveCurrentAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.ChatUsePermission, cancellationToken);
        var scenario = await RuntimeScenarioAsync(scenarioId, cancellationToken);
        if (!scenario.CurrentVersionId.HasValue) Conflict("Scenario has no current published version.");
        var version = await VersionAsync(scenario.CurrentVersionId!.Value, cancellationToken);
        await ValidateAsync(version.Id, cancellationToken);
        return version;
    }

    public async Task<AiScenarioSnapshot> ValidateAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        await AccessAsync(AiCenterConstants.ChatUsePermission, cancellationToken);
        var version = await VersionAsync(versionId, cancellationToken); var scenario = await RuntimeScenarioAsync(version.ScenarioId, cancellationToken);
        var history = await EventListAsync(scenario.Id, cancellationToken);
        if (!scenario.IsEnabled || Lifecycle(history, versionId)?.Type is not (AiScenarioEventType.Published or AiScenarioEventType.RolledBack))
            Conflict("AI version is not available. Create a new conversation after selecting a published version.");
        var snapshot = ReadSnapshot(version); snapshots.Validate(snapshot);
        var lifecycle = Lifecycle(history, versionId)!;
        var qualificationIds = lifecycle.QualificationJson is null ? [] : AiScenarioSnapshots.Read<Guid[]>(lifecycle.QualificationJson);
        var evidence = (await EvaluationListAsync(version, cancellationToken)).Where(e => qualificationIds.Contains(e.Id)).ToArray();
        if (FindApproved(evidence, history, EvaluationMode.Offline, "offline") is null) Conflict("Offline qualification is missing or revoked.");
        foreach (var provider in await RouteProvidersAsync(scenario.Code, cancellationToken))
            if (FindApproved(evidence, history, EvaluationMode.Live, AiScenarioSnapshots.ModelFingerprint(provider, snapshot.Configuration)) is null)
                Conflict("A configured model has no current reviewed qualification for this AI version.");
        return snapshot;
    }

    public async Task ValidateToolAsync(Guid versionId, string toolCode, CancellationToken cancellationToken = default)
    {
        var snapshot = await ValidateAsync(versionId, cancellationToken);
        var tool = snapshot.Tools.SingleOrDefault(t => t.ToolCode == toolCode);
        var actor = await AccessAsync(AiCenterConstants.ChatUsePermission, cancellationToken);
        if (tool is null || !tool.RequiredPermissions.All(p => current.HasPermission(p) &&
            PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(actor.Roles), actor.PermissionCodes, p)))
            throw new BusinessException(ErrorCode.Forbidden, "The AI scenario tool is no longer authorized.");
    }

    public async Task ValidateModelAsync(Guid versionId, AiProviderConfig provider, CancellationToken cancellationToken = default)
    {
        var snapshot = await ValidateAsync(versionId, cancellationToken);
        var actual = await queries.FirstOrDefaultAsync(ProviderQuery().Where(p => p.Id == provider.Id && p.TenantId == tenant.TenantId), cancellationToken);
        if (actual is null || !Eligible(actual) || AiScenarioSnapshots.ModelFingerprint(actual, snapshot.Configuration) !=
            AiScenarioSnapshots.ModelFingerprint(provider, snapshot.Configuration)) Conflict("Selected provider changed during this run.");
        var version = await VersionAsync(versionId, cancellationToken);
        if (FindApproved(await EvaluationListAsync(version, cancellationToken), await EventListAsync(version.ScenarioId, cancellationToken),
                EvaluationMode.Live, AiScenarioSnapshots.ModelFingerprint(actual!, snapshot.Configuration)) is null) Conflict("Selected model is not qualified.");
    }

    private async Task<IReadOnlyList<AiProviderConfig>> RouteProvidersAsync(string code, CancellationToken ct)
    {
        var route = await queries.FirstOrDefaultAsync(routes.Query().Where(r => r.TenantId == tenant.TenantId && r.AgentCode == code && r.IsEnabled)
            .Select(r => new AiModelRoutePolicy { PrimaryProviderConfigId = r.PrimaryProviderConfigId,
                CanaryProviderConfigId = r.CanaryProviderConfigId, CanaryPercentage = r.CanaryPercentage,
                FallbackProviderConfigId = r.FallbackProviderConfigId }), ct);
        if (route is null)
        {
            var provider = await queries.FirstOrDefaultAsync(ProviderQuery().Where(p => p.TenantId == tenant.TenantId && p.IsDefault && p.IsEnabled), ct);
            if (provider is null || !Eligible(provider)) Conflict("No eligible default provider.");
            return [provider!];
        }
        var ids = new List<Guid>();
        if (!route.CanaryProviderConfigId.HasValue || route.CanaryPercentage < 100) ids.Add(route.PrimaryProviderConfigId);
        if (route.CanaryProviderConfigId.HasValue && route.CanaryPercentage > 0) ids.Add(route.CanaryProviderConfigId.Value);
        if (route.FallbackProviderConfigId.HasValue) ids.Add(route.FallbackProviderConfigId.Value);
        var result = await queries.ToListAsync(ProviderQuery().Where(p => p.TenantId == tenant.TenantId && ids.Contains(p.Id)), ct);
        if (result.Count != ids.Distinct().Count() || result.Any(p => !Eligible(p))) Conflict("All configured route models must be eligible and qualified.");
        if (result.Any(p => !string.Equals(p.DataResidency?.Trim(), result[0].DataResidency?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(p.PricingCurrency?.Trim(), result[0].PricingCurrency?.Trim(), StringComparison.OrdinalIgnoreCase)))
            Conflict("Route models must retain matching residency and pricing currency.");
        return result;
    }

    // Scalar projections observe committed state even when this request already tracks the governance entities.
    private IQueryable<AiScenario> RuntimeScenarioQuery() => scenarios.Query().Select(s => new AiScenario
    {
        Id = s.Id, TenantId = s.TenantId, Code = s.Code, IsEnabled = s.IsEnabled, CurrentVersionId = s.CurrentVersionId
    });
    private async Task<AiScenario> RuntimeScenarioAsync(Guid id, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(RuntimeScenarioQuery().Where(s => s.Id == id && s.TenantId == tenant.TenantId), ct)
        ?? throw new BusinessException(ErrorCode.NotFound, "AI scenario is unavailable.");

    private IQueryable<AiProviderConfig> ProviderQuery() => providers.Query().Select(p => new AiProviderConfig
    {
        Id = p.Id, TenantId = p.TenantId, ProviderType = p.ProviderType, BaseUrl = p.BaseUrl,
        ChatCompletionsPath = p.ChatCompletionsPath, ModelName = p.ModelName, IsDefault = p.IsDefault,
        TimeoutSeconds = p.TimeoutSeconds, AllowInsecureHttp = p.AllowInsecureHttp, AllowPrivateNetwork = p.AllowPrivateNetwork,
        AllowedHostsJson = p.AllowedHostsJson, IsEnabled = p.IsEnabled, SupportsTools = p.SupportsTools,
        ComplianceConfirmedAt = p.ComplianceConfirmedAt, DataResidency = p.DataResidency, PricingCurrency = p.PricingCurrency
    });

    private static bool Eligible(AiProviderConfig p) => p.IsEnabled && p.ComplianceConfirmedAt.HasValue && p.SupportsTools;
    private async Task<AuthenticatedUser> AccessAsync(string permission, CancellationToken ct)
    {
        if (!current.IsAuthenticated || !current.UserId.HasValue || !current.TenantId.HasValue) throw new BusinessException(ErrorCode.Unauthorized, "Authentication is required.");
        if (tenant.TenantId != current.TenantId || tenant.IsSystemScopeActive || !configuration.Enabled || !configuration.AllowedTenantIds.Contains(current.TenantId.Value))
            throw new BusinessException(ErrorCode.Forbidden, "AI scenario requires an enabled, explicit current tenant.");
        var identity = await identities.GetAuthenticationStateAsync(current.TenantId.Value, current.UserId.Value, ct);
        if (identity is null || identity.TenantId != tenant.TenantId || identity.UserId != current.UserId) throw new BusinessException(ErrorCode.Unauthorized, "Identity is inactive.");
        if (!current.HasPermission(permission) || !PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(identity.Roles), identity.PermissionCodes, permission))
            throw new BusinessException(ErrorCode.Forbidden, "AI scenario operation is not authorized.");
        return identity;
    }
    private async Task<AiScenario> ScenarioAsync(Guid id, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(scenarios.Query().Where(s => s.Id == id && s.TenantId == tenant.TenantId), ct)
        ?? throw new BusinessException(ErrorCode.NotFound, "AI scenario is unavailable.");
    private async Task<AiScenarioVersion> VersionAsync(Guid id, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(versions.Query().Where(v => v.Id == id && v.TenantId == tenant.TenantId), ct)
        ?? throw new BusinessException(ErrorCode.NotFound, "AI version is unavailable.");
    private async Task<AiScenarioEvaluation> EvaluationAsync(Guid id, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(evaluations.Query().Where(e => e.Id == id && e.TenantId == tenant.TenantId), ct)
        ?? throw new BusinessException(ErrorCode.NotFound, "AI evaluation is unavailable.");
    private async Task<AiScenarioDraft> DraftAsync(AiScenario s, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(drafts.Query().Where(d => d.TenantId == s.TenantId && d.ScenarioId == s.Id), ct)
        ?? throw new BusinessException(ErrorCode.NotFound, "AI draft is unavailable.");
    private Task<IReadOnlyList<AiScenarioReleaseEvent>> EventListAsync(Guid id, CancellationToken ct) =>
        queries.ToListAsync(events.Query().Where(e => e.TenantId == tenant.TenantId && e.ScenarioId == id).OrderBy(e => e.Sequence), ct);
    private Task<IReadOnlyList<AiScenarioEvaluation>> EvaluationListAsync(AiScenarioVersion v, CancellationToken ct) =>
        queries.ToListAsync(evaluations.Query().Where(e => e.TenantId == v.TenantId && e.VersionId == v.Id).OrderByDescending(e => e.CreatedAt), ct);
    private static EvaluationReport Report(AiScenarioEvaluation e) => JsonSerializer.Deserialize<EvaluationReport>(e.ReportJson, EvaluationJson.Options)!;
    private static ReviewDocument? Review(IReadOnlyList<AiScenarioReleaseEvent> history, AiScenarioEvaluation e) =>
        history.Where(h => h.EvaluationId == e.Id && h.Type == AiScenarioEventType.Reviewed).OrderByDescending(h => h.Sequence).FirstOrDefault()?.ReviewJson is string json
            ? JsonSerializer.Deserialize<ReviewDocument>(json, EvaluationJson.Options) : null;
    private static AiScenarioReleaseEvent? Lifecycle(IReadOnlyList<AiScenarioReleaseEvent> history, Guid id) =>
        history.Where(e => e.VersionId == id && e.Type is AiScenarioEventType.Published or AiScenarioEventType.RolledBack or AiScenarioEventType.Stopped)
            .OrderByDescending(e => e.Sequence).FirstOrDefault();
    private static AiScenarioEvaluation? FindApproved(IReadOnlyList<AiScenarioEvaluation> evidence, IReadOnlyList<AiScenarioReleaseEvent> history,
        EvaluationMode mode, string fingerprint) => evidence.FirstOrDefault(e => e.Mode == mode.ToString() && e.ModelFingerprint == fingerprint &&
            !history.Any(h => h.EvaluationId == e.Id && h.Type == AiScenarioEventType.EvaluationRevoked) &&
            EvaluationGate.Evaluate(Report(e), e.ReportHash, Review(history, e)).Passed);
    private static AiScenarioSnapshot ReadSnapshot(AiScenarioVersion v)
    {
        if (AiScenarioSnapshots.Digest(v.SnapshotJson) != v.ContentHash) Conflict("AI snapshot integrity check failed.");
        var snapshot = AiScenarioSnapshots.Read<AiScenarioSnapshot>(v.SnapshotJson);
        if (snapshot.TenantId != v.TenantId || snapshot.BuildIdentity != v.BuildIdentity) Conflict("AI snapshot ownership or build mismatch.");
        return snapshot;
    }
    private async Task AppendAsync(AiScenario s, Guid version, AiScenarioEventType type, string reason, CancellationToken ct,
        Guid? evaluationId = null, Guid? previousVersionId = null, string? reviewJson = null, string? qualificationJson = null)
    {
        Touch(s);
        await events.AddAsync(new() { TenantId = s.TenantId, ScenarioId = s.Id, VersionId = version, Sequence = s.Revision,
            Type = type, ActorUserId = current.UserId!.Value, Reason = reason, EvaluationId = evaluationId,
            PreviousVersionId = previousVersionId, ReviewJson = reviewJson, QualificationJson = qualificationJson }, ct);
    }
    private void Touch(AiScenario s) { s.Revision++; scenarios.Update(s); }
    private async Task<AiScenarioResponse> ResponseAsync(AiScenario s, CancellationToken ct) => new(s.Id, s.Code, s.Name, s.Description,
        s.IsEnabled, s.CurrentVersionId, s.Revision, Token(s), AiScenarioSnapshots.Read<AiScenarioConfiguration>((await DraftAsync(s, ct)).ConfigurationJson));
    private AiScenarioVersionResponse VersionResponse(AiScenarioVersion v, IReadOnlyList<AiScenarioReleaseEvent> history)
    {
        var compatible = true;
        try { snapshots.Validate(ReadSnapshot(v)); } catch (BusinessException) { compatible = false; }
        return new(v.Id, v.VersionNumber, v.ContentHash, v.BuildIdentity, v.CreatedAt,
            Lifecycle(history, v.Id) is not null, Lifecycle(history, v.Id)?.Type == AiScenarioEventType.Stopped, compatible);
    }
    private static AiScenarioEvaluationResponse EvaluationResponse(AiScenarioEvaluation e, IReadOnlyList<AiScenarioReleaseEvent> history) =>
        new(e.Id, e.VersionId, e.ReportHash, e.Mode, e.ModelFingerprint, e.AutomaticPassed, e.CreatedAt,
            history.Where(h => h.EvaluationId == e.Id && h.Type == AiScenarioEventType.Reviewed).OrderByDescending(h => h.Sequence).FirstOrDefault()?.ReviewJson);
    private static byte[] Token(AiScenario s) => s.RowVersion.Length > 0 ? s.RowVersion : BitConverter.GetBytes(s.Revision);
    private static void Match(AiScenario s, byte[]? token)
    {
        if (token is null || token.Length == 0 || !CryptographicOperations.FixedTimeEquals(Token(s), token))
            Conflict("Scenario changed or concurrency token is missing. Reload before saving.");
    }
    private static string Reason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) Invalid("A bounded operation reason is required.");
        return reason.Trim();
    }
    [DoesNotReturn]
    private static void Invalid(string message) => throw new BusinessException(ErrorCode.ValidationFailed, message);
    [DoesNotReturn]
    private static void Conflict(string message) => throw new BusinessException(ErrorCode.Conflict, message);
}
