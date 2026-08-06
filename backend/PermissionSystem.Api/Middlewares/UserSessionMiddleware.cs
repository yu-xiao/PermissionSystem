using System.Text.Json;
using PermissionSystem.Api.Authentication;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Middlewares;

public sealed class UserSessionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public UserSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUserSessionStatusChecker sessionStatusChecker,
        IUserSessionService userSessionService)
    {
        if (TokenEndpointRequestClassifier.IsRefreshTokenGrant(context))
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimConstants.UserId)?.Value;
        var sessionId = context.User.FindFirst(ClaimConstants.SessionId)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) && string.IsNullOrWhiteSpace(sessionId))
        {
            await _next(context);
            return;
        }

        var tenantIdClaim = context.User.FindFirst(ClaimConstants.TenantId)?.Value;
        var securityStampClaim = context.User.FindFirst(ClaimConstants.SecurityStamp)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) ||
            !Guid.TryParse(tenantIdClaim, out var tenantId) ||
            string.IsNullOrWhiteSpace(sessionId))
        {
            await WriteUnauthorizedAsync(
                context,
                "X-Session-Revoked",
                "Current session is invalid. Please sign in again.");
            return;
        }

        if (!Guid.TryParse(securityStampClaim, out var securityStamp))
        {
            await WriteUnauthorizedAsync(
                context,
                "X-Authorization-Stale",
                "Current authorization state is stale. Please refresh the access token.");
            return;
        }

        var status = await sessionStatusChecker.ValidateAccessAsync(
            tenantId,
            userId,
            sessionId,
            securityStamp,
            context.RequestAborted);
        if (status != UserAccessValidationStatus.Valid)
        {
            var authorizationStale = status == UserAccessValidationStatus.StaleAuthorization;
            await WriteUnauthorizedAsync(
                context,
                authorizationStale ? "X-Authorization-Stale" : "X-Session-Revoked",
                authorizationStale
                    ? "Current authorization state is stale. Please refresh the access token."
                    : "Current session has been revoked. Please sign in again.");
            return;
        }

        await userSessionService.TouchAsync(sessionId, context.RequestAborted);
        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(
        HttpContext context,
        string responseHeader,
        string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers[responseHeader] = "true";

        var result = ApiResult.Fail(
            ErrorCode.Unauthorized,
            message,
            context.TraceIdentifier);
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(result, JsonOptions),
            context.RequestAborted);
    }
}
