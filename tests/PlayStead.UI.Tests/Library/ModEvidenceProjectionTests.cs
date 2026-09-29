using PlayStead.Core.Library;
using PlayStead.Core.Modding;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class ModEvidenceProjectionTests
{
    [Fact]
    public async Task Possibly_modded_is_excluded_and_confirmed_is_included()
    {
        var possible = GameId.New();
        var confirmed = GameId.New();
        var now = DateTimeOffset.UtcNow;
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(possible, "Possible", false, now, now), new LogicalGame(confirmed, "Confirmed", false, now, now)],
            [new GameInstallation(InstallationId.New(), possible, ProviderKind.Steam, "1", @"C:\Games\Possible", null, true, true, now), new GameInstallation(InstallationId.New(), confirmed, ProviderKind.Steam, "2", @"C:\Games\Confirmed", null, true, true, now)]);
        var store = new FakeEvidenceStore(
            new ModEvidence(possible, ProviderKind.Steam, "workshop", ModEvidenceKind.WorkshopContentPresent, ModDetectionState.PossiblyModded, now),
            new ModEvidence(confirmed, ProviderKind.Steam, "loader", ModEvidenceKind.ModLoaderPresent, ModDetectionState.ConfirmedModded, now));
        var library = new LibraryViewModel(new StubLibraryStore(snapshot));
        library.AttachModEvidenceStore(store);
        await library.RefreshAsync(CancellationToken.None);
        library.SetQuickFilter(LibraryQuickFilter.Modded);
        var item = Assert.Single(library.VisibleItems);
        Assert.Equal(confirmed, item.GameId);
    }

    [Fact]
    public async Task Game_detail_uses_the_confirmed_mods_copy()
    {
        var game = GameId.New();
        var now = DateTimeOffset.UtcNow;
        var item = new LibraryItemViewModel(game, "Confirmed", ProviderKind.Steam, "Steam", @"C:\Games\Confirmed", null);
        var store = new FakeEvidenceStore(new ModEvidence(game, ProviderKind.Steam, "loader", ModEvidenceKind.ModLoaderPresent, ModDetectionState.ConfirmedModded, now));
        var detail = new GameDetailViewModel(item, null, null, null, modEvidenceStore: store);

        await detail.LoadAsync(CancellationToken.None);

        Assert.True(detail.HasModEvidence);
        Assert.Equal("Toi, tu utilises des mods sur ce jeu, c’est certain. Je le vois, tu sais.", detail.ModStatusLabel);
    }

    [Fact]
    public async Task Game_detail_uses_the_possible_mods_copy()
    {
        var game = GameId.New();
        var now = DateTimeOffset.UtcNow;
        var item = new LibraryItemViewModel(game, "Possible", ProviderKind.Steam, "Steam", @"C:\Games\Possible", null);
        var store = new FakeEvidenceStore(new ModEvidence(game, ProviderKind.Steam, "loader", ModEvidenceKind.ModLoaderPresent, ModDetectionState.PossiblyModded, now));
        var detail = new GameDetailViewModel(item, null, null, null, modEvidenceStore: store);

        await detail.LoadAsync(CancellationToken.None);

        Assert.True(detail.HasModEvidence);
        Assert.Equal("Il me semble que tu utilises des mods sur ce jeu, mais j’ai encore un doute.", detail.ModStatusLabel);
    }

    [Fact]
    public async Task Game_detail_hides_mods_for_unknown_state()
    {
        var game = GameId.New();
        var now = DateTimeOffset.UtcNow;
        var item = new LibraryItemViewModel(game, "Unknown", ProviderKind.Steam, "Steam", @"C:\Games\Unknown", null);
        var detail = new GameDetailViewModel(item, null, null, null, modEvidenceStore: new FakeEvidenceStore());

        await detail.LoadAsync(CancellationToken.None);

        Assert.False(detail.HasModEvidence);
        Assert.Equal(string.Empty, detail.ModStatusLabel);
    }

    private sealed class StubLibraryStore(LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class FakeEvidenceStore(params ModEvidence[] values) : IModEvidenceStore
    {
        private readonly List<ModEvidence> _values = values.ToList();
        public Task<IReadOnlyList<ModEvidence>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ModEvidence>>(_values.Where(value => value.GameId == gameId).ToArray());
        public Task<IReadOnlyList<ModEvidence>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ModEvidence>>(_values.ToArray());
        public Task UpsertAsync(ModEvidence evidence, CancellationToken cancellationToken) { _values.RemoveAll(value => value.GameId == evidence.GameId && value.DetectorId == evidence.DetectorId && value.EvidenceKind == evidence.EvidenceKind); _values.Add(evidence); return Task.CompletedTask; }
        public Task RemoveAsync(GameId gameId, string detectorId, ModEvidenceKind evidenceKind, CancellationToken cancellationToken) { _values.RemoveAll(value => value.GameId == gameId && value.DetectorId == detectorId && value.EvidenceKind == evidenceKind); return Task.CompletedTask; }
    }
}
