using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiFollowUpClarificationException(string message) : Exception(message);

public enum AiFollowUpChange { None, CurrentDepartment, PreviousCalendarMonth }

public interface IAiFollowUpContextService
{
    Task<AiStructuredResultPage> ReadAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<AiStructuredResult> ResolveAsync(Guid conversationId, AiContextReference reference, CancellationToken cancellationToken = default);
    Task<string> PrepareArgumentsAsync(Guid conversationId, string toolCode, string argumentsJson,
        AiContextReference? selectedReference, int? explicitUtcOffsetMinutes = null,
        AiFollowUpChange expectedChange = AiFollowUpChange.None, CancellationToken cancellationToken = default);
}

public sealed class AiFollowUpContextService(IAiStructuredResultReader reader, IAiQueryAccessGuard access) : IAiFollowUpContextService
{
    public Task<AiStructuredResultPage> ReadAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        reader.ReadAsync(conversationId, cancellationToken: cancellationToken);

    public async Task<AiStructuredResult> ResolveAsync(Guid conversationId, AiContextReference reference, CancellationToken cancellationToken = default)
    {
        if (reference.RunId == Guid.Empty || string.IsNullOrWhiteSpace(reference.InvocationId) || reference.InvocationId.Length > 100)
            throw new AiFollowUpClarificationException("追问引用无效，请重新选择明确的查询对象。");
        var page = await reader.ReadAsync(conversationId, reference.RunId, cancellationToken);
        return page.Results.SingleOrDefault(item => item.RunId == reference.RunId && item.InvocationId == reference.InvocationId)
            ?? throw new AiFollowUpClarificationException("原查询上下文已过期或不可读取，请重新明确查询对象和条件。");
    }

    public async Task<string> PrepareArgumentsAsync(Guid conversationId, string toolCode, string argumentsJson,
        AiContextReference? selectedReference, int? explicitUtcOffsetMinutes = null,
        AiFollowUpChange expectedChange = AiFollowUpChange.None, CancellationToken cancellationToken = default)
    {
        JsonObject patch;
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                document.RootElement.EnumerateObject().Count()) throw new JsonException();
            patch = JsonNode.Parse(argumentsJson)!.AsObject();
        }
        catch (JsonException exception) { throw new BusinessException(ErrorCode.ValidationFailed, "Invalid AI query arguments.", exception); }
        AiContextReference? reference = null;
        if (patch.TryGetPropertyValue("contextRef", out var node))
        {
            try { reference = node?.Deserialize<AiContextReference>(AiStructuredResults.JsonOptions) ?? throw new JsonException(); }
            catch (JsonException) { throw new AiFollowUpClarificationException("请提供有效的追问引用。"); }
            patch.Remove("contextRef");
        }
        if (selectedReference is not null && reference is not null && selectedReference != reference)
            throw new AiFollowUpClarificationException("追问对象与所选结果不一致，请重新选择。");
        reference ??= selectedReference;
        if (!AiStructuredResults.IsSupported(toolCode))
        {
            if (reference is not null) throw new AiFollowUpClarificationException("所选结果不能用于该工具，请明确查询对象。");
            return patch.ToJsonString(AiStructuredResults.JsonOptions);
        }
        var actor = await access.AuthorizeAsync(toolCode, cancellationToken);
        if (!PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(actor.Roles), actor.PermissionCodes, AiCenterConstants.ChatUsePermission))
            throw new BusinessException(ErrorCode.Forbidden, "AI chat is not authorized.");
        var allowed = AiStructuredResults.AllowedParameters(toolCode).Concat(["period", "utcOffsetMinutes"]).ToArray();
        if (patch.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal)))
            throw new BusinessException(ErrorCode.ValidationFailed, "Unknown AI query parameter.");
        var merged = new JsonObject();
        if (reference is not null)
        {
            var source = await ResolveAsync(conversationId, reference, cancellationToken);
            if (source.ToolCode != toolCode) throw new AiFollowUpClarificationException("所选上下文的结果类型不匹配，请明确查询对象。");
            foreach (var parameter in source.Context.Parameters) merged[parameter.Key] = JsonNode.Parse(parameter.Value.GetRawText());
        }
        if (toolCode == "permission.reports.query_dataset")
            return PrepareReportArguments(merged, patch, reference, explicitUtcOffsetMinutes, expectedChange);
        if (toolCode == DemoBusinessOrderQueryAiToolHandler.ToolCode)
            return PrepareDemoArguments(merged, patch, reference, expectedChange);
        if (toolCode == PermissionSystem.Application.AiKnowledge.AiKnowledgeContract.ToolCode)
        {
            if (expectedChange != AiFollowUpChange.None || patch.ContainsKey("period") || patch.ContainsKey("utcOffsetMinutes"))
                throw new AiFollowUpClarificationException("文档检索只支持关键词、文档和展示数量，请明确检索条件。");
            foreach (var pair in patch) merged[pair.Key] = pair.Value?.DeepClone();
            var request = merged.Deserialize<PermissionSystem.Application.AiKnowledge.AiKnowledgeSearchRequest>(AiStructuredResults.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.Keyword) || request.Keyword.Trim().Length > 100 ||
                request.Limit is < 1 or > 5 || request.DocumentId == Guid.Empty)
                throw new BusinessException(ErrorCode.ValidationFailed, "Invalid knowledge query parameters.");
            return JsonSerializer.Serialize(new { keyword = request.Keyword.Trim(), request.DocumentId, request.Limit }, AiStructuredResults.JsonOptions);
        }
        if (expectedChange != AiFollowUpChange.None)
        {
            if (reference is null || (expectedChange == AiFollowUpChange.CurrentDepartment && toolCode != "permission.users.search") ||
                (expectedChange == AiFollowUpChange.PreviousCalendarMonth && toolCode is not ("permission.login_logs.summary" or "permission.operation_logs.summary")))
                throw new AiFollowUpClarificationException("所选结果不支持该追问条件，请重新明确对象。");
            foreach (var pair in patch)
            {
                var expectedField = expectedChange == AiFollowUpChange.CurrentDepartment ? pair.Key == "departmentScope" : pair.Key is "period" or "utcOffsetMinutes";
                if (!expectedField && !JsonNode.DeepEquals(pair.Value, merged[pair.Key]))
                    throw new AiFollowUpClarificationException("追问只能修改指定的条件，请重新明确其他过滤变更。");
            }
            if (expectedChange == AiFollowUpChange.CurrentDepartment) patch["departmentScope"] = "CurrentDepartment";
            else
            {
                patch.Remove("startTime");
                patch.Remove("endTime");
                patch["period"] = "PreviousCalendarMonth";
            }
        }
        foreach (var pair in patch) merged[pair.Key] = pair.Value?.DeepClone();
        foreach (var pair in patch)
            if (pair.Value is null && pair.Key is not ("keyword" or "isEnabled" or "userName" or "module" or
                "targetUserId" or "menuId" or "permissionCode" or "utcOffsetMinutes"))
                throw new BusinessException(ErrorCode.ValidationFailed, "This query parameter cannot be cleared.");
        if (toolCode == "permission.diagnose" && patch.ContainsKey("kind"))
        {
            if (merged["kind"] is not JsonValue kindValue || !kindValue.TryGetValue<string>(out var kind))
                throw new BusinessException(ErrorCode.ValidationFailed, "Invalid diagnostic kind.");
            if (kind != "Menu" && !patch.ContainsKey("menuId")) merged.Remove("menuId");
            if (kind != "Permission" && !patch.ContainsKey("permissionCode")) merged.Remove("permissionCode");
        }
        if (toolCode == "permission.diagnose")
        {
            if (merged["kind"] is not JsonValue kindValue || !kindValue.TryGetValue<string>(out var kind))
                throw new AiFollowUpClarificationException("请明确要诊断菜单、权限要求还是数据范围。");
            if ((kind == "Menu" && merged["menuId"] is null) || (kind == "Permission" && merged["permissionCode"] is null))
                throw new AiFollowUpClarificationException("请提供明确的菜单 ID 或权限要求，不能根据历史文字猜选对象。");
        }
        if (patch.ContainsKey("period"))
        {
            if (!explicitUtcOffsetMinutes.HasValue)
                throw new AiFollowUpClarificationException("“上个月”需要明确时区，请在输入区选择自然月查询时区后再查询。");
            if (patch["utcOffsetMinutes"] is not null && patch["utcOffsetMinutes"]!.ToJsonString() != explicitUtcOffsetMinutes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new AiFollowUpClarificationException("模型请求的时区与用户选择不一致，请重新明确查询条件。");
            patch["utcOffsetMinutes"] = explicitUtcOffsetMinutes.Value;
        }
        NormalizeRelativePeriod(toolCode, merged, patch, DateTimeOffset.UtcNow);
        var content = merged.ToJsonString(AiStructuredResults.JsonOptions);
        if (Encoding.UTF8.GetByteCount(content) > AiStructuredResults.MaxContextBytes)
            throw new BusinessException(ErrorCode.ValidationFailed, "AI query parameters are too large.");
        return content;
    }

    private static string PrepareDemoArguments(JsonObject merged, JsonObject patch, AiContextReference? reference,
        AiFollowUpChange expectedChange)
    {
        if (patch.ContainsKey("period") || patch.ContainsKey("utcOffsetMinutes") || expectedChange == AiFollowUpChange.PreviousCalendarMonth)
            throw new AiFollowUpClarificationException("Demo 单据首批不支持时间筛选，请明确关键词、审批状态或部门条件。");
        if (expectedChange == AiFollowUpChange.CurrentDepartment)
        {
            if (reference is null) throw new AiFollowUpClarificationException("请先选择明确的 Demo 查询结果。");
            if (patch.Any(pair => pair.Key != "departmentScope" && !JsonNode.DeepEquals(pair.Value, merged[pair.Key])))
                throw new AiFollowUpClarificationException("本部门追问只能修改部门范围条件。");
            patch["departmentScope"] = "CurrentDepartment";
        }
        foreach (var pair in patch)
        {
            if (pair.Value is null && pair.Key is not ("keyword" or "approvalStatus" or "departmentId"))
                throw new BusinessException(ErrorCode.ValidationFailed, "This Demo query parameter cannot be cleared.");
            merged[pair.Key] = pair.Value?.DeepClone();
        }
        var effective = DemoBusinessOrderReadOnlyContract.Normalize(merged.Deserialize<DemoBusinessOrderReadOnlyQuery>(AiStructuredResults.JsonOptions)!);
        var content = JsonSerializer.Serialize(effective, AiStructuredResults.JsonOptions);
        if (Encoding.UTF8.GetByteCount(content) > AiStructuredResults.MaxContextBytes)
            throw new BusinessException(ErrorCode.ValidationFailed, "Demo query context is too large.");
        return content;
    }

    private static string PrepareReportArguments(JsonObject merged, JsonObject patch, AiContextReference? reference,
        int? explicitOffset, AiFollowUpChange expectedChange)
    {
        if (reference is not null && patch.ContainsKey("reportDefinitionId") &&
            !JsonNode.DeepEquals(patch["reportDefinitionId"], merged["reportDefinitionId"]))
            throw new AiFollowUpClarificationException("切换报表必须发起新查询，不能继承另一数据集的过滤条件。");
        var filters = merged["params"]?.DeepClone() as JsonObject ?? new();
        JsonObject changes = new();
        if (patch.TryGetPropertyValue("params", out var filterPatch))
        {
            if (filterPatch is not JsonObject filterObject) throw new BusinessException(ErrorCode.ValidationFailed, "Report filter changes must be an object.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in filterObject)
            {
                if (!ReportDatasetCapabilities.UserFilterCodes.Contains(pair.Key, StringComparer.Ordinal) || !seen.Add(pair.Key))
                    throw new BusinessException(ErrorCode.ValidationFailed, "Unknown or duplicate report filter.");
                changes[pair.Key] = pair.Value?.DeepClone();
            }
        }
        if (expectedChange != AiFollowUpChange.None)
        {
            if (reference is null) throw new AiFollowUpClarificationException("请先选择明确的报表查询结果。");
            foreach (var pair in patch)
            {
                if (pair.Key == "params") continue;
                if (expectedChange == AiFollowUpChange.PreviousCalendarMonth && pair.Key is "period" or "utcOffsetMinutes") continue;
                if (!JsonNode.DeepEquals(pair.Value, merged[pair.Key]))
                    throw new AiFollowUpClarificationException("追问只能修改指定条件，请明确其他变更。");
            }
            foreach (var pair in changes)
                if (!(expectedChange == AiFollowUpChange.CurrentDepartment && pair.Key == "departmentScope") &&
                    !JsonNode.DeepEquals(pair.Value, filters[pair.Key]))
                    throw new AiFollowUpClarificationException("追问不能静默改变其他报表过滤条件。");
            if (expectedChange == AiFollowUpChange.CurrentDepartment) changes["departmentScope"] = "CurrentDepartment";
            else patch["period"] = "PreviousCalendarMonth";
        }
        foreach (var pair in changes) filters[pair.Key] = pair.Value?.DeepClone();
        foreach (var pair in patch)
        {
            if (pair.Key == "params") continue;
            if (pair.Value is null) throw new BusinessException(ErrorCode.ValidationFailed, "This report option cannot be cleared.");
            merged[pair.Key] = pair.Value.DeepClone();
        }
        merged["params"] = filters;
        if (patch["mode"]?.ToJsonString() == "\"Rows\"" && !patch.ContainsKey("dimension")) merged["dimension"] = "None";
        if ((patch.ContainsKey("mode") || patch.ContainsKey("dimension")) && !patch.ContainsKey("sort")) merged.Remove("sort");
        if (patch.ContainsKey("period"))
        {
            if (!explicitOffset.HasValue || (patch["utcOffsetMinutes"] is not null &&
                patch["utcOffsetMinutes"]!.ToJsonString() != explicitOffset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                throw new AiFollowUpClarificationException("“上个月”需要用户明确选择时区，不能使用模型猜测的时区。");
            if (changes.ContainsKey("startTime") || changes.ContainsKey("endTime"))
                throw new BusinessException(ErrorCode.ValidationFailed, "Relative period cannot include explicit report time changes.");
            patch["utcOffsetMinutes"] = explicitOffset.Value;
        }
        NormalizeRelativePeriod("permission.reports.query_dataset", merged, patch, DateTimeOffset.UtcNow);
        var content = merged.ToJsonString(AiStructuredResults.JsonOptions);
        if (Encoding.UTF8.GetByteCount(content) > AiStructuredResults.MaxContextBytes)
            throw new BusinessException(ErrorCode.ValidationFailed, "Report query context is too large.");
        return content;
    }

    public static void NormalizeRelativePeriod(string toolCode, JsonObject merged, JsonObject patch, DateTimeOffset now)
    {
        if (!patch.ContainsKey("period") && !patch.ContainsKey("utcOffsetMinutes")) return;
        if (toolCode is not ("permission.login_logs.summary" or "permission.operation_logs.summary" or "permission.reports.query_dataset") ||
            patch["period"]?.ToJsonString() != "\"PreviousCalendarMonth\"" || patch.ContainsKey("startTime") || patch.ContainsKey("endTime"))
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid relative time query.");
        if (patch["utcOffsetMinutes"] is null)
            throw new AiFollowUpClarificationException("“上个月”需要明确时区或 UTC 偏移，请补充后再查询。");
        if (!int.TryParse(patch["utcOffsetMinutes"]!.ToJsonString(), out var minutes) || minutes is < -840 or > 840)
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid UTC offset.");
        var offset = TimeSpan.FromMinutes(minutes);
        var local = now.ToOffset(offset);
        var endExclusive = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, offset);
        var timeParameters = toolCode == "permission.reports.query_dataset"
            ? merged["params"] as JsonObject ?? throw new BusinessException(ErrorCode.ValidationFailed, "Report filters are missing.") : merged;
        timeParameters["startTime"] = JsonValue.Create(endExclusive.AddMonths(-1));
        timeParameters["endTime"] = JsonValue.Create(toolCode == "permission.reports.query_dataset" ? endExclusive : endExclusive.AddTicks(-1));
        merged.Remove("period");
        merged.Remove("utcOffsetMinutes");
    }

    public static string ExtendModelSchema(AiToolDefinition definition)
    {
        if (!AiStructuredResults.IsSupported(definition.ToolCode)) return definition.InputSchemaJson;
        var schema = JsonNode.Parse(definition.InputSchemaJson)!.AsObject();
        var properties = schema["properties"] as JsonObject ?? new JsonObject();
        schema["properties"] = properties;
        properties["contextRef"] = JsonNode.Parse("""{"type":"object","required":["runId","invocationId"],"properties":{"runId":{"type":"string","format":"uuid"},"invocationId":{"type":"string","minLength":1,"maxLength":100}},"additionalProperties":false}""");
        if (definition.ToolCode == "permission.users.search")
            properties["departmentScope"] = JsonNode.Parse("""{"type":"string","enum":["Authorized","CurrentDepartment"]}""");
        if (definition.ToolCode is "permission.login_logs.summary" or "permission.operation_logs.summary" or "permission.reports.query_dataset")
        {
            properties["period"] = JsonNode.Parse("""{"type":"string","enum":["PreviousCalendarMonth"]}""");
            properties["utcOffsetMinutes"] = JsonNode.Parse("""{"type":"integer","minimum":-840,"maximum":840}""");
        }
        foreach (var field in new[] { "keyword", "isEnabled", "userName", "module", "targetUserId", "menuId", "permissionCode" })
        {
            if (properties[field] is not JsonObject fieldSchema || fieldSchema["type"] is null) continue;
            fieldSchema["type"] = new JsonArray(fieldSchema["type"]!.DeepClone(), JsonValue.Create("null"));
        }
        if (definition.ToolCode == DemoBusinessOrderQueryAiToolHandler.ToolCode)
        {
            properties["keyword"]!["type"] = new JsonArray("string", "null");
            properties["approvalStatus"]!["type"] = new JsonArray("string", "null");
            properties["approvalStatus"]!["enum"]!.AsArray().Add((JsonNode?)null);
            properties["departmentId"]!["type"] = new JsonArray("string", "null");
        }
        schema.Remove("required");
        return schema.ToJsonString();
    }
}
