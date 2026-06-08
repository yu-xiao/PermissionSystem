namespace PermissionSystem.Domain.Enums;

public enum SsoLoginResult
{
    Success = 0,
    Failed = 1,
    UserDisabled = 2,
    TenantDisabled = 3,
    BindingFailed = 4,
    AutoCreateFailed = 5
}
