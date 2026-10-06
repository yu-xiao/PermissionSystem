using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PermissionSystem.AiEvaluations;

public static class EvaluationRedactor
{
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey", "apiKeyEncrypted", "authorization", "password", "passwordHash", "securityStamp", "sessionId",
        "token", "accessToken", "refreshToken", "cookie", "connectionString", "clientSecret", "email", "phoneNumber", "ipAddress", "userAgent"
    };
    private static readonly Regex SensitiveText = new(@"(?i)(?:Bearer\s+[\w.\-]+|(?:api[_-]?key|password|client[_-]?secret|access[_-]?token)\s*[:=]\s*[^\s,;""}]+|https?://[^\s""<>]+|\b[\w.+-]+@[\w.-]+\.[a-z]{2,}\b)", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static (string Json, bool Changed) Sanitize<T>(T value, string? secret = null)
    {
        var node = JsonSerializer.SerializeToNode(value, EvaluationJson.Options)!;
        var changed = false;
        string Text(string text)
        {
            var safe = secret is { Length: > 0 } ? text.Replace(secret, "[redacted]", StringComparison.Ordinal) : text;
            safe = SensitiveText.Replace(safe, "[redacted]");
            if (safe.Length > 16384) safe = safe[..16384] + "[truncated]";
            changed |= safe != text;
            return safe;
        }
        void Walk(JsonNode? current)
        {
            if (current is JsonObject obj)
                foreach (var pair in obj.ToArray())
                {
                    if (SensitiveFields.Contains(pair.Key)) { obj[pair.Key] = "[redacted]"; changed = true; }
                    else if (pair.Value is JsonValue val && val.TryGetValue<string>(out var text)) obj[pair.Key] = Text(text);
                    else Walk(pair.Value);
                }
            else if (current is JsonArray array)
                for (var i = 0; i < array.Count; i++)
                    if (array[i] is JsonValue val && val.TryGetValue<string>(out var text)) array[i] = Text(text);
                    else Walk(array[i]);
        }
        Walk(node);
        return (node.ToJsonString(EvaluationJson.Options), changed);
    }

    public static string DeterministicDigest(EvaluationReport report)
    {
        var node = JsonSerializer.SerializeToNode(report.Results, EvaluationJson.Options)!;
        var volatileFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "runId", "invocationId", "queriedAt", "evaluatedAt", "createdAt", "asOf", "durationMilliseconds", "dataScopeFingerprint",
          "reservedTokens", "accountedCost", "estimatedInputTokens", "estimatedOutputTokens", "estimatedCost" };
        void Walk(JsonNode? value)
        {
            if (value is JsonObject obj)
                foreach (var pair in obj.ToArray())
                    if (volatileFields.Contains(pair.Key)) obj.Remove(pair.Key); else Walk(pair.Value);
            else if (value is JsonArray array) foreach (var item in array) Walk(item);
        }
        Walk(node);
        return EvaluationJson.Digest(node.ToJsonString(EvaluationJson.Options));
    }
}
