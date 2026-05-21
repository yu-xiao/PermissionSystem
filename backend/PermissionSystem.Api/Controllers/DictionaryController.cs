using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Dictionaries;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/dictionaries")]
public sealed class DictionaryController : ApiControllerBase
{
    private readonly IDictionaryService _dictionaryService;

    public DictionaryController(IDictionaryService dictionaryService)
    {
        _dictionaryService = dictionaryService;
    }

    [HttpGet("types")]
    [Permission("system:dict:view")]
    public async Task<ActionResult<ApiResult<PagedResult<DictionaryTypeResponse>>>> GetTypesPagedAsync(
        [FromQuery] DictionaryTypeQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.GetTypesPagedAsync(request, cancellationToken));
    }

    [HttpPost("types")]
    [Permission("system:dict:create")]
    public async Task<ActionResult<ApiResult<DictionaryTypeResponse>>> CreateTypeAsync(
        [FromBody] CreateDictionaryTypeRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.CreateTypeAsync(request, cancellationToken));
    }

    [HttpPut("types/{id:guid}")]
    [Permission("system:dict:update")]
    public async Task<ActionResult<ApiResult<DictionaryTypeResponse>>> UpdateTypeAsync(
        Guid id,
        [FromBody] UpdateDictionaryTypeRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.UpdateTypeAsync(id, request, cancellationToken));
    }

    [HttpDelete("types/{id:guid}")]
    [Permission("system:dict:delete")]
    public async Task<ActionResult<ApiResult>> DeleteTypeAsync(Guid id, CancellationToken cancellationToken)
    {
        await _dictionaryService.DeleteTypeAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("items")]
    [Permission("system:dict:view")]
    public async Task<ActionResult<ApiResult<PagedResult<DictionaryItemResponse>>>> GetItemsPagedAsync(
        [FromQuery] DictionaryItemQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.GetItemsPagedAsync(request, cancellationToken));
    }

    [HttpPost("items")]
    [Permission("system:dict:create")]
    public async Task<ActionResult<ApiResult<DictionaryItemResponse>>> CreateItemAsync(
        [FromBody] CreateDictionaryItemRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.CreateItemAsync(request, cancellationToken));
    }

    [HttpPut("items/{id:guid}")]
    [Permission("system:dict:update")]
    public async Task<ActionResult<ApiResult<DictionaryItemResponse>>> UpdateItemAsync(
        Guid id,
        [FromBody] UpdateDictionaryItemRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.UpdateItemAsync(id, request, cancellationToken));
    }

    [HttpDelete("items/{id:guid}")]
    [Permission("system:dict:delete")]
    public async Task<ActionResult<ApiResult>> DeleteItemAsync(Guid id, CancellationToken cancellationToken)
    {
        await _dictionaryService.DeleteItemAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("types/{typeCode}/items/enabled")]
    [Permission("system:dict:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<DictionaryItemResponse>>>> GetEnabledItemsAsync(
        string typeCode,
        CancellationToken cancellationToken)
    {
        return Success(await _dictionaryService.GetEnabledItemsByTypeCodeAsync(typeCode, cancellationToken));
    }
}
