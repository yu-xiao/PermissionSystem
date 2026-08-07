using System.Net;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Shared.Results;

public static class ErrorCodeHttpStatusMapper
{
    public static int GetStatusCode(ErrorCode errorCode)
    {
        return errorCode switch
        {
            ErrorCode.Success => (int)HttpStatusCode.OK,
            ErrorCode.BadRequest => (int)HttpStatusCode.BadRequest,
            ErrorCode.BusinessError => (int)HttpStatusCode.BadRequest,
            ErrorCode.Unauthorized => (int)HttpStatusCode.Unauthorized,
            ErrorCode.Forbidden => (int)HttpStatusCode.Forbidden,
            ErrorCode.NotFound => (int)HttpStatusCode.NotFound,
            ErrorCode.Conflict => (int)HttpStatusCode.Conflict,
            ErrorCode.ValidationFailed => 422,
            ErrorCode.TooManyRequests => 429,
            ErrorCode.InternalServerError => (int)HttpStatusCode.InternalServerError,
            _ => (int)HttpStatusCode.InternalServerError
        };
    }
}
