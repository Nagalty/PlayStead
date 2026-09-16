using PlayStead.Core.Notifications;

namespace PlayStead.Core.Notifications;

public interface INotificationCenterService
{
    Task<NotificationRecord> PublishOrRefreshAsync(
        NotificationPublishRequest request,
        CancellationToken cancellationToken);

    Task<NotificationRecord> MarkReadAsync(
        NotificationId id,
        CancellationToken cancellationToken);

    Task<NotificationRecord> ResolveAsync(
        NotificationId id,
        CancellationToken cancellationToken);

    Task<int> GetActiveCountAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationRecord>> ListAsync(
        NotificationListFilter filter,
        CancellationToken cancellationToken);

    Task<int> PurgeExpiredResolvedAsync(
        CancellationToken cancellationToken);
}
