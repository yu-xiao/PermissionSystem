using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Domain.Enums;
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
        IUserSessionStatusChecker sessionStatusChecker,
        IMcpClientAccessService clientAccessService,
        IMcpCallerContext callerContext)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp") ||
            context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var callerType = context.User.FindFirst(ClaimConstants.McpCallerType)?.Value;
        if (string.Equals(callerType, McpCallerType.ServiceClient.ToString(), StringComparison.Ordinal))
        {
            await ValidateServiceClientAsync(context, tenantContext, clientAccessService, callerContext);
            return;
        }

        if (!string.IsNullOrWhiteSpace(callerType) &&
            !string.Equals(callerType, McpCallerType.DelegatedUser.ToString(), StringComparison.Ordinal))
        {
            await WriteFailureAsync(context, new ValidationFailure(
                StatusCodes.Status401Unauthorized,
                ErrorCode.Unauthorized,
                "The MCP caller type is invalid."));
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
        callerContext.SetDelegatedUser(identity.TenantId, identity.UserId, GetClientIp(context));

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

    private async Task ValidateServiceClientAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IMcpClientAccessService clientAccessService,
        IMcpCallerContext callerContext)
    {
        if (!TryResolveServiceIdentity(context, out var identity, out var failure))
        {
            await WriteFailureAsync(context, failure!);
            return;
        }

        var admission = await clientAccessService.AdmitRequestAsync(
            identity.OAuthClientId,
            GetClientIp(context),
            context.RequestAborted);
        if (!admission.Succeeded || admission.Client is null)
        {
            if (admission.IsRateLimited)
            {
                context.Response.Headers.RetryAfter = Math.Max(
                    1,
                    (int)Math.Ceiling(admission.RetryAfter.TotalSeconds)).ToString();
            }

            await WriteFailureAsync(context, new ValidationFailure(
                admission.IsRateLimited
                    ? StatusCodes.Status429TooManyRequests
                    : StatusCodes.Status403Forbidden,
                admission.IsRateLimited ? ErrorCode.TooManyRequests : ErrorCode.Forbidden,
                admission.ErrorMessage));
            return;
        }

        var client = admission.Client;
        if (client.TenantId != identity.TenantId ||
            client.ClientBindingId != identity.ClientBindingId ||
            client.ApiClientId != identity.ApiClientId)
        {
            await WriteFailureAsync(context, new ValidationFailure(
                StatusCodes.Status401Unauthorized,
                ErrorCode.Unauthorized,
                "The MCP client token no longer matches its binding."));
            return;
        }

        tenantContext.MarkAsHttpRequest();
        tenantContext.MarkAsSuperAdmin(false);
        tenantContext.SetTenant(client.TenantId, "McpClientBinding");
        callerContext.SetServiceClient(client, GetClientIp(context));
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

    internal static bool TryResolveServiceIdentity(
        HttpContext context,
        out McpServiceIdentity identity,
        out ValidationFailure? failure)
    {
        identity = default;
        failure = null;

        var oauthClientId = context.User.FindFirst(OpenIddict.Abstractions.OpenIddictConstants.Claims.ClientId)?.Value;
        var tenantValue = context.User.FindFirst(ClaimConstants.TenantId)?.Value;
        var bindingValue = context.User.FindFirst(ClaimConstants.McpClientBindingId)?.Value;
        var apiClientValue = context.User.FindFirst(ClaimConstants.ApiClientId)?.Value;
        if (string.IsNullOrWhiteSpace(oauthClientId) ||
            !Guid.TryParse(tenantValue, out var tenantId) ||
            !Guid.TryParse(bindingValue, out var bindingId) ||
            !Guid.TryParse(apiClientValue, out var apiClientId))
        {
            failure = new ValidationFailure(
                StatusCodes.Status401Unauthorized,
                ErrorCode.Unauthorized,
                "A bound MCP service client token is required.");
            return false;
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
            (!Guid.TryParse(tenantHeader.FirstOrDefault(), out var headerTenantId) || headerTenantId != tenantId))
        {
            failure = new ValidationFailure(
                StatusCodes.Status403Forbidden,
                ErrorCode.Forbidden,
                "The requested tenant does not match the MCP client binding.");
            return false;
        }

        identity = new McpServiceIdentity(oauthClientId, tenantId, bindingId, apiClientId);
        return true;
    }

    private static string GetClientIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return string.Empty;
        }

        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
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

    internal readonly record struct McpServiceIdentity(
        string OAuthClientId,
        Guid TenantId,
        Guid ClientBindingId,
        Guid ApiClientId);

    internal sealed record ValidationFailure(int StatusCode, ErrorCode ErrorCode, string Message);
}
