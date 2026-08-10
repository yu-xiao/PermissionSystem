namespace PermissionSystem.Domain.Common;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DataPermissionExemptAttribute : Attribute
{
    public DataPermissionExemptAttribute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A data permission exemption reason is required.", nameof(reason));
        }

        Reason = reason.Trim();
    }

    public string Reason { get; }
}
