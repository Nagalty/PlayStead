using Microsoft.Data.Sqlite;
using PlayStead.Core.Notifications;
using PlayStead.Data.Database;
using PlayStead.Data.Notifications;

namespace PlayStead.Data.Tests.Notifications;

public sealed class SqliteNotificationStoreTests
{
    [Fact]
    public async Task Missing_id_returns_null()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Store.GetByIdAsync(
            NotificationId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Insert_and_get_round_trip_all_fields_and_deduplication_lookup()
    {
        await using var fixture = await Fixture.CreateAsync();
        var notification = fixture.Create(NotificationState.Unread);

        await fixture.Store.InsertAsync(notification, CancellationToken.None);

        Assert.Equal(notification, await fixture.Store.GetByIdAsync(notification.NotificationId, CancellationToken.None));
        Assert.Equal(notification, await fixture.Store.GetByDeduplicationKeyAsync(notification.DeduplicationKey, CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_deduplication_key_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.Create(NotificationState.Unread);
        var duplicate = first with { NotificationId = NotificationId.New() };

        await fixture.Store.InsertAsync(first, CancellationToken.None);

        await Assert.ThrowsAnyAsync<SqliteException>(() =>
            fixture.Store.InsertAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task Update_persists_mutable_fields_and_payload_null_round_trip()
    {
        await using var fixture = await Fixture.CreateAsync();
        var original = fixture.Create(NotificationState.Unread);
        await fixture.Store.InsertAsync(original, CancellationToken.None);
        var updated = original with
        {
            State = NotificationState.Read,
            Title = "Updated",
            Message = "Changed",
            PayloadJson = null,
            UpdatedUtc = original.UpdatedUtc.AddMinutes(1),
            ReadUtc = original.UpdatedUtc.AddMinutes(1)
        };

        await fixture.Store.UpdateAsync(updated, CancellationToken.None);

        Assert.Equal(updated, await fixture.Store.GetByIdAsync(original.NotificationId, CancellationToken.None));
    }

    [Fact]
    public async Task Active_and_resolved_queries_count_and_order_correctly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var low = fixture.Create(NotificationState.Unread, NotificationPriority.Info, 1);
        var high = fixture.Create(NotificationState.Read, NotificationPriority.ActionRequired, 2);
        var resolved = fixture.Create(NotificationState.Resolved, NotificationPriority.Warning, 3);
        await fixture.Store.InsertAsync(low, CancellationToken.None);
        await fixture.Store.InsertAsync(high, CancellationToken.None);
        await fixture.Store.InsertAsync(resolved, CancellationToken.None);

        var active = await fixture.Store.ListActiveAsync(CancellationToken.None);
        var archived = await fixture.Store.ListResolvedAsync(CancellationToken.None);

        Assert.Equal(new[] { high.NotificationId, low.NotificationId }, active.Select(x => x.NotificationId));
        Assert.Single(archived);
        Assert.Equal(resolved.NotificationId, archived[0].NotificationId);
        Assert.Equal(2, await fixture.Store.GetActiveCountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Purge_deletes_only_resolved_rows_older_than_cutoff()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var old = fixture.Create(NotificationState.Resolved, resolvedUtc: cutoff.AddTicks(-1));
        var atCutoff = fixture.Create(NotificationState.Resolved, resolvedUtc: cutoff);
        var active = fixture.Create(NotificationState.Unread);
        await fixture.Store.InsertAsync(old, CancellationToken.None);
        await fixture.Store.InsertAsync(atCutoff, CancellationToken.None);
        await fixture.Store.InsertAsync(active, CancellationToken.None);

        var deleted = await fixture.Store.DeleteResolvedOlderThanAsync(cutoff, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Null(await fixture.Store.GetByIdAsync(old.NotificationId, CancellationToken.None));
        Assert.NotNull(await fixture.Store.GetByIdAsync(atCutoff.NotificationId, CancellationToken.None));
        Assert.NotNull(await fixture.Store.GetByIdAsync(active.NotificationId, CancellationToken.None));
    }

    [Fact]
    public async Task Already_cancelled_token_is_propagated()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Store.ListActiveAsync(cts.Token));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _directory;
        private int _sequence;
        public SqliteNotificationStore Store { get; }

        private Fixture(string directory)
        {
            _directory = directory;
            Store = new SqliteNotificationStore(new DatabaseOptions(Path.Combine(directory, "playstead.db"), Path.Combine(directory, "backups")));
        }

        public static async Task<Fixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PlaySteadNotificationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var fixture = new Fixture(directory);
            var initializer = new DatabaseInitializer(new DatabaseOptions(Path.Combine(directory, "playstead.db"), Path.Combine(directory, "backups")));
            await initializer.InitializeAsync(CancellationToken.None);
            return fixture;
        }

        public NotificationRecord Create(NotificationState state, NotificationPriority priority = NotificationPriority.Warning, int offset = 0, DateTimeOffset? resolvedUtc = null)
        {
            var now = DateTimeOffset.UtcNow.AddMinutes(offset);
            return new(NotificationId.New(), NotificationProducer.IdentityResolution, "game-" + (++_sequence), "reason", "dedup-" + _sequence, priority, state, "Title", "Message", "{\"x\":1}", now, now, state == NotificationState.Read ? now : null, state == NotificationState.Resolved ? resolvedUtc ?? now : null);
        }

        public ValueTask DisposeAsync()
        {
            try { Directory.Delete(_directory, true); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}
