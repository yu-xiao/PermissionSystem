using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiActions;

public sealed class AiBusinessActionAccessPolicy(
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IAiCenterConfiguration configuration)
{
    public (Guid UserId, Guid TenantId) EnsureIdentity()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null || currentUser.UserId == Guid.Empty ||
            currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "A valid user and tenant context is required.");
        }

        if (!HasIdentity())
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to use AI actions.");
        }

        return (currentUser.UserId.Value, currentUser.TenantId.Value);
    }

    public bool CanPrepare(AiBusinessActionDefinition definition) =>
        HasIdentity() &&
        currentUser.HasPermission(AiCenterConstants.DocumentDraftPermission) &&
        HasBusinessPermissions(definition.ToolDefinition.RequiredPermissions) &&
        definition.ToolDefinition.RequiredPermissions.All(currentUser.HasPermission);

    public bool CanExecute(AiBusinessActionDefinition definition) =>
        definition.SupportsExecution && CanPrepare(definition) &&
        definition.RequiredExecutionPermissions.Contains(AiCenterConstants.DocumentExecutePermission, StringComparer.Ordinal) &&
        definition.RequiredExecutionPermissions.All(currentUser.HasPermission);

    public (Guid UserId, Guid TenantId) EnsurePrepare(AiBusinessActionDefinition definition)
    {
        var identity = EnsureIdentity();
        if (!CanPrepare(definition))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The requested AI action is not available.");
        }

        return identity;
    }

    public void EnsureExecute(AiBusinessActionDefinition definition)
    {
        EnsureIdentity();
        if (!CanExecute(definition))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current user is not allowed to execute this AI action.");
        }
    }

    public static void ValidateDefinition(AiBusinessActionDefinition definition)
    {
        var tool = definition.ToolDefinition;
        if (string.IsNullOrWhiteSpace(definition.BusinessType) || definition.BusinessType.Length > 100 ||
            string.IsNullOrWhiteSpace(definition.HandlerVersion) || definition.HandlerVersion.Length > 64 ||
            definition.HandlerVersion != tool.Version ||
            string.IsNullOrWhiteSpace(tool.ToolCode) || tool.ToolCode.Length > 200 ||
            string.IsNullOrWhiteSpace(tool.FunctionName) || tool.FunctionName.Length > 64 ||
            tool.FunctionName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-') ||
            string.IsNullOrWhiteSpace(tool.Description) || string.IsNullOrWhiteSpace(tool.DataClassification) ||
            tool.DataScopePolicy != AiToolDataScopePolicies.ActorOwnedDraft ||
            !IsJsonObject(tool.InputSchemaJson) || !IsJsonObject(tool.OutputSchemaJson) ||
            tool.TimeoutSeconds is < 1 or > 90 || tool.MaxRows != 1 ||
            !tool.RequiredPermissions.Contains(AiCenterConstants.DocumentDraftPermission, StringComparer.Ordinal) ||
            !HasBusinessPermissions(tool.RequiredPermissions) ||
            definition.RequiredExecutionPermissions.Any(string.IsNullOrWhiteSpace) ||
            (definition.SupportsExecution
                ? !definition.RequiredExecutionPermissions.Contains(AiCenterConstants.DocumentExecutePermission, StringComparer.Ordinal)
                : definition.RequiredExecutionPermissions.Count != 0))
        {
            throw new InvalidOperationException($"AI action definition '{tool.ToolCode}' is invalid.");
        }
    }

    private bool HasIdentity() =>
        configuration.Enabled && currentUser.IsAuthenticated &&
        currentUser.UserId is { } userId && userId != Guid.Empty &&
        currentUser.TenantId is { } tenantId && tenantId != Guid.Empty &&
        tenantContext.IsResolved && !tenantContext.IsSystemScopeActive && tenantContext.TenantId == tenantId &&
        configuration.AllowedTenantIds.Contains(tenantId);

    private static bool HasBusinessPermissions(IReadOnlyCollection<string> permissions) =>
        permissions.Count > 0 && !permissions.Any(string.IsNullOrWhiteSpace) &&
        permissions.Any(permission => permission != AiCenterConstants.DocumentDraftPermission &&
            permission != AiCenterConstants.DocumentExecutePermission);

    private static bool IsJsonObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
