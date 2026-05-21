namespace PermissionSystem.Api.Middlewares;

public sealed class SignalRAccessTokenMiddleware
{
    private readonly RequestDelegate _next;

    public SignalRAccessTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/hubs/notifications") &&
            string.IsNullOrWhiteSpace(context.Request.Headers.Authorization) &&
            context.Request.Query.TryGetValue("access_token", out var accessToken) &&
            !string.IsNullOrWhiteSpace(accessToken))
        {
            context.Request.Headers.Authorization = $"Bearer {accessToken}";
        }

        await _next(context);
    }
}
