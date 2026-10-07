using Microsoft.AspNetCore.SignalR;
using PermissionSystem.Api.Hubs;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;

namespace PermissionSystem.Api.Services;

public sealed class AiRunProgressRelayHostedService(IServiceScopeFactory scopes, AiRunSubscriptions subscriptions,
    IHubContext<AiHub> hub, ILogger<AiRunProgressRelayHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sent = new Dictionary<string, (Guid RunId, long Version)>();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var current = subscriptions.Snapshot();
            foreach (var stale in sent.Keys.Except(current.Select(x => x.Key)).ToArray()) sent.Remove(stale);
            foreach (var item in current)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var sub = item.Value;
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(sub.TenantId, "Request");
                    var progress = await scope.ServiceProvider.GetRequiredService<AiRunProgressReader>()
                        .ReadAsync(sub.TenantId, sub.UserId, sub.SessionId, sub.Stamp, sub.RunId, stoppingToken);
                    if (!subscriptions.Matches(item.Key, sub)) continue;
                    if (progress is null) { subscriptions.Remove(item.Key); continue; }
                    if (sent.TryGetValue(item.Key, out var previous) && previous == (sub.RunId, progress.Version)) continue;
                    await hub.Clients.Client(item.Key).SendAsync("ReceiveAiRunEvent", new AiRunRealtimeMessage
                    {
                        RunId = progress.RunId, ConversationId = progress.ConversationId, Status = progress.Status,
                        EventType = "run.snapshot", ProgressVersion = progress.Version, ErrorCode = progress.ErrorCode,
                        OccurredAt = DateTimeOffset.UtcNow
                    }, stoppingToken);
                    foreach (var tool in progress.Tools)
                        await hub.Clients.Client(item.Key).SendAsync("ReceiveAiRunEvent", new AiRunRealtimeMessage
                        {
                            RunId = progress.RunId, ConversationId = progress.ConversationId, Status = progress.Status,
                            EventType = "tool.snapshot", ProgressVersion = progress.Version, InvocationId = tool.InvocationId,
                            ToolCode = tool.ToolCode, ToolStatus = tool.Status, OccurredAt = DateTimeOffset.UtcNow
                        }, stoppingToken);
                    sent[item.Key] = (sub.RunId, progress.Version);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch { logger.LogWarning("AI progress relay could not read a subscribed Run."); }
            }
        }
    }
}
