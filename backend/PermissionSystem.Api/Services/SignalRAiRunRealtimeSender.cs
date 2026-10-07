using PermissionSystem.Application.AiCenter;

namespace PermissionSystem.Api.Services;

public sealed class SignalRAiRunRealtimeSender : IAiRunRealtimeSender
{
    public Task SendToUserAsync(
        Guid userId,
        AiRunRealtimeMessage message,
        CancellationToken cancellationToken = default)
    {
        // Progress is delivered from committed state by the authenticated subscription relay.
        return Task.CompletedTask;
    }
}
