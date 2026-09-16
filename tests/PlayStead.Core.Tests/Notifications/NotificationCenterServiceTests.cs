using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Notifications;

public sealed class NotificationCenterServiceTests
{
    [Fact]
    public async Task First_publication_creates_unread_record()
    {
        var store = new FakeStore();
        var now = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        var sut = new NotificationCenterService(store, new FixedTimeProvider(now));

        var result = await sut.PublishOrRefreshAsync(Request("k1"), CancellationToken.None);

        Assert.Equal(NotificationState.Unread, result.State);
        Assert.Equal(now, result.CreatedUtc);
        Assert.Equal(now, result.UpdatedUtc);
        Assert.Null(result.ReadUtc);
        Assert.Null(result.ResolvedUtc);
        Assert.Single(store.Items);
    }

    [Fact]
    public async Task Active_republication_keeps_identity_state_and_created_timestamp_but_refreshes_content()
    {
        var store = new FakeStore();
        var firstNow = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        var sut = new NotificationCenterService(store, new FixedTimeProvider(firstNow));
        var first = await sut.PublishOrRefreshAsync(Request("k1"), CancellationToken.None);
        store.Items[0] = first with { State = NotificationState.Read, ReadUtc = first.UpdatedUtc };
        var refreshedNow = firstNow.AddMinutes(5);
        sut = new NotificationCenterService(store, new FixedTimeProvider(refreshedNow));

        var result = await sut.PublishOrRefreshAsync(Request("k1") with { Title = "Refreshed" }, CancellationToken.None);

        Assert.Equal(first.NotificationId, result.NotificationId);
        Assert.Equal(first.CreatedUtc, result.CreatedUtc);
        Assert.Equal(NotificationState.Read, result.State);
        Assert.Equal(first.UpdatedUtc, result.ReadUtc);
        Assert.Equal(refreshedNow, result.UpdatedUtc);
        Assert.Equal("Refreshed", result.Title);
    }

    [Fact]
    public async Task Resolved_republication_reactivates_same_record()
    {
        var store = new FakeStore();
        var now = DateTimeOffset.UtcNow;
        var original = new NotificationRecord(NotificationId.New(), NotificationProducer.IdentityResolution, "s", "r", "k", NotificationPriority.Warning, NotificationState.Resolved, "old", "old", null, now.AddDays(-1), now.AddDays(-1), null, now.AddDays(-1));
        store.Items.Add(original);
        var current = now;
        var sut = new NotificationCenterService(store, new FixedTimeProvider(current));

        var result = await sut.PublishOrRefreshAsync(Request("k"), CancellationToken.None);

        Assert.Equal(original.NotificationId, result.NotificationId);
        Assert.Equal(original.CreatedUtc, result.CreatedUtc);
        Assert.Equal(NotificationState.Unread, result.State);
        Assert.Null(result.ReadUtc);
        Assert.Null(result.ResolvedUtc);
    }

    [Fact]
    public async Task Mark_read_is_idempotent_and_never_resolves()
    {
        var store = new FakeStore();
        var now = DateTimeOffset.UtcNow;
        var record = new NotificationRecord(NotificationId.New(), NotificationProducer.IdentityResolution, "s", "r", "k", NotificationPriority.Warning, NotificationState.Unread, "t", "m", null, now, now, null, null);
        store.Items.Add(record);
        var sut = new NotificationCenterService(store, new FixedTimeProvider(now.AddMinutes(1)));

        var read = await sut.MarkReadAsync(record.NotificationId, CancellationToken.None);
        var again = await sut.MarkReadAsync(record.NotificationId, CancellationToken.None);

        Assert.Equal(NotificationState.Read, read.State);
        Assert.Equal(NotificationState.Read, again.State);
        Assert.Equal(read.ReadUtc, again.ReadUtc);
    }

    [Fact]
    public async Task Resolve_allows_unread_and_is_idempotent()
    {
        var store = new FakeStore();
        var now = DateTimeOffset.UtcNow;
        var record = new NotificationRecord(NotificationId.New(), NotificationProducer.IdentityResolution, "s", "r", "k", NotificationPriority.Warning, NotificationState.Unread, "t", "m", null, now, now, null, null);
        store.Items.Add(record);
        var sut = new NotificationCenterService(store, new FixedTimeProvider(now.AddMinutes(1)));

        var resolved = await sut.ResolveAsync(record.NotificationId, CancellationToken.None);
        var again = await sut.ResolveAsync(record.NotificationId, CancellationToken.None);

        Assert.Equal(NotificationState.Resolved, resolved.State);
        Assert.NotNull(resolved.ResolvedUtc);
        Assert.Equal(resolved, again);
    }

    [Fact]
    public async Task Count_and_lists_forward_to_store()
    {
        var store = new FakeStore();
        var sut = new NotificationCenterService(store, TimeProvider.System);

        Assert.Equal(7, await sut.GetActiveCountAsync(CancellationToken.None));
        Assert.Same(store.Active, await sut.ListAsync(NotificationListFilter.Active, CancellationToken.None));
        Assert.Same(store.Resolved, await sut.ListAsync(NotificationListFilter.Resolved, CancellationToken.None));
    }

    [Fact]
    public async Task Purge_uses_ninety_day_cutoff()
    {
        var now = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore();
        var sut = new NotificationCenterService(store, new FixedTimeProvider(now));

        await sut.PurgeExpiredResolvedAsync(CancellationToken.None);

        Assert.Equal(now.AddDays(-90), store.Cutoff);
    }

    [Fact]
    public async Task Already_cancelled_token_is_propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = new NotificationCenterService(new FakeStore(), TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetActiveCountAsync(cts.Token));
    }

    private static NotificationPublishRequest Request(string key) => new(NotificationProducer.IdentityResolution, "subject", "reason", new NotificationDeduplicationKey(key), NotificationPriority.ActionRequired, "Title", "Message", "{\"x\":1}");

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeStore : INotificationStore
    {
        public List<NotificationRecord> Items { get; } = [];
        public IReadOnlyList<NotificationRecord> Active { get; } = [];
        public IReadOnlyList<NotificationRecord> Resolved { get; } = [];
        public DateTimeOffset? Cutoff { get; private set; }
        public Task<NotificationRecord?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Items.SingleOrDefault(x => x.NotificationId == id)); }
        public Task<NotificationRecord?> GetByDeduplicationKeyAsync(string key, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Items.SingleOrDefault(x => x.DeduplicationKey == key)); }
        public Task InsertAsync(NotificationRecord notification, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Items.Add(notification); return Task.CompletedTask; }
        public Task UpdateAsync(NotificationRecord notification, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Items[Items.FindIndex(x => x.NotificationId == notification.NotificationId)] = notification; return Task.CompletedTask; }
        public Task<IReadOnlyList<NotificationRecord>> ListActiveAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Active); }
        public Task<IReadOnlyList<NotificationRecord>> ListResolvedAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Resolved); }
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(7); }
        public Task<int> DeleteResolvedOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Cutoff = cutoffUtc; return Task.FromResult(0); }
    }
}
