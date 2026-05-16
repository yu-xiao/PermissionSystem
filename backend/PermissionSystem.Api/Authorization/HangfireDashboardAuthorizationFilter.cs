using Hangfire.Dashboard;

namespace PermissionSystem.Api.Authorization;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IHostEnvironment _environment;

    public HangfireDashboardAuthorizationFilter(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public bool Authorize(DashboardContext context)
    {
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Docker"))
        {
            return true;
        }

        var httpContext = context.GetHttpContext();

        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
