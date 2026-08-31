using Microsoft.Extensions.Primitives;
using PermissionSystem.McpServer.Configuration;

namespace PermissionSystem.McpServer.Middlewares;

internal sealed class McpResourceMetadataChallengeMiddleware
{
    private readonly RequestDelegate _next;

    public McpResourceMetadataChallengeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, McpProtectedResourceMetadata metadata)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                AppendChallenge(context.Response.Headers, metadata.ResourceMetadataUrl);
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }

    internal static void AppendChallenge(IHeaderDictionary headers, string resourceMetadataUrl)
    {
        var values = headers.WWWAuthenticate.ToArray();
        if (values.Any(value =>
                value?.Contains("resource_metadata=", StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

        var resourceParameter = $"resource_metadata=\"{resourceMetadataUrl}\"";
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index]?.Trim() ?? string.Empty;
            if (string.Equals(value, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                values[index] = $"Bearer {resourceParameter}";
                headers.WWWAuthenticate = new StringValues(values);
                return;
            }

            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                values[index] = $"{value}, {resourceParameter}";
                headers.WWWAuthenticate = new StringValues(values);
                return;
            }
        }

        headers.Append("WWW-Authenticate", new StringValues($"Bearer {resourceParameter}"));
    }
}
