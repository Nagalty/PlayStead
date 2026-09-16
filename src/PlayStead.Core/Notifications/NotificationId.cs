namespace PlayStead.Core.Notifications;

public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() =>
        new(Guid.NewGuid());

    public static NotificationId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Guid.TryParseExact(value, "D", out var guid)
            ? new NotificationId(guid)
            : throw new FormatException("Notification ID must be a D-format GUID.");
    }

    public static bool TryParse(
        string? value,
        out NotificationId notificationId)
    {
        if (value is not null && Guid.TryParseExact(value, "D", out var guid))
        {
            notificationId = new NotificationId(guid);
            return true;
        }

        notificationId = default;
        return false;
    }

    public override string ToString() =>
        Value.ToString("D");
}
