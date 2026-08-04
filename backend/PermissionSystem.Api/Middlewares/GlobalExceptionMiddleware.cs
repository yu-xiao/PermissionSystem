using System.Text.Json;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException exception)
        {
            await WriteErrorAsync(
                context,
                GetStatusCode(exception.ErrorCode),
                exception.ErrorCode,
                exception.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request was canceled by the client. TraceId: {TraceId}", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);

            var message = _environment.IsDevelopment()
                ? exception.Message
                : "An unexpected server error occurred.";

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCode.InternalServerError,
                message);
        }
    }

    private static int GetStatusCode(ErrorCode errorCode)
    {
        return errorCode switch
        {
            ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCode.ValidationFailed => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        ErrorCode errorCode,
        string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var result = ApiResult.Fail(errorCode, message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions), context.RequestAborted);
    }
}
