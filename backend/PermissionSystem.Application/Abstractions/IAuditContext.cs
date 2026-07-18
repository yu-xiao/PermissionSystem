namespace PermissionSystem.Application.Abstractions;

public interface IAuditContext
{
    Guid? UserId { get; }
}

public sealed class NullAuditContext : IAuditContext
{
    public Guid? UserId => null;
}
