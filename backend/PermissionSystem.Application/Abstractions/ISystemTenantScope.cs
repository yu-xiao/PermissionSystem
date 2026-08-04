namespace PermissionSystem.Application.Abstractions;

public interface ISystemTenantScope
{
    IDisposable Begin(string operation);
}
