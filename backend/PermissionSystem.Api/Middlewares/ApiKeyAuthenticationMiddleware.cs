using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Integration;
using PermissionSystem.Api.RateLimiting;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Middlewares;

public sealed class ApiKeyAuthenticationMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, RateWindow> RateWindows = new();
    private readonly RequestDelegate _next;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOpenIntegrationService openIntegrationService,
        IApiClientContext apiClientContext,
        ITenantContext tenantContext,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        var apiSecret = context.Request.Headers["X-Api-Secret"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(apiSecret))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var tenantId = tenantContext.TenantId ?? Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid? clientId = null;
        var statusCode = StatusCodes.Status200OK;
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                await WriteFailureAsync(context, statusCode, "API key and secret are required.");
                return;
            }

            var validation = await openIntegrationService.ValidateApiClientAsync(
                apiKey,
                apiSecret,
                GetClientIp(context),
                context.RequestAborted);
            if (!validation.Succeeded || !validation.ClientId.HasValue || !validation.TenantId.HasValue)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                await WriteFailureAsync(context, statusCode, validation.ErrorMessage ?? "API client authentication failed.");
                return;
            }

            tenantId = validation.TenantId.Value;
            clientId = validation.ClientId.Value;
            tenantContext.SetTenant(tenantId, "ApiKey");
            apiClientContext.SetClient(clientId.Value, validation.ClientCode ?? apiKey, validation.AllowedScopes);
            context.User = BuildApiClientPrincipal(clientId.Value, tenantId, validation.ClientCode ?? apiKey, validation.AllowedScopes);
            context.Items[RateLimitMetadataKeys.ClientId] = clientId.Value.ToString("N");

            if (IsRateLimited(clientId.Value, validation.RateLimitPerMinute))
            {
                statusCode = StatusCodes.Status429TooManyRequests;
                await WriteFailureAsync(context, statusCode, "API client rate limit exceeded.");
                return;
            }

            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch
        {
            statusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            try
            {
                await openIntegrationService.RecordExternalApiCallAsync(new RecordExternalApiCallRequest
                {
                    TenantId = tenantId,
                    ClientId = clientId,
                    Path = context.Request.Path.Value ?? string.Empty,
                    Method = context.Request.Method,
                    IpAddress = GetClientIp(context),
                    StatusCode = statusCode,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to record external API call log. ClientId: {ClientId}, Path: {Path}", clientId, context.Request.Path);
            }
        }
    }

    private static ClaimsPrincipal BuildApiClientPrincipal(
        Guid clientId,
        Guid tenantId,
        string clientCode,
        string? allowedScopes)
    {
        var identity = new ClaimsIdentity("ApiKey", OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, $"api-client:{clientId:N}"));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.ClientId, clientId.ToString("N")));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, clientCode));
        identity.AddClaim(new Claim(ClaimConstants.Username, clientCode));
        identity.AddClaim(new Claim(ClaimConstants.TenantId, tenantId.ToString()));

        foreach (var scope in ParseScopes(allowedScopes))
        {
            identity.AddClaim(new Claim(ClaimConstants.PermissionCode, scope));
        }

        return new ClaimsPrincipal(identity);
    }

    private static IReadOnlyCollection<string> ParseScopes(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static bool IsRateLimited(Guid clientId, int rateLimitPerMinute)
    {
        if (rateLimitPerMinute <= 0)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        var key = $"{clientId:N}:{windowStart:yyyyMMddHHmm}";
        var window = RateWindows.AddOrUpdate(
            key,
            _ => new RateWindow(windowStart, 1),
            (_, current) => current.WindowStart == windowStart
                ? current.Increment()
                : new RateWindow(windowStart, 1));

        foreach (var expired in RateWindows.Where(item => item.Value.WindowStart < windowStart.AddMinutes(-2)).Select(item => item.Key).ToList())
        {
            RateWindows.TryRemove(expired, out _);
        }

        return window.Count > rateLimitPerMinute;
    }

    private static async Task WriteFailureAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var errorCode = statusCode == StatusCodes.Status429TooManyRequests
            ? ErrorCode.TooManyRequests
            : ErrorCode.Unauthorized;
        var result = ApiResult.Fail(errorCode, message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions), context.RequestAborted);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private sealed record RateWindow(DateTimeOffset WindowStart, int Count)
    {
        public RateWindow Increment()
        {
            return this with { Count = Count + 1 };
        }
    }
}
