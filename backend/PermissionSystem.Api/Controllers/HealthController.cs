using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[AllowAnonymous]
[Route("health")]
[Route("api/health")]
public sealed class HealthController : ApiControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<HealthSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<HealthSummaryResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResult<HealthSummaryResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
        var response = new HealthSummaryResponse
        {
            Status = report.Status.ToString(),
            TotalDurationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            CheckedAt = DateTimeOffset.UtcNow
        };

        return ToHealthResult(report.Status, ApiResult<HealthSummaryResponse>.Success(response));
    }

    [HttpGet("detail")]
    [ProducesResponseType(typeof(ApiResult<HealthDetailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResult<HealthDetailResponse>>> GetDetailAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
        var response = new HealthDetailResponse
        {
            Status = report.Status.ToString(),
            TotalDurationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            CheckedAt = DateTimeOffset.UtcNow,
            Entries = report.Entries
                .OrderBy(entry => entry.Key)
                .Select(entry => new HealthEntryResponse
                {
                    Name = entry.Key,
                    Status = entry.Value.Status.ToString(),
                    DurationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                    Description = entry.Value.Description,
                    Error = entry.Value.Exception?.Message,
                    Tags = entry.Value.Tags.ToArray(),
                    Data = entry.Value.Data.ToDictionary(
                        item => item.Key,
                        item => item.Value?.ToString())
                })
                .ToArray()
        };

        return Ok(ApiResult<HealthDetailResponse>.Success(response));
    }

    private static ObjectResult ToHealthResult<T>(HealthStatus status, ApiResult<T> result)
    {
        var statusCode = status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return new ObjectResult(result)
        {
            StatusCode = statusCode
        };
    }
}

public class HealthSummaryResponse
{
    public string Status { get; init; } = string.Empty;

    public double TotalDurationMilliseconds { get; init; }

    public DateTimeOffset CheckedAt { get; init; }
}

public sealed class HealthDetailResponse : HealthSummaryResponse
{
    public IReadOnlyCollection<HealthEntryResponse> Entries { get; init; } = [];
}

public sealed class HealthEntryResponse
{
    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public double DurationMilliseconds { get; init; }

    public string? Description { get; init; }

    public string? Error { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } = [];

    public IReadOnlyDictionary<string, string?> Data { get; init; } = new Dictionary<string, string?>();
}
