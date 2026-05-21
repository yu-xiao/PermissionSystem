namespace PermissionSystem.Application.Excels;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ExcelColumnAttribute : Attribute
{
    public ExcelColumnAttribute(string header)
    {
        Header = header;
    }

    public string Header { get; }

    public int Order { get; init; }

    public bool Required { get; init; }
}
