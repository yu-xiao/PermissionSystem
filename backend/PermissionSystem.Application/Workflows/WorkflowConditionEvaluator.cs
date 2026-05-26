using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowConditionEvaluator : IWorkflowConditionEvaluator
{
    public bool Evaluate(string? expressionJson, string? formDataJson)
    {
        if (string.IsNullOrWhiteSpace(expressionJson))
        {
            return false;
        }

        try
        {
            var expression = JsonNode.Parse(expressionJson);
            var formData = string.IsNullOrWhiteSpace(formDataJson)
                ? new JsonObject()
                : JsonNode.Parse(formDataJson) ?? new JsonObject();

            return expression is not null && EvaluateNode(expression, formData);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool EvaluateNode(JsonNode node, JsonNode formData)
    {
        if (node is not JsonObject obj)
        {
            return false;
        }

        if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray children)
        {
            var logic = GetString(obj, "logic") ?? "AND";
            var results = children
                .Where(child => child is not null)
                .Select(child => EvaluateNode(child!, formData))
                .ToList();

            return string.Equals(logic, "OR", StringComparison.OrdinalIgnoreCase)
                ? results.Any(result => result)
                : results.All(result => result);
        }

        var field = GetString(obj, "field");
        var op = GetString(obj, "operator") ?? GetString(obj, "op");
        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
        {
            return false;
        }

        var actual = ResolveValue(formData, field);
        obj.TryGetPropertyValue("value", out var expected);
        return EvaluateCondition(actual, op, expected);
    }

    private static bool EvaluateCondition(JsonNode? actual, string op, JsonNode? expected)
    {
        var normalizedOperator = NormalizeOperator(op);
        if (normalizedOperator is "contains")
        {
            return Contains(actual, expected);
        }

        if (normalizedOperator is "in")
        {
            return In(actual, expected);
        }

        var comparison = Compare(actual, expected);
        return normalizedOperator switch
        {
            "=" => comparison == 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            _ => false
        };
    }

    private static string NormalizeOperator(string op)
    {
        return op.Trim().ToLowerInvariant() switch
        {
            "equals" or "eq" => "=",
            "notequals" or "neq" => "!=",
            "greaterthan" or "gt" => ">",
            "greaterthanorequal" or "gte" => ">=",
            "lessthan" or "lt" => "<",
            "lessthanorequal" or "lte" => "<=",
            "contains" => "contains",
            "in" => "in",
            _ => op.Trim().ToLowerInvariant()
        };
    }

    private static int Compare(JsonNode? actual, JsonNode? expected)
    {
        if (TryGetDecimal(actual, out var actualNumber) && TryGetDecimal(expected, out var expectedNumber))
        {
            return actualNumber.CompareTo(expectedNumber);
        }

        if (TryGetDateTime(actual, out var actualDate) && TryGetDateTime(expected, out var expectedDate))
        {
            return actualDate.CompareTo(expectedDate);
        }

        return string.Compare(
            ToScalarString(actual),
            ToScalarString(expected),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(JsonNode? actual, JsonNode? expected)
    {
        var expectedValue = ToScalarString(expected);
        if (actual is JsonArray actualArray)
        {
            return actualArray.Any(item => string.Equals(ToScalarString(item), expectedValue, StringComparison.OrdinalIgnoreCase));
        }

        return ToScalarString(actual).Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool In(JsonNode? actual, JsonNode? expected)
    {
        var actualValue = ToScalarString(actual);
        if (expected is JsonArray expectedArray)
        {
            return expectedArray.Any(item => string.Equals(ToScalarString(item), actualValue, StringComparison.OrdinalIgnoreCase));
        }

        return string.Equals(actualValue, ToScalarString(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static JsonNode? ResolveValue(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static string? GetString(JsonObject obj, string propertyName)
    {
        return obj.TryGetPropertyValue(propertyName, out var node)
            ? ToScalarString(node)
            : null;
    }

    private static bool TryGetDecimal(JsonNode? node, out decimal value)
    {
        return decimal.TryParse(ToScalarString(node), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDateTime(JsonNode? node, out DateTimeOffset value)
    {
        return DateTimeOffset.TryParse(ToScalarString(node), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }

    private static string ToScalarString(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            return value.TryGetValue<string>(out var stringValue)
                ? stringValue
                : value.ToString();
        }

        return node.ToJsonString();
    }
}
