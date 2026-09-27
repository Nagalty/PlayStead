namespace PlayStead.Core.Notifications;

public enum AttentionSeverity { Warning = 1, ActionRequired = 2 }

public sealed record AttentionItem(
    NotificationId AttentionId,
    Guid? GameId,
    string Title,
    string Message,
    AttentionSeverity Severity,
    DateTimeOffset UpdatedAtUtc);

public interface IAttentionService
{
    IReadOnlyList<AttentionItem> Items { get; }
    event EventHandler? Changed;
    Task<IReadOnlyList<AttentionItem>> GetActiveAsync(CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
}
