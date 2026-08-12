using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Api.Middlewares;

public sealed class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public RequestMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var endpoint = context.GetEndpoint() as RouteEndpoint;
            var route = endpoint?.RoutePattern.RawText ?? "unmatched";
            ObservabilityMetrics.RecordHttpRequest(
                context.Request.Method,
                route,
                failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                stopwatch.Elapsed);
        }
    }
}
