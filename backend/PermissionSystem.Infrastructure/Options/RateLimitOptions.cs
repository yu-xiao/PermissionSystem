namespace PermissionSystem.Infrastructure.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public bool Enabled { get; init; } = true;

    public int GlobalPermitLimit { get; init; } = 120;

    public int GlobalWindowSeconds { get; init; } = 60;

    public int LoginPermitLimit { get; init; } = 5;

    public int LoginWindowSeconds { get; init; } = 60;

    public int RefreshTokenPermitLimit { get; init; } = 20;

    public int RefreshTokenWindowSeconds { get; init; } = 60;

    public int QueueLimit { get; init; } = 0;
}
