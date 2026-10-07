using System.Collections.Concurrent;
using System.Security.Claims;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Services;

public sealed record AiRunSubscription(Guid TenantId, Guid UserId, string SessionId, Guid Stamp, Guid RunId);

public sealed class AiRunSubscriptions
{
    private readonly ConcurrentDictionary<string, AiRunSubscription> _items = new();
    public IReadOnlyCollection<KeyValuePair<string, AiRunSubscription>> Snapshot() => _items.ToArray();
    public void Set(string connectionId, AiRunSubscription subscription) => _items[connectionId] = subscription;
    public void Remove(string connectionId) => _items.TryRemove(connectionId, out _);
    public bool Matches(string connectionId, AiRunSubscription subscription) => _items.TryGetValue(connectionId, out var current) && current == subscription;
    public static AiRunSubscription? FromClaims(ClaimsPrincipal? user, Guid runId)
    {
        if (user?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(user.FindFirst(ClaimConstants.TenantId)?.Value, out var tenant) ||
            !Guid.TryParse(user.FindFirst(ClaimConstants.UserId)?.Value, out var actor) ||
            !Guid.TryParse(user.FindFirst(ClaimConstants.SecurityStamp)?.Value, out var stamp) ||
            string.IsNullOrWhiteSpace(user.FindFirst(ClaimConstants.SessionId)?.Value)) return null;
        return new(tenant, actor, user.FindFirst(ClaimConstants.SessionId)!.Value, stamp, runId);
    }
}
