using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Http;
using PermissionSystem.Api.Configuration;
using PermissionSystem.Api.Controllers;

namespace PermissionSystem.UnitTests.Architecture;

public sealed class Ea030ApiAndModuleBoundaryTests
{
    [Fact]
    public void VersionableRoutes_ShouldUseApiV1Prefix_AndExcludeInfrastructureEndpoints()
    {
        Assert.True(ApiVersioning.IsVersionableRoute("api/users"));
        Assert.True(ApiVersioning.IsVersionableRoute("api/workflow/tasks"));
        Assert.False(ApiVersioning.IsVersionableRoute("api/health"));
        Assert.False(ApiVersioning.IsVersionableRoute("api/sso/oidc"));
    }

    [Fact]
    public void RouteConvention_ShouldAddVersionedSelector_AndKeepLegacySelector()
    {
        var controller = new ControllerModel(
            typeof(MeController).GetTypeInfo(),
            Array.Empty<object>());
        controller.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel
            {
                Template = "api/me"
            }
        });

        var application = new ApplicationModel();
        application.Controllers.Add(controller);
        new ApiVersionRouteConvention().Apply(application);

        var routes = controller.Selectors
            .Select(selector => selector.AttributeRouteModel?.Template)
            .Where(template => template is not null)
            .ToArray();

        Assert.Contains("api/me", routes);
        Assert.Contains("api/v1/me", routes);
    }

    [Fact]
    public void LayerAssemblies_ShouldRespectDependencyDirection()
    {
        var domainReferences = typeof(PermissionSystem.Domain.Common.BaseEntity).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var applicationReferences = typeof(PermissionSystem.Application.DependencyInjection).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("PermissionSystem.Api", domainReferences);
        Assert.DoesNotContain("PermissionSystem.Infrastructure", domainReferences);
        Assert.DoesNotContain("PermissionSystem.Api", applicationReferences);
        Assert.DoesNotContain("PermissionSystem.Infrastructure", applicationReferences);
    }

    [Fact]
    public async Task LegacyApiRouteDeprecationMiddleware_ShouldAdvertiseSuccessorVersion()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/me";
        var middleware = new LegacyApiRouteDeprecationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Equal("true", context.Response.Headers["Deprecation"].ToString());
        Assert.Equal("</api/v1>; rel=\"successor-version\"", context.Response.Headers["Link"].ToString());
    }
}
