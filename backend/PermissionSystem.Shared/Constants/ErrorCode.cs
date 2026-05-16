namespace PermissionSystem.Shared.Constants;

public enum ErrorCode
{
    Success = 0,
    BadRequest = 40000,
    Unauthorized = 40100,
    Forbidden = 40300,
    NotFound = 40400,
    Conflict = 40900,
    ValidationFailed = 42200,
    BusinessError = 50000,
    InternalServerError = 50099
}
