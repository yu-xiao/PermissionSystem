using Microsoft.AspNetCore.Identity;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Authentication;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public PasswordHashService(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new User(), password);
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        return _passwordHasher.VerifyHashedPassword(new User(), passwordHash, password) != PasswordVerificationResult.Failed;
    }
}
