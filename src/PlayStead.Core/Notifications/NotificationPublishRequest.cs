namespace PlayStead.Core.Notifications;

public sealed record NotificationPublishRequest(
    NotificationProducer Producer,
    string SubjectId,
    string Reason,
    NotificationDeduplicationKey DeduplicationKey,
    NotificationPriority Priority,
    string Title,
    string Message,
    string? PayloadJson);
