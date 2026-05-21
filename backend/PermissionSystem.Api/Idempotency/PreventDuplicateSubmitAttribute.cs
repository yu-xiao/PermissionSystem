namespace PermissionSystem.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PreventDuplicateSubmitAttribute : Attribute
{
    public int LockSeconds { get; init; } = 3;
}
