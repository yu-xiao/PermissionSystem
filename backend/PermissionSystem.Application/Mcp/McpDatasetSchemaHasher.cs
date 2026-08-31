using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PermissionSystem.Application.Mcp;

public static class McpDatasetSchemaHasher
{
    public static string Compute(McpDatasetTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var canonical = new StringBuilder();
        Append(canonical, template.DatasetCode);
        Append(canonical, template.DatasetName);
        Append(canonical, template.Version);
        Append(canonical, template.Description);
        Append(canonical, template.DataClassification);
        Append(canonical, template.HandlerCode);
        Append(canonical, template.MaxRows.ToString(CultureInfo.InvariantCulture));

        foreach (var field in template.Fields.OrderBy(field => field.FieldCode, StringComparer.Ordinal))
        {
            Append(canonical, field.FieldCode);
            Append(canonical, field.DisplayName);
            Append(canonical, field.DataType);
            Append(canonical, field.DataClassification);
            Append(canonical, field.IsFilterable ? "1" : "0");
            Append(canonical, field.IsDefault ? "1" : "0");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder builder, string? value)
    {
        var normalized = value ?? string.Empty;
        builder
            .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(normalized)
            .Append('|');
    }
}
