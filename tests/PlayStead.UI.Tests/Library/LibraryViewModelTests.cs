using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewModelTests
{
    [Fact]
    public async Task RefreshAsync_exposes_only_present_games_with_provider_size_and_path()
    {
        var now = new DateTimeOffset(
            2026, 9, 12, 9, 30, 0, TimeSpan.Zero);

        var presentGameId = new GameId(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var absentGameId = new GameId(
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var snapshot = new LibrarySnapshot(
            Games:
            [
                new LogicalGame(
                    presentGameId,
                    "Arma Reforger",
                    IsHidden: false,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now),

                new LogicalGame(
                    absentGameId,
                    "Old Removed Game",
                    IsHidden: false,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now)
            ],
            Installations:
            [
                new GameInstallation(
                    new InstallationId(
                        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                    presentGameId,
                    ProviderKind.Steam,
                    "1874880",
                    @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                    42_000_000_000,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now),

                new GameInstallation(
                    new InstallationId(
                        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                    absentGameId,
                    ProviderKind.Epic,
                    "removed-game",
                    @"C:\Games\Removed",
                    10_000_000_000,
                    IsPreferred: true,
                    IsPresent: false,
                    LastSeenUtc: now)
            ]);

        var sut = new LibraryViewModel(
            new StubLibraryStore(snapshot));

        await sut.RefreshAsync(CancellationToken.None);

        var item = Assert.Single(sut.Items);

        Assert.Equal(presentGameId, item.GameId);
        Assert.Equal("Arma Reforger", item.Title);
        Assert.Equal(ProviderKind.Steam, item.Provider);
        Assert.Equal("Steam", item.ProviderLabel);
        Assert.Equal(
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            item.InstallPath);
        Assert.Equal(42_000_000_000, item.InstalledSizeBytes);
    }

    [Fact]
    public async Task RefreshAsync_prefers_preferred_installation_then_sorts_by_title()
    {
        var now = DateTimeOffset.UtcNow;

        var alphaId = GameId.New();
        var betaId = GameId.New();

        var snapshot = new LibrarySnapshot(
            Games:
            [
                new LogicalGame(betaId, "Beta", false, now, now),
                new LogicalGame(alphaId, "Alpha", false, now, now)
            ],
            Installations:
            [
                new GameInstallation(
                    InstallationId.New(),
                    alphaId,
                    ProviderKind.Epic,
                    "alpha-epic",
                    @"C:\Games\AlphaEpic",
                    2,
                    IsPreferred: false,
                    IsPresent: true,
                    LastSeenUtc: now),

                new GameInstallation(
                    InstallationId.New(),
                    alphaId,
                    ProviderKind.Steam,
                    "123",
                    @"D:\Steam\Alpha",
                    3,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now),

                new GameInstallation(
                    InstallationId.New(),
                    betaId,
                    ProviderKind.Gog,
                    "beta",
                    @"E:\GOG\Beta",
                    null,
                    IsPreferred: false,
                    IsPresent: true,
                    LastSeenUtc: now)
            ]);

        var sut = new LibraryViewModel(
            new StubLibraryStore(snapshot));

        await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, sut.Items.Count);

        Assert.Equal("Alpha", sut.Items[0].Title);
        Assert.Equal(ProviderKind.Steam, sut.Items[0].Provider);

        Assert.Equal("Beta", sut.Items[1].Title);
        Assert.Equal(ProviderKind.Gog, sut.Items[1].Provider);
        Assert.Equal("GOG", sut.Items[1].ProviderLabel);
        Assert.Null(sut.Items[1].InstalledSizeBytes);
    }

    [Fact]
    public async Task RefreshAsync_exposes_epic_games_provider_label()
    {
        var now = DateTimeOffset.UtcNow;
        var gameId = GameId.New();
        var snapshot = new LibrarySnapshot(
            Games: [new LogicalGame(gameId, "Epic title", false, now, now)],
            Installations:
            [
                new GameInstallation(
                    InstallationId.New(),
                    gameId,
                    ProviderKind.Epic,
                    "catalog-id",
                    @"C:\Games\EpicTitle",
                    1,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now)
            ]);

        var sut = new LibraryViewModel(new StubLibraryStore(snapshot));

        await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal("Epic Games", Assert.Single(sut.Items).ProviderLabel);
    }

    [Fact]
    public async Task RefreshAsync_localizes_manual_provider_label()
    {
        var now = DateTimeOffset.UtcNow;
        var gameId = GameId.New();
        var snapshot = new LibrarySnapshot(
            Games: [new LogicalGame(gameId, "Manual title", false, now, now)],
            Installations:
            [
                new GameInstallation(
                    InstallationId.New(),
                    gameId,
                    ProviderKind.Manual,
                    "manual:game",
                    @"C:\Games\ManualTitle",
                    null,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now)
            ]);

        var sut = new LibraryViewModel(new StubLibraryStore(snapshot));

        await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal("Manuel", Assert.Single(sut.Items).ProviderLabel);
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }
}
