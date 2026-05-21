namespace PermissionSystem.Api.RateLimiting;

public static class RateLimitPolicyNames
{
    public const string Token = "token";
}

public static class RateLimitMetadataKeys
{
    public const string GrantType = "RateLimit:GrantType";

    public const string ClientId = "RateLimit:ClientId";
}
