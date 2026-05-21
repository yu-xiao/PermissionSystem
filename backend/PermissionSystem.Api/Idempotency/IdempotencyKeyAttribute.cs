namespace PermissionSystem.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class IdempotencyKeyAttribute : Attribute
{
    public int ExpirationSeconds { get; init; } = 600;
}
