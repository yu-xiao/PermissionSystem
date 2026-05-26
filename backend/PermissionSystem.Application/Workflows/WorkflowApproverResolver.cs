using System.Text.Json;
using System.Text.Json.Nodes;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowApproverResolver : IWorkflowApproverResolver
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserRole> _userRoleRepository;

    public WorkflowApproverResolver(
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
    }

    public IReadOnlyList<Guid> ResolveApproverUserIds(
        WorkflowNode node,
        WorkflowInstance instance,
        string? formDataJson)
    {
        var userIds = node.ApproverType switch
        {
            WorkflowApproverType.Users => ResolveUsers(node.ApproverIds),
            WorkflowApproverType.Roles => ResolveRoleUsers(instance.TenantId, node.ApproverIds),
            WorkflowApproverType.Initiator => [instance.StarterUserId],
            WorkflowApproverType.FormFieldUser => ResolveFormFieldUsers(node.ApproverIds, formDataJson),
            WorkflowApproverType.DepartmentManager => [],
            WorkflowApproverType.InitiatorDirectLeader => [],
            WorkflowApproverType.InitiatorDepartmentManager => [],
            WorkflowApproverType.Positions => [],
            _ => []
        };

        var distinctIds = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();

        return _userRepository.Query()
            .Where(entity => entity.TenantId == instance.TenantId && entity.IsEnabled && distinctIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray();
    }

    private static IReadOnlyCollection<Guid> ResolveUsers(string? approverIds)
    {
        return ParseGuidList(approverIds);
    }

    private IReadOnlyCollection<Guid> ResolveRoleUsers(Guid tenantId, string? approverIds)
    {
        var roleIds = ParseGuidList(approverIds);
        if (roleIds.Count == 0)
        {
            return [];
        }

        return _userRoleRepository.Query()
            .Where(entity => entity.TenantId == tenantId && roleIds.Contains(entity.RoleId))
            .Select(entity => entity.UserId)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyCollection<Guid> ResolveFormFieldUsers(string? fieldExpression, string? formDataJson)
    {
        if (string.IsNullOrWhiteSpace(fieldExpression) || string.IsNullOrWhiteSpace(formDataJson))
        {
            return [];
        }

        try
        {
            var formData = JsonNode.Parse(formDataJson);
            if (formData is null)
            {
                return [];
            }

            var userIds = new List<Guid>();
            foreach (var fieldPath in SplitTokens(fieldExpression))
            {
                CollectGuidValues(ResolveValue(formData, fieldPath), userIds);
            }

            return userIds;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void CollectGuidValues(JsonNode? node, List<Guid> userIds)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                CollectGuidValues(item, userIds);
            }

            return;
        }

        if (Guid.TryParse(ToScalarString(node), out var userId))
        {
            userIds.Add(userId);
        }
    }

    private static IReadOnlyCollection<Guid> ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(value);
            if (node is JsonArray array)
            {
                return array
                    .Select(ToScalarString)
                    .Where(item => Guid.TryParse(item, out _))
                    .Select(Guid.Parse)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
        }

        return SplitTokens(value)
            .Where(item => Guid.TryParse(item, out _))
            .Select(Guid.Parse)
            .ToArray();
    }

    private static IEnumerable<string> SplitTokens(string value)
    {
        return value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
