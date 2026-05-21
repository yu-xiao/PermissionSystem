namespace PermissionSystem.Application.Abstractions;

public interface IConfigValueProtector
{
    string Protect(string value);

    string Unprotect(string protectedValue);
}
