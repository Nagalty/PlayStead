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
}
