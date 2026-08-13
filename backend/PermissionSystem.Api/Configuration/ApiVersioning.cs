using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace PermissionSystem.Api.Configuration;

public static class ApiVersioning
{
    public const string CurrentVersion = "v1";
    public const string VersionedPrefix = "api/v1/";
    public const string LegacyPrefix = "api/";

    public static bool IsVersionableRoute(string routeTemplate)
    {
        return routeTemplate.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase) &&
            !routeTemplate.StartsWith("api/health", StringComparison.OrdinalIgnoreCase) &&
            !routeTemplate.StartsWith("api/sso/oidc", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLegacyBusinessApiPath(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWithSegments("/api/sso/oidc", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ApiVersionRouteConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var versionedSelectors = controller.Selectors
                .Where(selector => selector.AttributeRouteModel?.Template is { } template &&
                    ApiVersioning.IsVersionableRoute(template))
                .ToArray();

            foreach (var selector in versionedSelectors)
            {
                var route = selector.AttributeRouteModel!;
                if (route.Template!.StartsWith(ApiVersioning.VersionedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var versionedSelector = new SelectorModel(selector)
                {
                    AttributeRouteModel = new AttributeRouteModel(route)
                    {
                        Template = $"{ApiVersioning.VersionedPrefix}{route.Template[ApiVersioning.LegacyPrefix.Length..]}"
                    }
                };

                controller.Selectors.Add(versionedSelector);
            }
        }
    }
}

public sealed class LegacyApiRouteDeprecationMiddleware
{
    private readonly RequestDelegate _next;

    public LegacyApiRouteDeprecationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ApiVersioning.IsLegacyBusinessApiPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        context.Response.Headers.TryAdd("Deprecation", "true");
        context.Response.Headers.TryAdd("Link", "</api/v1>; rel=\"successor-version\"");

        await _next(context);
    }
}
