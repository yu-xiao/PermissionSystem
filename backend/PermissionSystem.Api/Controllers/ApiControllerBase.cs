using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResult<T>> Success<T>(T data, string message = "Success")
    {
        return Ok(ApiResult<T>.Success(data, message));
    }

    protected ActionResult<ApiResult> Success(string message = "Success")
    {
        return Ok(ApiResult.Success(message));
    }
}
