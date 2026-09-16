using PlayStead.Core.Notifications;

namespace PlayStead.Core.Persistence;

public interface INotificationStore
{
    Task<NotificationRecord?> GetByIdAsync(
        NotificationId id,
        CancellationToken cancellationToken);

    Task<NotificationRecord?> GetByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken);

    Task InsertAsync(
        NotificationRecord notification,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        NotificationRecord notification,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationRecord>> ListActiveAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationRecord>> ListResolvedAsync(
        CancellationToken cancellationToken);

    Task<int> GetActiveCountAsync(
        CancellationToken cancellationToken);

    Task<int> DeleteResolvedOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken);
}
