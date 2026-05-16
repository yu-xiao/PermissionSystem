using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Shared.Exceptions;

public sealed class BusinessException : Exception
{
    public BusinessException(string message)
        : this(ErrorCode.BusinessError, message)
    {
    }

    public BusinessException(ErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public BusinessException(ErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public ErrorCode ErrorCode { get; }
}
