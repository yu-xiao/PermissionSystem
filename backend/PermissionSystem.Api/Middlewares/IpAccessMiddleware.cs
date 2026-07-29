using System.Text.Json;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Security;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Middlewares;

public sealed class IpAccessMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public IpAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISecurityPolicyService securityPolicyService,
        IClientIpAccessor clientIpAccessor)
    {
        if (!await securityPolicyService.IsIpAllowedAsync(
                clientIpAccessor.GetClientIp(context),
                context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            var result = ApiResult.Fail(ErrorCode.Forbidden, "Current IP is not allowed to access the system.", context.TraceIdentifier);
            await context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions), context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
