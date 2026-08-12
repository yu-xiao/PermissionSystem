using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PermissionSystem.Api.HealthChecks;

public static class HealthCheckEndpointPredicates
{
    public static bool IsLive(HealthCheckRegistration registration)
    {
        return registration.Tags.Contains("live");
    }

    public static bool IsReady(HealthCheckRegistration registration)
    {
        return registration.Tags.Contains("ready");
    }
}
