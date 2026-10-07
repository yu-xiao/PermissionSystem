using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Api.Authorization;

public sealed class AiDocumentDraftAccessAttribute : TypeFilterAttribute
{
    public AiDocumentDraftAccessAttribute(bool requireExecutionPermission = false)
        : base(typeof(AiDocumentDraftAccessFilter))
    {
        Arguments = [requireExecutionPermission];
        Order = -2100;
    }
}

public sealed class AiDocumentDraftAccessFilter(
    bool requireExecutionPermission,
    IAiDocumentDraftService draftService,
    IAiDocumentExecutionService executionService) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!Guid.TryParse(context.RouteData.Values["id"]?.ToString(), out var id))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "A valid AI draft identifier is required.");
        }

        if (requireExecutionPermission)
        {
            await executionService.EnsureAccessAsync(id, context.HttpContext.RequestAborted);
        }
        else
        {
            _ = await draftService.GetByIdAsync(id, context.HttpContext.RequestAborted);
        }

        await next();
    }
}
