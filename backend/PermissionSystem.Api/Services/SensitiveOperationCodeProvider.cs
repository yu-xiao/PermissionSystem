using PermissionSystem.Application.Security;

namespace PermissionSystem.Api.Services;

public sealed class SensitiveOperationCodeProvider : ISensitiveOperationCodeProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SensitiveOperationCodeProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? StepUpTicket =>
        _httpContextAccessor.HttpContext?.Request.Headers["X-Step-Up-Ticket"].FirstOrDefault();
}
