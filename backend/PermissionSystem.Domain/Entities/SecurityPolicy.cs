using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SecurityPolicy : BaseEntity
{
    public int PasswordMinLength { get; set; } = 8;

    public bool RequireDigit { get; set; } = true;

    public bool RequireUppercase { get; set; }

    public bool RequireLowercase { get; set; } = true;

    public bool RequireSpecialChar { get; set; }

    public int PasswordExpireDays { get; set; }

    public int LoginFailureLockThreshold { get; set; } = 5;

    public int LoginFailureLockMinutes { get; set; } = 15;

    public bool EnableMfa { get; set; }

    public bool EnableSensitiveOperationVerify { get; set; }

    public bool EnableIpWhitelist { get; set; }

    public bool EnableIpBlacklist { get; set; }
}
