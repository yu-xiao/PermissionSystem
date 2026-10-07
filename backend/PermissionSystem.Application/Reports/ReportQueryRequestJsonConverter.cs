using System.Text.Json;
using System.Text.Json.Serialization;

namespace PermissionSystem.Application.Reports;

public sealed class ReportQueryRequestJsonConverter : JsonConverter<ReportQueryRequest>
{
    public override ReportQueryRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Report query must be an object.");
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!new[] { "params", "mode", "dimension", "sort", "limit" }.Contains(property.Name, StringComparer.OrdinalIgnoreCase) ||
                !properties.TryAdd(property.Name, property.Value)) throw new JsonException("Unknown or duplicate report option.");
        }
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (properties.TryGetValue("params", out var filters))
        {
            if (filters.ValueKind != JsonValueKind.Object) throw new JsonException("Report params must be an object.");
            foreach (var property in filters.EnumerateObject())
                if (!parameters.TryAdd(property.Name, property.Value.Clone())) throw new JsonException("Duplicate report filter.");
        }
        string StringOption(string name, string fallback) => !properties.TryGetValue(name, out var value) ? fallback :
            value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new JsonException("Report option must be a string.");
        return new()
        {
            Params = parameters, Mode = StringOption("mode", "Rows"), Dimension = StringOption("dimension", "None"),
            Sort = properties.ContainsKey("sort") ? StringOption("sort", "") : null,
            Limit = properties.TryGetValue("limit", out var limit) ? limit.ValueKind == JsonValueKind.Number && limit.TryGetInt32(out var number)
                ? number : throw new JsonException("Report limit must be an integer.") : null
        };
    }

    public override void Write(Utf8JsonWriter writer, ReportQueryRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("params"); JsonSerializer.Serialize(writer, value.Params, options);
        writer.WriteString("mode", value.Mode); writer.WriteString("dimension", value.Dimension);
        if (value.Sort is not null) writer.WriteString("sort", value.Sort);
        if (value.Limit.HasValue) writer.WriteNumber("limit", value.Limit.Value);
        writer.WriteEndObject();
    }
}
