using PermissionSystem.Application.Security;

namespace PermissionSystem.Api.Services;

public sealed class SensitiveOperationCodeProvider : ISensitiveOperationCodeProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SensitiveOperationCodeProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? VerificationCode =>
        _httpContextAccessor.HttpContext?.Request.Headers["X-Sensitive-Verification-Code"].FirstOrDefault() ??
        _httpContextAccessor.HttpContext?.Request.Query["verificationCode"].FirstOrDefault();
}
