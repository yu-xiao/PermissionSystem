using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Authentication;

public sealed class ApiAuthenticationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await WriteResultAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCode.Unauthorized,
                "Authentication is required.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await WriteResultAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCode.Forbidden,
                "Permission denied.");
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task WriteResultAsync(
        HttpContext context,
        int statusCode,
        ErrorCode errorCode,
        string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var result = ApiResult.Fail(errorCode, message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions), context.RequestAborted);
    }
}
