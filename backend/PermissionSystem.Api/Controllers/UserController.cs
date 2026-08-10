using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Users;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/users")]
public sealed class UserController : ApiControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserDataScopeService _userDataScopeService;

    public UserController(IUserService userService, IUserDataScopeService userDataScopeService)
    {
        _userService = userService;
        _userDataScopeService = userDataScopeService;
    }

    [HttpGet]
    [Permission("system:user:view")]
    public async Task<ActionResult<ApiResult<PagedResult<UserResponse>>>> GetPagedAsync(
        [FromQuery] UserQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("system:user:create")]
    public async Task<ActionResult<ApiResult<UserResponse>>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult<UserResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:user:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPatch("{id:guid}/enabled")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult>> SetEnabledAsync(
        Guid id,
        [FromBody] SetUserEnabledRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.SetEnabledAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/reset-password")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult>> ResetPasswordAsync(
        Guid id,
        [FromBody] ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/roles")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult>> AssignRolesAsync(
        Guid id,
        [FromBody] AssignUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.AssignRolesAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpGet("{id:guid}/data-scope")]
    [Permission("system:role:data-scope")]
    public async Task<ActionResult<ApiResult<UserDataScopeResponse>>> GetDataScopeAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _userDataScopeService.GetUserDataScopeAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/data-scope")]
    [Permission("system:role:data-scope")]
    public async Task<ActionResult<ApiResult>> SetDataScopeAsync(
        Guid id,
        [FromBody] SetUserDataScopeRequest request,
        CancellationToken cancellationToken)
    {
        await _userDataScopeService.SetUserDataScopeAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpDelete("{id:guid}/data-scope")]
    [Permission("system:role:data-scope")]
    public async Task<ActionResult<ApiResult>> ClearDataScopeAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userDataScopeService.ClearUserDataScopeAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("export")]
    [Permission("system:user:export")]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] UserQueryRequest request,
        CancellationToken cancellationToken)
    {
        var content = await _userService.ExportAsync(request, cancellationToken);
        var fileName = $"users-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("import-template")]
    [Permission("system:user:import")]
    public async Task<IActionResult> DownloadImportTemplateAsync(CancellationToken cancellationToken)
    {
        var content = await _userService.CreateImportTemplateAsync(cancellationToken);
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "user-import-template.xlsx");
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [Permission("system:user:import")]
    public async Task<ActionResult<ApiResult<ImportResult<UserImportRow>>>> ImportAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(ApiResult<ImportResult<UserImportRow>>.Fail(
                ErrorCode.ValidationFailed,
                "File is required.",
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        return Success(await _userService.ImportPreviewAsync(stream, cancellationToken));
    }
}
