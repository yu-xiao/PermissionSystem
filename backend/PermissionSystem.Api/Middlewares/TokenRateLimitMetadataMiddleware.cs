using PermissionSystem.Api.RateLimiting;

namespace PermissionSystem.Api.Middlewares;

public sealed class TokenRateLimitMetadataMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRateLimitMetadataMiddleware> _logger;

    public TokenRateLimitMetadataMiddleware(
        RequestDelegate next,
        ILogger<TokenRateLimitMetadataMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsTokenRequest(context.Request))
        {
            try
            {
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                context.Items[RateLimitMetadataKeys.GrantType] = form["grant_type"].FirstOrDefault();
                context.Items[RateLimitMetadataKeys.ClientId] = form["client_id"].FirstOrDefault();
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Failed to read token request metadata for rate limiting.");
            }
        }

        await _next(context);
    }

    private static bool IsTokenRequest(HttpRequest request)
    {
        return HttpMethods.IsPost(request.Method)
            && request.Path.Equals("/connect/token", StringComparison.OrdinalIgnoreCase)
            && request.HasFormContentType;
    }
}
