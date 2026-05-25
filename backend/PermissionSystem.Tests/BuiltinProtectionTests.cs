using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Tests;

public sealed class BuiltinProtectionTests
{
    [Fact]
    public void BuiltinConstants_ShouldMatchSeededAdminAndSuperAdmin()
    {
        Assert.Equal("admin", SystemBuiltinConstants.AdminUserName);
        Assert.Equal("ADMIN", SystemBuiltinConstants.AdminNormalizedUserName);
        Assert.Equal("SuperAdmin", SystemBuiltinConstants.SuperAdminRoleCode);
        Assert.Equal(SystemBuiltinConstants.SuperAdminRoleCode, ClaimConstants.SuperAdminRoleCode);
    }

    [Fact]
    public void UserAndRole_ShouldDefaultToNotBuiltin()
    {
        Assert.False(new User().IsBuiltin);
        Assert.False(new Role().IsBuiltin);
    }

    [Fact]
    public void SeededAdminShape_ShouldBeRecognizableAsBuiltinTarget()
    {
        var admin = new User
        {
            UserName = SystemBuiltinConstants.AdminUserName,
            NormalizedUserName = SystemBuiltinConstants.AdminNormalizedUserName,
            IsBuiltin = true
        };

        Assert.True(admin.IsBuiltin);
        Assert.Equal("admin", admin.UserName);
        Assert.Equal("ADMIN", admin.NormalizedUserName);
    }

    [Fact]
    public void SeededSuperAdminShape_ShouldBeRecognizableAsBuiltinTarget()
    {
        var role = new Role
        {
            Code = SystemBuiltinConstants.SuperAdminRoleCode,
            Name = SystemBuiltinConstants.SuperAdminRoleName,
            IsBuiltin = true
        };

        Assert.True(role.IsBuiltin);
        Assert.Equal(ClaimConstants.SuperAdminRoleCode, role.Code);
    }
}
