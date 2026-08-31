using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.RateLimiting;

public sealed class DistributedRateLimitMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public DistributedRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDistributedRateLimitService rateLimitService,
        IClientIpAccessor clientIpAccessor,
        IOptions<RateLimitOptions> options,
        ILogger<DistributedRateLimitMiddleware> logger)
    {
        var settings = options.Value;
        if (!settings.Enabled || IsRateLimitExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var policy = ResolvePolicy(context, settings);
        var partitionKey = BuildIdentityKey(context, clientIpAccessor);
        var result = await rateLimitService.TryAcquireAsync(
            policy.Name,
            partitionKey,
            policy.PermitLimit,
            TimeSpan.FromSeconds(policy.WindowSeconds),
            context.RequestAborted);
        if (result.IsAcquired)
        {
            await _next(context);
            return;
        }

        logger.LogWarning(
            "Rate limit rejected. Method: {Method}, Path: {Path}, Policy: {Policy}, PartitionKeyHash: {PartitionKeyHash}, TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            policy.Name,
            HashPartitionKey(partitionKey),
            context.TraceIdentifier);

        context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString();
        context.Response.StatusCode = ErrorCodeHttpStatusMapper.GetStatusCode(ErrorCode.TooManyRequests);
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = ApiResult.Fail(
            ErrorCode.TooManyRequests,
            "Too many requests. Please try again later.",
            context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions), context.RequestAborted);
    }

    private static RateLimitPolicy ResolvePolicy(HttpContext context, RateLimitOptions settings)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.StartsWithSegments("/connect/authorize", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitPolicy("login", settings.LoginPermitLimit, settings.LoginWindowSeconds);
        }

        if (context.Request.Path.StartsWithSegments("/api/open-integration/webhooks", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitPolicy("webhook", settings.WebhookPermitLimit, settings.WebhookWindowSeconds);
        }

        if (context.Request.Path.StartsWithSegments("/connect/token", StringComparison.OrdinalIgnoreCase))
        {
            var grantType = context.Items[RateLimitMetadataKeys.GrantType] as string;
            if (string.Equals(grantType, OpenIddictConstants.GrantTypes.Password, StringComparison.OrdinalIgnoreCase))
            {
                return new RateLimitPolicy("login", settings.LoginPermitLimit, settings.LoginWindowSeconds);
            }

            if (string.Equals(grantType, OpenIddictConstants.GrantTypes.RefreshToken, StringComparison.OrdinalIgnoreCase))
            {
                return new RateLimitPolicy("refresh-token", settings.RefreshTokenPermitLimit, settings.RefreshTokenWindowSeconds);
            }

            return new RateLimitPolicy("token", settings.GlobalPermitLimit, settings.GlobalWindowSeconds);
        }

        if (context.Request.Path.StartsWithSegments("/api/reports", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitPolicy("report", settings.ReportPermitLimit, settings.ReportWindowSeconds);
        }

        return new RateLimitPolicy("global", settings.GlobalPermitLimit, settings.GlobalWindowSeconds);
    }

    private static string BuildIdentityKey(HttpContext context, IClientIpAccessor clientIpAccessor)
    {
        var userId = context.User.FindFirst(ClaimConstants.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        var clientId = context.Items[RateLimitMetadataKeys.ClientId] as string
            ?? context.User.FindFirst(OpenIddictConstants.Claims.ClientId)?.Value;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return $"client:{clientId.Trim()}";
        }

        var clientIp = clientIpAccessor.GetClientIp(context);
        return $"ip:{(string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp)}";
    }

    private static string HashPartitionKey(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static bool IsRateLimitExempt(PathString path)
    {
        return path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RateLimitPolicy(string Name, int PermitLimit, int WindowSeconds);
}
