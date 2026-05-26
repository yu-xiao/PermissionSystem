using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.DemoApprovalOrders;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/demo-approval-orders")]
public sealed class DemoApprovalOrderController : ApiControllerBase
{
    private readonly IDemoApprovalOrderService _demoApprovalOrderService;

    public DemoApprovalOrderController(IDemoApprovalOrderService demoApprovalOrderService)
    {
        _demoApprovalOrderService = demoApprovalOrderService;
    }

    [HttpGet]
    [Permission("demo-approval-order:view")]
    public async Task<ActionResult<ApiResult<PagedResult<DemoApprovalOrderResponse>>>> GetPagedAsync(
        [FromQuery] DemoApprovalOrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoApprovalOrderService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("demo-approval-order:view")]
    public async Task<ActionResult<ApiResult<DemoApprovalOrderResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _demoApprovalOrderService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-approval-order:create")]
    public async Task<ActionResult<ApiResult<DemoApprovalOrderResponse>>> CreateAsync(
        [FromBody] CreateDemoApprovalOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoApprovalOrderService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("demo-approval-order:update")]
    public async Task<ActionResult<ApiResult<DemoApprovalOrderResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateDemoApprovalOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoApprovalOrderService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("demo-approval-order:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _demoApprovalOrderService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/submit")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-approval-order:submit")]
    public async Task<ActionResult<ApiResult<DemoApprovalOrderResponse>>> SubmitAsync(
        Guid id,
        [FromBody] SubmitDemoApprovalOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoApprovalOrderService.SubmitAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/withdraw")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-approval-order:withdraw")]
    public async Task<ActionResult<ApiResult<DemoApprovalOrderResponse>>> WithdrawAsync(
        Guid id,
        [FromBody] WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoApprovalOrderService.WithdrawAsync(id, request, cancellationToken));
    }
}
