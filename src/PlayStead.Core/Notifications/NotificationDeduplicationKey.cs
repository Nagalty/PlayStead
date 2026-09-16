namespace PlayStead.Core.Notifications;

public sealed record NotificationDeduplicationKey
{
    public NotificationDeduplicationKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
