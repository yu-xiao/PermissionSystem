using System.Text.Json;
using PermissionSystem.Api.Authentication;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Middlewares;

public sealed class UserSessionMiddleware
{
    private readonly RequestDelegate _next;

    public UserSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserSessionService userSessionService)
    {
        if (TokenEndpointRequestClassifier.IsRefreshTokenGrant(context))
        {
            await _next(context);
            return;
        }

        var sessionId = context.User.FindFirst(ClaimConstants.SessionId)?.Value;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await _next(context);
            return;
        }

        if (await userSessionService.IsRevokedAsync(sessionId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["X-Session-Revoked"] = "true";

            var result = ApiResult.Fail(
                ErrorCode.Unauthorized,
                "Current session has been revoked. Please sign in again.",
                context.TraceIdentifier);

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                context.RequestAborted);
            return;
        }

        await userSessionService.TouchAsync(sessionId, context.RequestAborted);
        await _next(context);
    }
}
