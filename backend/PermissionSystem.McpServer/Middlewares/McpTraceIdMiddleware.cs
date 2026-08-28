using System.Diagnostics;
using PermissionSystem.Application.Abstractions;
using Serilog.Context;

namespace PermissionSystem.McpServer.Middlewares;

public sealed class McpTraceIdMiddleware
{
    public const string TraceHeaderName = "X-Trace-Id";

    private readonly RequestDelegate _next;

    public McpTraceIdMiddleware(RequestDelegate next)
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
        var supplied = context.Request.Headers[TraceHeaderName].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return supplied.Length <= 128 ? supplied : supplied[..128];
        }

        return Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
    }
}
