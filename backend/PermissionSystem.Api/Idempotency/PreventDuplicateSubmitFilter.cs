using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Idempotency;

public sealed class PreventDuplicateSubmitFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PreventDuplicateSubmitFilter> _logger;

    public PreventDuplicateSubmitFilter(
        IIdempotencyService idempotencyService,
        ICurrentUserService currentUserService,
        ILogger<PreventDuplicateSubmitFilter> logger)
    {
        _idempotencyService = idempotencyService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public int Order => -1000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata.OfType<PreventDuplicateSubmitAttribute>().FirstOrDefault();
        if (attribute is null || IsReadOnlyRequest(context.HttpContext.Request))
        {
            await next();
            return;
        }

        var lockKey = BuildScopedKey(context.HttpContext);
        var acquired = await _idempotencyService.TryAcquireDuplicateSubmitLockAsync(
            lockKey,
            TimeSpan.FromSeconds(attribute.LockSeconds),
            context.HttpContext.RequestAborted);

        if (!acquired)
        {
            _logger.LogWarning(
                "Duplicate submit blocked. UserId: {UserId}, Path: {Path}, Method: {Method}",
                _currentUserService.UserId,
                context.HttpContext.Request.Path,
                context.HttpContext.Request.Method);

            context.Result = new ConflictObjectResult(ApiResult.Fail(
                ErrorCode.Conflict,
                "Duplicate submit detected. Please try again later.",
                context.HttpContext.TraceIdentifier));
            return;
        }

        await next();
    }

    private string BuildScopedKey(HttpContext context)
    {
        var userPart = _currentUserService.UserId?.ToString("N") ??
            _currentUserService.Username ??
            "anonymous";
        var tenantPart = _currentUserService.TenantId?.ToString("N") ?? "default";
        var rawKey = string.Join(
            '|',
            tenantPart,
            userPart,
            context.Request.Method.ToUpperInvariant(),
            context.Request.Path.Value?.ToLowerInvariant());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    }

    private static bool IsReadOnlyRequest(HttpRequest request)
    {
        return HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method);
    }
}
