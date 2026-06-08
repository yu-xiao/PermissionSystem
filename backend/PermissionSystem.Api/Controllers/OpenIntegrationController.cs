using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Integration;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/integration")]
public sealed class OpenIntegrationController : ApiControllerBase
{
    private readonly IOpenIntegrationService _openIntegrationService;

    public OpenIntegrationController(IOpenIntegrationService openIntegrationService)
    {
        _openIntegrationService = openIntegrationService;
    }

    [HttpGet("clients")]
    [Permission("integration:client:view")]
    public async Task<ActionResult<ApiResult<PagedResult<ApiClientResponse>>>> GetClientsAsync(
        [FromQuery] ApiClientQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.GetClientsAsync(request, cancellationToken));
    }

    [HttpPost("clients")]
    [Permission("integration:client:create")]
    public async Task<ActionResult<ApiResult<ApiClientResponse>>> CreateClientAsync(
        [FromBody] CreateApiClientRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.CreateClientAsync(request, cancellationToken));
    }

    [HttpPut("clients/{id:guid}")]
    [Permission("integration:client:update")]
    public async Task<ActionResult<ApiResult<ApiClientResponse>>> UpdateClientAsync(
        Guid id,
        [FromBody] UpdateApiClientRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.UpdateClientAsync(id, request, cancellationToken));
    }

    [HttpDelete("clients/{id:guid}")]
    [Permission("integration:client:delete")]
    public async Task<ActionResult<ApiResult>> DeleteClientAsync(Guid id, CancellationToken cancellationToken)
    {
        await _openIntegrationService.DeleteClientAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("clients/{id:guid}/generate-secret")]
    [Permission("integration:client:secret")]
    public async Task<ActionResult<ApiResult<GenerateApiClientSecretResponse>>> GenerateSecretAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.GenerateSecretAsync(id, cancellationToken));
    }

    [HttpPost("clients/{id:guid}/enable")]
    [Permission("integration:client:update")]
    public async Task<ActionResult<ApiResult>> EnableClientAsync(Guid id, CancellationToken cancellationToken)
    {
        await _openIntegrationService.SetClientEnabledAsync(id, true, cancellationToken);
        return Success();
    }

    [HttpPost("clients/{id:guid}/disable")]
    [Permission("integration:client:update")]
    public async Task<ActionResult<ApiResult>> DisableClientAsync(Guid id, CancellationToken cancellationToken)
    {
        await _openIntegrationService.SetClientEnabledAsync(id, false, cancellationToken);
        return Success();
    }

    [HttpGet("webhooks")]
    [Permission("integration:webhook:view")]
    public async Task<ActionResult<ApiResult<PagedResult<WebhookSubscriptionResponse>>>> GetWebhooksAsync(
        [FromQuery] WebhookQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.GetWebhooksAsync(request, cancellationToken));
    }

    [HttpPost("webhooks")]
    [Permission("integration:webhook:create")]
    public async Task<ActionResult<ApiResult<WebhookSubscriptionResponse>>> CreateWebhookAsync(
        [FromBody] CreateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.CreateWebhookAsync(request, cancellationToken));
    }

    [HttpPut("webhooks/{id:guid}")]
    [Permission("integration:webhook:update")]
    public async Task<ActionResult<ApiResult<WebhookSubscriptionResponse>>> UpdateWebhookAsync(
        Guid id,
        [FromBody] UpdateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.UpdateWebhookAsync(id, request, cancellationToken));
    }

    [HttpDelete("webhooks/{id:guid}")]
    [Permission("integration:webhook:delete")]
    public async Task<ActionResult<ApiResult>> DeleteWebhookAsync(Guid id, CancellationToken cancellationToken)
    {
        await _openIntegrationService.DeleteWebhookAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("webhooks/{id:guid}/test")]
    [Permission("integration:webhook:test")]
    public async Task<ActionResult<ApiResult>> TestWebhookAsync(Guid id, CancellationToken cancellationToken)
    {
        await _openIntegrationService.TestWebhookAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("webhook-logs")]
    [Permission("integration:log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<WebhookDeliveryLogResponse>>>> GetWebhookLogsAsync(
        [FromQuery] WebhookDeliveryLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.GetWebhookLogsAsync(request, cancellationToken));
    }

    [HttpGet("api-call-logs")]
    [Permission("integration:log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<ExternalApiCallLogResponse>>>> GetApiCallLogsAsync(
        [FromQuery] ExternalApiCallLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _openIntegrationService.GetApiCallLogsAsync(request, cancellationToken));
    }
}
