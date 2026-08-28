using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.McpServer.Middlewares;

public sealed class McpCallerValidationMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public McpCallerValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantStatusChecker tenantStatusChecker,
        IUserSessionStatusChecker sessionStatusChecker)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp") ||
            context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (!TryResolveIdentity(context, out var identity, out var failure))
        {
            await WriteFailureAsync(context, failure!);
            return;
        }

        tenantContext.MarkAsHttpRequest();
        tenantContext.MarkAsSuperAdmin(identity.IsSuperAdmin);
        tenantContext.SetTenant(identity.TenantId, "Claims");

        if (!await tenantStatusChecker.IsActiveAsync(identity.TenantId, context.RequestAborted))
        {
            await WriteFailureAsync(context, new ValidationFailure(
                StatusCodes.Status403Forbidden,
                ErrorCode.Forbidden,
                "The tenant is not active."));
            return;
        }

        var sessionStatus = await sessionStatusChecker.ValidateAccessAsync(
            identity.TenantId,
            identity.UserId,
            identity.SessionId,
            identity.SecurityStamp,
            context.RequestAborted);
        if (sessionStatus != UserAccessValidationStatus.Valid)
        {
            await WriteFailureAsync(context, new ValidationFailure(
                StatusCodes.Status401Unauthorized,
                ErrorCode.Unauthorized,
                sessionStatus == UserAccessValidationStatus.StaleAuthorization
                    ? "Current authorization state is stale."
                    : "Current session is no longer valid."));
            return;
        }

        await _next(context);
    }

    internal static bool TryResolveIdentity(
        HttpContext context,
        out McpCallerIdentity identity,
        out ValidationFailure? failure)
    {
        identity = default;
        failure = null;

        var tenantValue = context.User.FindFirst(ClaimConstants.TenantId)?.Value;
        var userValue = context.User.FindFirst(ClaimConstants.UserId)?.Value;
        var sessionId = context.User.FindFirst(ClaimConstants.SessionId)?.Value;
        var securityStampValue = context.User.FindFirst(ClaimConstants.SecurityStamp)?.Value;
        if (!Guid.TryParse(tenantValue, out var tenantId) ||
            !Guid.TryParse(userValue, out var userId) ||
            string.IsNullOrWhiteSpace(sessionId) ||
            !Guid.TryParse(securityStampValue, out var securityStamp))
        {
            failure = new ValidationFailure(
                StatusCodes.Status401Unauthorized,
                ErrorCode.Unauthorized,
                "A delegated user token is required.");
            return false;
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
            (!Guid.TryParse(tenantHeader.FirstOrDefault(), out var headerTenantId) || headerTenantId != tenantId))
        {
            failure = new ValidationFailure(
                StatusCodes.Status403Forbidden,
                ErrorCode.Forbidden,
                "The requested tenant does not match the delegated identity.");
            return false;
        }

        var isSuperAdmin = context.User
            .FindAll(OpenIddict.Abstractions.OpenIddictConstants.Claims.Role)
            .Any(claim => string.Equals(
                claim.Value,
                ClaimConstants.SuperAdminRoleCode,
                StringComparison.OrdinalIgnoreCase));
        identity = new McpCallerIdentity(tenantId, userId, sessionId, securityStamp, isSuperAdmin);
        return true;
    }

    private static async Task WriteFailureAsync(HttpContext context, ValidationFailure failure)
    {
        context.Response.StatusCode = failure.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var result = ApiResult.Fail(failure.ErrorCode, failure.Message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions), context.RequestAborted);
    }

    internal readonly record struct McpCallerIdentity(
        Guid TenantId,
        Guid UserId,
        string SessionId,
        Guid SecurityStamp,
        bool IsSuperAdmin);

    internal sealed record ValidationFailure(int StatusCode, ErrorCode ErrorCode, string Message);
}
