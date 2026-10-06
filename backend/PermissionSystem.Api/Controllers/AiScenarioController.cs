using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.AiEvaluations;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/scenarios")]
public sealed class AiScenarioController(IAiScenarioService service) : ApiControllerBase
{
    [HttpGet]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<AiScenarioResponse>>>> List(CancellationToken ct) => Success(await service.ListAsync(ct));

    [HttpGet("options")]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<AiScenarioOption>>>> Options(CancellationToken ct) => Success(await service.GetOptionsAsync(ct));

    [HttpGet("{id:guid}")]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<AiScenarioDetailResponse>>> Detail(Guid id, CancellationToken ct) => Success(await service.GetDetailAsync(id, ct));

    [HttpPut]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult<AiScenarioResponse>>> Save(SaveAiScenarioRequest request, CancellationToken ct) => Success(await service.SaveAsync(request, ct));

    [HttpPost("{id:guid}/freeze")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult<AiScenarioVersionResponse>>> Freeze(Guid id, AiScenarioChangeRequest request, CancellationToken ct) => Success(await service.FreezeAsync(id, request, ct));

    [HttpPost("versions/{id:guid}/copy")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult<AiScenarioResponse>>> Copy(Guid id, AiScenarioChangeRequest request, CancellationToken ct) => Success(await service.CopyToDraftAsync(id, request, ct));

    [HttpGet("versions/{id:guid}/snapshot")]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<AiScenarioSnapshot>>> Snapshot(Guid id, CancellationToken ct) => Success(await service.ExportAsync(id, ct));

    [HttpPost("versions/{id:guid}/evaluations")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult<AiScenarioEvaluationResponse>>> Import(Guid id, ImportAiScenarioEvaluationRequest request, CancellationToken ct) => Success(await service.ImportAsync(id, request, ct));

    [HttpGet("evaluations/{id:guid}/report")]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<EvaluationReport>>> Report(Guid id, CancellationToken ct) =>
        new JsonResult(ApiResult<EvaluationReport>.Success(await service.GetEvaluationReportAsync(id, ct)), EvaluationJson.Options);

    [HttpPost("evaluations/{id:guid}/review")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult>> Review(Guid id, ReviewAiScenarioEvaluationRequest request, CancellationToken ct)
    { await service.ReviewAsync(id, request, ct); return Success(); }

    [HttpPost("versions/{id:guid}/publish")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult>> Publish(Guid id, AiScenarioChangeRequest request, CancellationToken ct)
    { await service.PublishAsync(id, request, cancellationToken: ct); return Success(); }

    [HttpPost("versions/{id:guid}/rollback")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult>> Rollback(Guid id, AiScenarioChangeRequest request, CancellationToken ct)
    { await service.PublishAsync(id, request, rollback: true, cancellationToken: ct); return Success(); }

    [HttpPost("versions/{id:guid}/stop")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult>> Stop(Guid id, AiScenarioChangeRequest request, CancellationToken ct)
    { await service.StopAsync(id, request, ct); return Success(); }

    [HttpPost("evaluations/{id:guid}/revoke")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult>> Revoke(Guid id, AiScenarioChangeRequest request, CancellationToken ct)
    { await service.RevokeEvaluationAsync(id, request, ct); return Success(); }
}
