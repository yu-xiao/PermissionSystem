using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PermissionSystem.Application.PrintTemplates;

public static partial class PrintTemplateRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Render(string template, JsonElement? data)
    {
        var model = data.HasValue && data.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? data.Value
            : BuildDefaultModel();

        var html = LoopRegex().Replace(template, match =>
        {
            var collectionName = match.Groups["name"].Value;
            var body = match.Groups["body"].Value;
            if (!TryGetProperty(model, collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            return string.Concat(collection.EnumerateArray().Select(item => ReplaceVariables(body, item)));
        });

        return ReplaceVariables(html, model);
    }

    private static string ReplaceVariables(string template, JsonElement model)
    {
        return VariableRegex().Replace(template, match =>
        {
            var variableName = match.Groups["name"].Value;
            return TryGetProperty(model, variableName, out var value)
                ? WebUtility.HtmlEncode(ToDisplayValue(value))
                : string.Empty;
        });
    }

    private static bool TryGetProperty(JsonElement model, string name, out JsonElement value)
    {
        value = default;
        if (model.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in model.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string ToDisplayValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static JsonElement BuildDefaultModel()
    {
        return JsonSerializer.SerializeToElement(new
        {
            OrderNo = "PO202605260001",
            CreatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            ApplicantName = "Admin",
            Amount = 1234.56m,
            items = new[]
            {
                new { Name = "Sample Item A", Qty = 2, Price = 100 },
                new { Name = "Sample Item B", Qty = 1, Price = 200 }
            }
        }, JsonOptions);
    }

    [GeneratedRegex(@"{{#(?<name>[A-Za-z0-9_.-]+)}}(?<body>[\s\S]*?){{/\k<name>}}", RegexOptions.Compiled)]
    private static partial Regex LoopRegex();

    [GeneratedRegex(@"{{\s*(?<name>[A-Za-z0-9_.-]+)\s*}}", RegexOptions.Compiled)]
    private static partial Regex VariableRegex();
}
