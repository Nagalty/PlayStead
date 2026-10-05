using System.Diagnostics;
using System.Text;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Core.Tests.ProviderActivity;

public sealed class ProviderActivityReconciliationServiceTests
{
    [Fact]
    public async Task Provider_values_are_persisted_and_unchanged_values_do_not_raise_again()
    {
        var game = GameId.New();
        var store = new Store();
        var value = new ProviderActivityMetadata(game, ProviderKind.Steam, "123", TimeSpan.FromHours(4), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow, ProviderActivityAvailability.Complete);
        var source = new Source(value);
        var sut = new ProviderActivityReconciliationService(store, [source]);
        var changed = 0;
        sut.Changed += (_, _) => changed++;
        var snapshot = new LibrarySnapshot([], [new GameInstallation(InstallationId.New(), game, ProviderKind.Steam, "123", "C:\\Game", null, true, true, DateTimeOffset.UtcNow)]);

        await sut.RefreshAsync(snapshot, CancellationToken.None);
        await sut.RefreshAsync(snapshot, CancellationToken.None);

        Assert.Single(await store.GetAllAsync(CancellationToken.None));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task An_incomplete_refresh_does_not_erase_known_provider_activity()
    {
        var game = GameId.New();
        var store = new Store();
        var complete = new ProviderActivityMetadata(
            game,
            ProviderKind.Steam,
            "123",
            TimeSpan.FromHours(12),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            DateTimeOffset.UtcNow,
            ProviderActivityAvailability.Complete);
        var source = new SequenceSource(ProviderKind.Steam, complete, ProviderActivityMetadata.Unknown(
            game,
            ProviderKind.Steam,
            "123",
            DateTimeOffset.UtcNow));
        var sut = new ProviderActivityReconciliationService(store, [source]);
        var snapshot = new LibrarySnapshot([], [new GameInstallation(InstallationId.New(), game, ProviderKind.Steam, "123", "C:\\Game", null, true, true, DateTimeOffset.UtcNow)]);

        await sut.RefreshAsync(snapshot, CancellationToken.None);
        await sut.RefreshAsync(snapshot, CancellationToken.None);

        var persisted = Assert.Single(await store.GetAllAsync(CancellationToken.None));
        Assert.Equal(complete.TotalPlaytime, persisted.TotalPlaytime);
        Assert.Equal(complete.LastPlayedAtUtc, persisted.LastPlayedAtUtc);
    }

    [Fact]
    public async Task Complete_refresh_can_clear_a_provider_field_known_to_be_unavailable()
    {
        var game = GameId.New();
        var store = new Store();
        var first = new ProviderActivityMetadata(
            game, ProviderKind.Gog, "1495134320", TimeSpan.FromMinutes(23),
            new DateTimeOffset(2024, 9, 13, 9, 1, 58, TimeSpan.Zero),
            DateTimeOffset.UtcNow, ProviderActivityAvailability.Complete);
        var current = new ProviderActivityMetadata(
            game, ProviderKind.Gog, "1495134320", TimeSpan.FromMinutes(23), null,
            DateTimeOffset.UtcNow, ProviderActivityAvailability.Complete);
        var sut = new ProviderActivityReconciliationService(store, [new SequenceSource(ProviderKind.Gog, first, current)]);
        var snapshot = new LibrarySnapshot([], [new GameInstallation(InstallationId.New(), game, ProviderKind.Gog, "1495134320", "C:\\Game", null, true, true, DateTimeOffset.UtcNow)]);

        await sut.RefreshAsync(snapshot, CancellationToken.None);
        await sut.RefreshAsync(snapshot, CancellationToken.None);

        var persisted = Assert.Single(await store.GetAllAsync(CancellationToken.None));
        Assert.Equal(TimeSpan.FromMinutes(23), persisted.TotalPlaytime);
        Assert.Null(persisted.LastPlayedAtUtc);
    }

    [Fact]
    public async Task Source_exception_is_logged_and_next_source_still_runs()
    {
        var output = new StringBuilder();
        using var listener = new TextWriterTraceListener(new StringWriter(output));
        Trace.Listeners.Add(listener);
        try
        {
            var game = GameId.New();
            var store = new Store();
            var failing = new ThrowingSource(ProviderKind.Steam);
            var succeeding = new Source(new ProviderActivityMetadata(
                game, ProviderKind.Epic, "epic-1", null, null, DateTimeOffset.UtcNow,
                ProviderActivityAvailability.Unknown));
            var sut = new ProviderActivityReconciliationService(store, [failing, succeeding]);
            var snapshot = new LibrarySnapshot([], [
                new GameInstallation(InstallationId.New(), game, ProviderKind.Steam, "123", "C:\\Game", null, true, true, DateTimeOffset.UtcNow),
                new GameInstallation(InstallationId.New(), game, ProviderKind.Epic, "epic-1", "C:\\Game", null, true, true, DateTimeOffset.UtcNow)]);

            await sut.RefreshAsync(snapshot, CancellationToken.None);

            Assert.Single(await store.GetAllAsync(CancellationToken.None));
            listener.Flush();
            Trace.Listeners.Remove(listener);
            var trace = output.ToString();
            Assert.Contains("[PROVIDER-ACTIVITY-ERROR]", trace, StringComparison.Ordinal);
            Assert.Contains("Source=", trace, StringComparison.Ordinal);
            Assert.Contains("Exception=System.InvalidOperationException", trace, StringComparison.Ordinal);
            Assert.Contains("Message=synthetic provider failure", trace, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    private sealed class Store : IProviderActivityMetadataStore
    {
        private readonly List<ProviderActivityMetadata> _items = [];
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken _) => Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>(_items.ToArray());
        public Task UpsertAsync(ProviderActivityMetadata value, CancellationToken _)
        { _items.RemoveAll(x => x.GameId == value.GameId && x.Provider == value.Provider); _items.Add(value); return Task.CompletedTask; }
    }

    private sealed class Source(ProviderActivityMetadata value) : IProviderActivityMetadataSource
    {
        public ProviderKind Provider => value.Provider;
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(IReadOnlyCollection<GameInstallation> _, CancellationToken __) => Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>([value]);
    }

    private sealed class SequenceSource(ProviderKind provider, params ProviderActivityMetadata[] values) : IProviderActivityMetadataSource
    {
        private int _index;
        public ProviderKind Provider => provider;
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(IReadOnlyCollection<GameInstallation> _, CancellationToken __)
        {
            var value = values[Math.Min(_index++, values.Length - 1)];
            return Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>([value]);
        }
    }

    private sealed class ThrowingSource(ProviderKind provider) : IProviderActivityMetadataSource
    {
        public ProviderKind Provider => provider;
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(IReadOnlyCollection<GameInstallation> _, CancellationToken __) =>
            throw new InvalidOperationException("synthetic provider failure");
    }
}
