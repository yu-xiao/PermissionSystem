using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Integration;
using PermissionSystem.Api.RateLimiting;
using PermissionSystem.Api.Services;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Api.Middlewares;

public sealed class ApiKeyAuthenticationMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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
        IClientIpAccessor clientIpAccessor,
        IDistributedRateLimitService rateLimitService,
        IOptions<RateLimitOptions> rateLimitOptions,
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
        var clientIp = clientIpAccessor.GetClientIp(context);
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
                clientIp,
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

            var settings = rateLimitOptions.Value;
            var rateLimitResult = settings.Enabled
                ? await rateLimitService.TryAcquireAsync(
                    "api-key",
                    clientId.Value.ToString("N"),
                    validation.RateLimitPerMinute,
                    TimeSpan.FromSeconds(settings.ApiKeyWindowSeconds),
                    context.RequestAborted)
                : RateLimitAcquireResult.Acquired;
            if (!rateLimitResult.IsAcquired)
            {
                statusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(rateLimitResult.RetryAfter.TotalSeconds)).ToString();
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
                    IpAddress = clientIp,
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

}
