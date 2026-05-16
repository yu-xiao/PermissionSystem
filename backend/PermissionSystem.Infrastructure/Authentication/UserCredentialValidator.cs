using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Authentication;

public sealed class UserCredentialValidator : IUserCredentialValidator
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserCredentialValidator(AppDbContext dbContext, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
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

        var user = await _dbContext.Users
            .AsSplitQuery()
            .Include(entity => entity.UserRoles)
                .ThenInclude(entity => entity.Role)
                    .ThenInclude(entity => entity.RolePermissions)
                        .ThenInclude(entity => entity.Permission)
            .FirstOrDefaultAsync(
                entity => entity.NormalizedUserName == normalizedUserName && entity.IsEnabled,
                cancellationToken);

        if (user is null)
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
            roles,
            permissionCodes);
    }
}
