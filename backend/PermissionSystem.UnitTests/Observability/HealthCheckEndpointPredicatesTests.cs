using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Api.HealthChecks;

namespace PermissionSystem.UnitTests.Observability;

public sealed class HealthCheckEndpointPredicatesTests
{
    [Fact]
    public void IsLive_ShouldOnlySelectLivenessChecks()
    {
        var live = CreateRegistration("api-self", ["live"]);
        var ready = CreateRegistration("sql-server", ["ready", "database"]);

        Assert.True(HealthCheckEndpointPredicates.IsLive(live));
        Assert.False(HealthCheckEndpointPredicates.IsLive(ready));
    }

    [Fact]
    public void IsReady_ShouldOnlySelectDependencyChecks()
    {
        var live = CreateRegistration("api-self", ["live"]);
        var ready = CreateRegistration("sql-server", ["ready", "database"]);

        Assert.False(HealthCheckEndpointPredicates.IsReady(live));
        Assert.True(HealthCheckEndpointPredicates.IsReady(ready));
    }

    private static HealthCheckRegistration CreateRegistration(string name, string[] tags)
    {
        return new HealthCheckRegistration(
            name,
            new DelegateHealthCheck(_ => Task.FromResult(HealthCheckResult.Healthy())),
            failureStatus: null,
            tags);
    }

    private sealed class DelegateHealthCheck : IHealthCheck
    {
        private readonly Func<HealthCheckContext, Task<HealthCheckResult>> _check;

        public DelegateHealthCheck(Func<HealthCheckContext, Task<HealthCheckResult>> check)
        {
            _check = check;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return _check(context);
        }
    }
}
