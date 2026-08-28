using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.Mcp;

public interface IMcpCallerContext
{
    bool IsResolved { get; }

    McpCallerType CallerType { get; }

    Guid TenantId { get; }

    Guid? ActorUserId { get; }

    Guid? ClientBindingId { get; }

    Guid? ApiClientId { get; }

    string? OAuthClientId { get; }

    IReadOnlyCollection<string> AllowedScopes { get; }

    string? IpAddress { get; }

    void SetDelegatedUser(Guid tenantId, Guid userId, string? ipAddress);

    void SetServiceClient(McpServiceClientRecord client, string? ipAddress);

    bool HasScope(string scope);
}

public sealed class McpCallerContext : IMcpCallerContext
{
    private IReadOnlyCollection<string> _allowedScopes = [];

    public bool IsResolved { get; private set; }

    public McpCallerType CallerType { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public Guid? ClientBindingId { get; private set; }

    public Guid? ApiClientId { get; private set; }

    public string? OAuthClientId { get; private set; }

    public IReadOnlyCollection<string> AllowedScopes => _allowedScopes;

    public string? IpAddress { get; private set; }

    public void SetDelegatedUser(Guid tenantId, Guid userId, string? ipAddress)
    {
        EnsureNotResolved();
        IsResolved = true;
        CallerType = McpCallerType.DelegatedUser;
        TenantId = tenantId;
        ActorUserId = userId;
        IpAddress = ipAddress;
        _allowedScopes = McpToolScopes.All;
    }

    public void SetServiceClient(McpServiceClientRecord client, string? ipAddress)
    {
        EnsureNotResolved();
        IsResolved = true;
        CallerType = McpCallerType.ServiceClient;
        TenantId = client.TenantId;
        ClientBindingId = client.ClientBindingId;
        ApiClientId = client.ApiClientId;
        OAuthClientId = client.OAuthClientId;
        IpAddress = ipAddress;
        _allowedScopes = ParseScopes(client.AllowedScopes);
    }

    public bool HasScope(string scope)
    {
        return IsResolved && _allowedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureNotResolved()
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("The MCP caller context has already been resolved.");
        }
    }

    private static IReadOnlyCollection<string> ParseScopes(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                    [',', ';', '\r', '\n', '\t', ' '],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
