namespace PermissionSystem.Application.Abstractions;

public interface ITokenRevocationService
{
    Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken = default);

    Task RevokeUserRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
