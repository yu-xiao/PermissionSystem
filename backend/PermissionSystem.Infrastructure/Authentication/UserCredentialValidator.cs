using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Authentication;

public sealed class UserCredentialValidator : IUserCredentialValidator
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITenantContext? _tenantContext;

    public UserCredentialValidator(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        ITenantContext? tenantContext = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
    }

    public async Task<Guid?> ResolveActiveTenantIdAsync(
        string tenantCodeOrId,
        CancellationToken cancellationToken = default)
    {
        var value = tenantCodeOrId?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var tenantId = Guid.TryParse(value, out var parsedTenantId) ? parsedTenantId : (Guid?)null;
        return await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => !entity.IsDeleted && entity.Status == TenantStatus.Active)
            .Where(entity => tenantId.HasValue ? entity.Id == tenantId.Value : entity.Code == value)
            .Select(entity => (Guid?)entity.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AuthenticatedUser?> ValidateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUserName = username.Trim().ToUpperInvariant();
        var tenantId = _tenantContext?.TenantId;

        var user = await _dbContext.Users
            .AsSplitQuery()
            .Include(entity => entity.UserRoles)
                .ThenInclude(entity => entity.Role)
                    .ThenInclude(entity => entity.RolePermissions)
                        .ThenInclude(entity => entity.Permission)
            .FirstOrDefaultAsync(
                entity => entity.NormalizedUserName == normalizedUserName &&
                    entity.IsEnabled &&
                    (!tenantId.HasValue || entity.TenantId == tenantId.Value),
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!await _dbContext.Tenants.IgnoreQueryFilters().AnyAsync(
            entity => !entity.IsDeleted && entity.Id == user.TenantId && entity.Status == TenantStatus.Active,
            cancellationToken))
        {
            return null;
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var roles = user.UserRoles
            .Select(entity => entity.Role)
            .Where(role => role.IsEnabled)
            .Select(role => role.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissionCodes = user.UserRoles
            .Select(entity => entity.Role)
            .Where(role => role.IsEnabled)
            .SelectMany(role => role.RolePermissions)
            .Select(entity => entity.Permission.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AuthenticatedUser(
            user.Id,
            user.UserName,
            user.TenantId,
            user.DepartmentId,
            user.SecurityStamp,
            roles,
            permissionCodes);
    }

    public async Task<AuthenticatedUser?> GetAuthenticationStateAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        var user = await (
                from currentUser in _dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                join tenant in _dbContext.Tenants.IgnoreQueryFilters().AsNoTracking()
                    on currentUser.TenantId equals tenant.Id
                where currentUser.Id == userId &&
                    currentUser.TenantId == tenantId &&
                    !currentUser.IsDeleted &&
                    currentUser.IsEnabled &&
                    tenant.Id == tenantId &&
                    tenant.TenantId == tenantId &&
                    !tenant.IsDeleted &&
                    tenant.Status == TenantStatus.Active
                select new
                {
                    currentUser.Id,
                    currentUser.UserName,
                    currentUser.TenantId,
                    currentUser.DepartmentId,
                    currentUser.SecurityStamp
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roleCodes = await (
                from userRole in _dbContext.UserRoles.IgnoreQueryFilters().AsNoTracking()
                join role in _dbContext.Roles.IgnoreQueryFilters().AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userRole.UserId == userId &&
                    userRole.TenantId == tenantId &&
                    !userRole.IsDeleted &&
                    role.TenantId == tenantId &&
                    !role.IsDeleted &&
                    role.IsEnabled
                select role.Code)
            .ToArrayAsync(cancellationToken);

        var permissionCodes = await (
                from userRole in _dbContext.UserRoles.IgnoreQueryFilters().AsNoTracking()
                join role in _dbContext.Roles.IgnoreQueryFilters().AsNoTracking()
                    on userRole.RoleId equals role.Id
                join rolePermission in _dbContext.RolePermissions.IgnoreQueryFilters().AsNoTracking()
                    on role.Id equals rolePermission.RoleId
                join permission in _dbContext.Permissions.IgnoreQueryFilters().AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where userRole.UserId == userId &&
                    userRole.TenantId == tenantId &&
                    !userRole.IsDeleted &&
                    role.TenantId == tenantId &&
                    !role.IsDeleted &&
                    role.IsEnabled &&
                    rolePermission.TenantId == tenantId &&
                    !rolePermission.IsDeleted &&
                    permission.TenantId == tenantId &&
                    !permission.IsDeleted &&
                    !string.IsNullOrWhiteSpace(permission.Code)
                select permission.Code)
            .ToArrayAsync(cancellationToken);

        return new AuthenticatedUser(
            user.Id,
            user.UserName,
            user.TenantId,
            user.DepartmentId,
            user.SecurityStamp,
            roleCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            permissionCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
