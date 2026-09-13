namespace PlayStead.UI.Tray;

public static class TrayStatusText
{
    public static string Format(
        int activeSessionCount)
    {
        if (activeSessionCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeSessionCount),
                activeSessionCount,
                "Active session count cannot be negative.");
        }

        return activeSessionCount switch
        {
            0 => "PlayStead — aucune session active",
            1 => "PlayStead — 1 session active",
            _ => $"PlayStead — {activeSessionCount} sessions actives"
        };
    }
}
