using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteSummaryAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = ApiResult<HealthSummaryResponse>.Success(new HealthSummaryResponse
        {
            Status = report.Status.ToString(),
            TotalDurationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            CheckedAt = DateTimeOffset.UtcNow
        });

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions), context.RequestAborted);
    }
}

public sealed class HealthSummaryResponse
{
    public string Status { get; init; } = string.Empty;

    public double TotalDurationMilliseconds { get; init; }

    public DateTimeOffset CheckedAt { get; init; }
}
