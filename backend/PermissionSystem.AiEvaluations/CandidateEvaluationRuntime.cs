using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.AiEvaluations;

// Only the isolated evaluation process can compose this runtime; it never publishes a candidate.
internal sealed class CandidateEvaluationRuntime(AiScenarioSnapshot snapshot, ICurrentUserService current,
    IUserCredentialValidator identities) : IAiScenarioRuntime
{
    public Guid ScenarioId { get; } = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public Task<AiScenarioVersion> ResolveCurrentAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        if (scenarioId != ScenarioId) throw new EvaluationInputException("Unknown isolated candidate.");
        return Task.FromResult(new AiScenarioVersion
        {
            Id = _versionId, TenantId = IsolatedEvaluationEnvironment.TenantId, ScenarioId = ScenarioId, VersionNumber = 1,
            SnapshotJson = AiScenarioSnapshots.Json(snapshot), ContentHash = AiScenarioSnapshots.Digest(AiScenarioSnapshots.Json(snapshot)),
            BuildIdentity = snapshot.BuildIdentity
        });
    }
    public async Task<AiScenarioSnapshot> ValidateAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        if (versionId != _versionId) throw new EvaluationInputException("Unknown isolated version.");
        var actor = await identities.GetAuthenticationStateAsync(IsolatedEvaluationEnvironment.TenantId,
            IsolatedEvaluationEnvironment.ActorId, cancellationToken);
        if (actor is null) throw new BusinessException(ErrorCode.Unauthorized, "Evaluation actor is inactive.");
        return snapshot;
    }
    public async Task ValidateToolAsync(Guid versionId, string toolCode, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(versionId, cancellationToken);
        var actor = await identities.GetAuthenticationStateAsync(IsolatedEvaluationEnvironment.TenantId,
            IsolatedEvaluationEnvironment.ActorId, cancellationToken);
        var tool = snapshot.Tools.SingleOrDefault(t => t.ToolCode == toolCode);
        if (tool is null || !tool.RequiredPermissions.All(p => current.HasPermission(p) &&
            PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(actor!.Roles), actor.PermissionCodes, p)))
            throw new BusinessException(ErrorCode.Forbidden, "Evaluation tool is not authorized.");
    }
    public async Task ValidateModelAsync(Guid versionId, AiProviderConfig provider, CancellationToken cancellationToken = default) =>
        await ValidateAsync(versionId, cancellationToken);
}
