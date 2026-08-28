using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/mcp")]
public sealed class McpAdministrationController : ApiControllerBase
{
    private readonly IMcpAdministrationService _administrationService;

    public McpAdministrationController(IMcpAdministrationService administrationService)
    {
        _administrationService = administrationService;
    }

    [HttpGet("clients")]
    [Permission(AiCenterConstants.McpClientViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<McpClientResponse>>>> GetClientsAsync(
        [FromQuery] McpClientQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.GetClientsAsync(request, cancellationToken));
    }

    [HttpGet("clients/{id:guid}")]
    [Permission(AiCenterConstants.McpClientViewPermission)]
    public async Task<ActionResult<ApiResult<McpClientResponse>>> GetClientAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.GetClientAsync(id, cancellationToken));
    }

    [HttpPost("clients")]
    [Permission(AiCenterConstants.McpClientManagePermission)]
    [Permission(AiCenterConstants.McpClientSecretPermission)]
    public async Task<ActionResult<ApiResult<McpClientCredentialResponse>>> CreateClientAsync(
        [FromBody] CreateMcpClientRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.CreateClientAsync(request, cancellationToken));
    }

    [HttpPut("clients/{id:guid}")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.McpClientManagePermission)]
    public async Task<ActionResult<ApiResult<McpClientResponse>>> UpdateClientAsync(
        Guid id,
        [FromBody] UpdateMcpClientRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.UpdateClientAsync(id, request, cancellationToken));
    }

    [HttpPost("clients/{id:guid}/rotate-secret")]
    [Permission(AiCenterConstants.McpClientSecretPermission)]
    public async Task<ActionResult<ApiResult<McpClientCredentialResponse>>> RotateSecretAsync(
        Guid id,
        [FromBody] RotateMcpClientSecretRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.RotateSecretAsync(id, request, cancellationToken));
    }

    [HttpPut("clients/{id:guid}/enabled")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.McpClientManagePermission)]
    public async Task<ActionResult<ApiResult<McpClientResponse>>> SetEnabledAsync(
        Guid id,
        [FromBody] SetMcpClientEnabledRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.SetEnabledAsync(id, request, cancellationToken));
    }

    [HttpGet("datasets")]
    [Permission(AiCenterConstants.McpClientViewPermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<McpDatasetResponse>>>> GetDatasetsAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.GetDatasetsAsync(cancellationToken));
    }

    [HttpGet("invocations")]
    [Permission(AiCenterConstants.McpAuditViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<McpInvocationLogResponse>>>> GetInvocationLogsAsync(
        [FromQuery] McpInvocationLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _administrationService.GetInvocationLogsAsync(request, cancellationToken));
    }
}
