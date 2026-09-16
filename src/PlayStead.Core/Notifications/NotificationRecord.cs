namespace PlayStead.Core.Notifications;

public sealed record NotificationRecord(
    NotificationId NotificationId,
    NotificationProducer Producer,
    string SubjectId,
    string Reason,
    string DeduplicationKey,
    NotificationPriority Priority,
    NotificationState State,
    string Title,
    string Message,
    string? PayloadJson,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DateTimeOffset? ReadUtc,
    DateTimeOffset? ResolvedUtc);
