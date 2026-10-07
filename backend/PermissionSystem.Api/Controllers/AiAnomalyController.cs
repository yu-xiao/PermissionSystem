using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/anomalies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiAnomalyController(IAiAnomalyService service) : ApiControllerBase
{
    [HttpGet("rules")]
    [Permission(AiAnomalyContract.ViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<AiAnomalyRuleResponse>>>> List(CancellationToken ct, int pageIndex = 1, int pageSize = 20) =>
        Success(await service.ListAsync(pageIndex, pageSize, ct));

    [HttpPost("rules")]
    [Permission(AiAnomalyContract.CreatePermission)]
    public async Task<ActionResult<ApiResult<AiAnomalyRuleResponse>>> Create(CancellationToken ct) => Success(await service.CreateAsync(ct));

    [HttpPut("rules/{id:guid}/enabled")]
    [Permission(AiAnomalyContract.UpdatePermission)]
    public async Task<ActionResult<ApiResult<AiAnomalyRuleResponse>>> Enable(Guid id, AiAnomalyChangeRequest request, CancellationToken ct) =>
        Success(await service.SetEnabledAsync(id, request, ct));

    [HttpPost("rules/{id:guid}/check")]
    [Permission(AiAnomalyContract.TriggerPermission)]
    public async Task<ActionResult<ApiResult>> Check(Guid id, CancellationToken ct)
    { await service.TriggerAsync(id, ct); return Success(); }

    [HttpGet("rules/{id:guid}/events")]
    [Permission(AiAnomalyContract.ViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<AiAnomalyEventResponse>>>> Events(Guid id, CancellationToken ct, int pageIndex = 1, int pageSize = 20) =>
        Success(await service.EventsAsync(id, pageIndex, pageSize, ct));

    [HttpGet("events/{id:guid}")]
    [Permission(AiAnomalyContract.ViewPermission)]
    public async Task<ActionResult<ApiResult<AiAnomalyEventResponse>>> Event(Guid id, CancellationToken ct) => Success(await service.EventAsync(id, ct));

    [HttpPost("events/{id:guid}/close")]
    [Permission(AiAnomalyContract.UpdatePermission)]
    public async Task<ActionResult<ApiResult>> Close(Guid id, AiAnomalyCloseRequest request, CancellationToken ct)
    { await service.CloseAsync(id, request.RowVersion, ct); return Success(); }
}
