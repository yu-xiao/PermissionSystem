using System.Diagnostics;
using Serilog.Context;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Api.Middlewares;

public sealed class TraceIdMiddleware
{
    public const string TraceHeaderName = "X-Trace-Id";

    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITraceContextAccessor traceContextAccessor)
    {
        var traceId = ResolveTraceId(context);
        traceContextAccessor.TraceId = traceId;
        context.TraceIdentifier = traceId;
        Activity.Current?.SetTag("app.trace_id", traceId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[TraceHeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("TraceId", traceId))
        {
            await _next(context);
        }
    }

    private static string ResolveTraceId(HttpContext context)
    {
        var headerTraceId = context.Request.Headers[TraceHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerTraceId))
        {
            return NormalizeTraceId(headerTraceId);
        }

        var activityTraceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(activityTraceId))
        {
            return activityTraceId;
        }

        return ActivityTraceId.CreateRandom().ToString();
    }

    private static string NormalizeTraceId(string value)
    {
        var traceId = value.Trim();
        return traceId.Length <= 128 ? traceId : traceId[..128];
    }
}
