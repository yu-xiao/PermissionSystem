namespace PermissionSystem.Application.Authentication;

public interface IUserCredentialValidator
{
    Task<AuthenticatedUser?> ValidateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
