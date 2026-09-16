using PlayStead.Core.Persistence;

namespace PlayStead.Core.Notifications;

public sealed class NotificationCenterService : INotificationCenterService
{
    private readonly INotificationStore _store;
    private readonly TimeProvider _timeProvider;

    public NotificationCenterService(INotificationStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var existing = await _store.GetByDeduplicationKeyAsync(request.DeduplicationKey.Value, cancellationToken);
        NotificationRecord record;
        if (existing is null)
        {
            record = new(NotificationId.New(), request.Producer, request.SubjectId, request.Reason, request.DeduplicationKey.Value, request.Priority, NotificationState.Unread, request.Title, request.Message, request.PayloadJson, now, now, null, null);
            await _store.InsertAsync(record, cancellationToken);
        }
        else
        {
            var state = existing.State == NotificationState.Resolved ? NotificationState.Unread : existing.State;
            record = existing with
            {
                Producer = request.Producer,
                SubjectId = request.SubjectId,
                Reason = request.Reason,
                Priority = request.Priority,
                Title = request.Title,
                Message = request.Message,
                PayloadJson = request.PayloadJson,
                State = state,
                UpdatedUtc = now,
                ReadUtc = state == NotificationState.Read ? existing.ReadUtc : null,
                ResolvedUtc = null
            };
            await _store.UpdateAsync(record, cancellationToken);
        }

        return record;
    }

    public async Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = await RequireAsync(id, cancellationToken);
        if (existing.State != NotificationState.Unread) return existing;
        var now = _timeProvider.GetUtcNow();
        var updated = existing with { State = NotificationState.Read, ReadUtc = now, UpdatedUtc = now };
        await _store.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = await RequireAsync(id, cancellationToken);
        if (existing.State == NotificationState.Resolved) return existing;
        var now = _timeProvider.GetUtcNow();
        var updated = existing with { State = NotificationState.Resolved, ResolvedUtc = now, UpdatedUtc = now };
        await _store.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) =>
        _store.GetActiveCountAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken)
    {
        return filter switch
        {
            NotificationListFilter.Active => await _store.ListActiveAsync(cancellationToken),
            NotificationListFilter.Resolved => await _store.ListResolvedAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };
    }

    public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _store.DeleteResolvedOlderThanAsync(_timeProvider.GetUtcNow().AddDays(-90), cancellationToken);
    }

    private async Task<NotificationRecord> RequireAsync(NotificationId id, CancellationToken cancellationToken) =>
        await _store.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Notification '{id}' was not found.");
}
