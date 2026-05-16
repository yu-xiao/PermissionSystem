using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Shared.Results;

public sealed class ApiResult
{
    public bool Succeeded { get; init; }

    public int Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? TraceId { get; init; }

    public static ApiResult Success(string message = "Success")
    {
        return new ApiResult
        {
            Succeeded = true,
            Code = (int)ErrorCode.Success,
            Message = message
        };
    }

    public static ApiResult Fail(ErrorCode errorCode, string message, string? traceId = null)
    {
        return new ApiResult
        {
            Succeeded = false,
            Code = (int)errorCode,
            Message = message,
            TraceId = traceId
        };
    }
}

public sealed class ApiResult<T>
{
    public bool Succeeded { get; init; }

    public int Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public string? TraceId { get; init; }

    public static ApiResult<T> Success(T? data, string message = "Success")
    {
        return new ApiResult<T>
        {
            Succeeded = true,
            Code = (int)ErrorCode.Success,
            Message = message,
            Data = data
        };
    }

    public static ApiResult<T> Fail(ErrorCode errorCode, string message, string? traceId = null, T? data = default)
    {
        return new ApiResult<T>
        {
            Succeeded = false,
            Code = (int)errorCode,
            Message = message,
            Data = data,
            TraceId = traceId
        };
    }
}
