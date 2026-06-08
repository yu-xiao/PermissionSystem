using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Security;

public sealed class SecurityPolicyResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public int PasswordMinLength { get; init; }
    public bool RequireDigit { get; init; }
    public bool RequireUppercase { get; init; }
    public bool RequireLowercase { get; init; }
    public bool RequireSpecialChar { get; init; }
    public int PasswordExpireDays { get; init; }
    public int LoginFailureLockThreshold { get; init; }
    public int LoginFailureLockMinutes { get; init; }
    public bool EnableMfa { get; init; }
    public bool EnableSensitiveOperationVerify { get; init; }
    public bool EnableIpWhitelist { get; init; }
    public bool EnableIpBlacklist { get; init; }
}

public sealed class UpdateSecurityPolicyRequest
{
    public int PasswordMinLength { get; init; } = 8;
    public bool RequireDigit { get; init; } = true;
    public bool RequireUppercase { get; init; }
    public bool RequireLowercase { get; init; } = true;
    public bool RequireSpecialChar { get; init; }
    public int PasswordExpireDays { get; init; }
    public int LoginFailureLockThreshold { get; init; } = 5;
    public int LoginFailureLockMinutes { get; init; } = 15;
    public bool EnableMfa { get; init; }
    public bool EnableSensitiveOperationVerify { get; init; }
    public bool EnableIpWhitelist { get; init; }
    public bool EnableIpBlacklist { get; init; }
}

public sealed class SendSensitiveVerificationRequest
{
    public string OperationCode { get; init; } = string.Empty;
}

public sealed class SendSensitiveVerificationResponse
{
    public string OperationCode { get; init; } = string.Empty;
    public string? VerifyCode { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string DeliveryMessage { get; init; } = string.Empty;
}

public sealed class VerifySensitiveOperationRequest
{
    public string OperationCode { get; init; } = string.Empty;
    public string VerifyCode { get; init; } = string.Empty;
}

public sealed class IpAccessRuleQueryRequest : PaginationRequest
{
    public string? RuleType { get; init; }
    public string? Keyword { get; init; }
    public bool? IsEnabled { get; init; }
}

public sealed class CreateIpAccessRuleRequest
{
    public string RuleType { get; init; } = "Blacklist";
    public string IpPattern { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public sealed class UpdateIpAccessRuleRequest
{
    public string RuleType { get; init; } = "Blacklist";
    public string IpPattern { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public sealed class IpAccessRuleResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string RuleType { get; init; } = string.Empty;
    public string IpPattern { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class LoginFailureQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }
}

public sealed class LoginFailureRecordResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public int FailureCount { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }
    public DateTimeOffset LastFailureAt { get; init; }
}

public interface ISensitiveOperationCodeProvider
{
    string? VerificationCode { get; }
}

public interface ISecurityPolicyService
{
    Task<SecurityPolicyResponse> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<SecurityPolicyResponse> UpdatePolicyAsync(UpdateSecurityPolicyRequest request, CancellationToken cancellationToken = default);
    Task ValidatePasswordAsync(string password, CancellationToken cancellationToken = default);
    Task EnsureLoginAllowedAsync(string userName, string? ipAddress, CancellationToken cancellationToken = default);
    Task RecordLoginFailureAsync(Guid tenantId, string userName, string? ipAddress, CancellationToken cancellationToken = default);
    Task ClearLoginFailureAsync(Guid tenantId, string userName, string? ipAddress, CancellationToken cancellationToken = default);
    Task<SendSensitiveVerificationResponse> SendVerificationAsync(SendSensitiveVerificationRequest request, CancellationToken cancellationToken = default);
    Task VerifyAsync(VerifySensitiveOperationRequest request, CancellationToken cancellationToken = default);
    Task EnsureSensitiveOperationVerifiedAsync(string operationCode, CancellationToken cancellationToken = default);
    Task EnsureSensitiveOperationVerifiedAsync(string operationCode, bool force, CancellationToken cancellationToken = default);
    Task<bool> IsIpAllowedAsync(string? ipAddress, CancellationToken cancellationToken = default);
    Task<PagedResult<IpAccessRuleResponse>> GetIpRulesAsync(IpAccessRuleQueryRequest request, CancellationToken cancellationToken = default);
    Task<IpAccessRuleResponse> CreateIpRuleAsync(CreateIpAccessRuleRequest request, CancellationToken cancellationToken = default);
    Task<IpAccessRuleResponse> UpdateIpRuleAsync(Guid id, UpdateIpAccessRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<LoginFailureRecordResponse>> GetLoginFailuresAsync(LoginFailureQueryRequest request, CancellationToken cancellationToken = default);
}
