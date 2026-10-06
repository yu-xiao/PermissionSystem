using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/permission-diagnostics")]
public sealed class AiPermissionDiagnosticController(IPermissionDiagnosticService service) : ApiControllerBase
{
    [HttpPost]
    [Permission(AiCenterConstants.ToolQueryPermission)]
    public async Task<ActionResult<ApiResult<PermissionDiagnosticResponse>>> DiagnoseAsync(
        [FromBody] PermissionDiagnosticRequest request, CancellationToken cancellationToken)
    {
        return Success(await service.DiagnoseAsync(request, cancellationToken));
    }
}
