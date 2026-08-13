using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PermissionSystem.Api.Configuration;

public sealed class ApiVersionOpenApiDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var path in swaggerDoc.Paths.Keys
                     .Where(path => path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
                         !path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            swaggerDoc.Paths.Remove(path);
        }
    }
}

