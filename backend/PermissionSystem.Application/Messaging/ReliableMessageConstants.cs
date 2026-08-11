namespace PermissionSystem.Application.Messaging;

public static class ReliableMessageStatus
{
    public const string Pending = "Pending";

    public const string Processing = "Processing";

    public const string Published = "Published";

    public const string Failed = "Failed";

    public const string Processed = "Processed";
}

public static class DeadLetterMessageStatuses
{
    public const string Pending = "Pending";

    public const string Replayed = "Replayed";

    public const string Discarded = "Discarded";
}

public sealed class SystemNotificationCreatedEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
