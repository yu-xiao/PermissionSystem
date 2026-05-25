using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.Tokens;

public sealed class OpenIddictTokenRevocationService : ITokenRevocationService
{
    private readonly IOpenIddictTokenManager _tokenManager;

    public OpenIddictTokenRevocationService(IOpenIddictTokenManager tokenManager)
    {
        _tokenManager = tokenManager;
    }

    public async Task RevokeRefreshTokenAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var token = await _tokenManager.FindByReferenceIdAsync(refreshToken, cancellationToken);
        if (token is not null)
        {
            await _tokenManager.TryRevokeAsync(token, cancellationToken);
        }
    }

    public async Task RevokeUserRefreshTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _tokenManager.RevokeAsync(
            userId.ToString(),
            client: null,
            status: null,
            type: "refresh_token",
            cancellationToken);
    }
}
