using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewModelSteamStatusTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefreshAsync_projects_Steam_state_by_external_app_id_and_leaves_non_Steam_without_status()
    {
        var steamGameId = GameId.New();
        var gogGameId = GameId.New();

        var store = new StubLibraryStore(
            new LibrarySnapshot(
                [
                    Game(steamGameId, "Counter-Strike 2"),
                    Game(gogGameId, "GOG Game")
                ],
                [
                    Installation(
                        steamGameId,
                        ProviderKind.Steam,
                        "730",
                        @"G:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"),
                    Installation(
                        gogGameId,
                        ProviderKind.Gog,
                        "gog-1",
                        @"C:\Games\GOG")
                ]));

        var runtime = new MutableSteamRuntime(
            Snapshot(
                Entry(
                    "730",
                    SteamUpdateState.UpdateAvailable,
                    SteamUpdateReason.DepotManifestMismatch)));

        var sut = new LibraryViewModel(
            store,
            runtime);

        await sut.RefreshAsync(
            CancellationToken.None);

        var steam = Assert.Single(
            sut.Items,
            x => x.Provider == ProviderKind.Steam);

        Assert.Equal(
            SteamUpdateState.UpdateAvailable,
            steam.SteamState);

        Assert.Equal(
            "Mise à jour disponible",
            steam.SteamStatusLabel);

        var gog = Assert.Single(
            sut.Items,
            x => x.Provider == ProviderKind.Gog);

        Assert.Null(gog.SteamState);
        Assert.False(gog.HasSteamStatus);
    }

    [Fact]
    public async Task Steam_game_without_reference_entry_is_displayed_as_unknown()
    {
        var gameId = GameId.New();

        var sut = new LibraryViewModel(
            new StubLibraryStore(
                new LibrarySnapshot(
                    [Game(gameId, "Steam Game")],
                    [
                        Installation(
                            gameId,
                            ProviderKind.Steam,
                            "730",
                            @"G:\SteamLibrary\steamapps\common\Game")
                    ])),
            new MutableSteamRuntime(
                SteamReferenceSnapshot.Empty));

        await sut.RefreshAsync(
            CancellationToken.None);

        var item = Assert.Single(sut.Items);

        Assert.Equal(
            SteamUpdateState.Unknown,
            item.SteamState);

        Assert.Equal(
            "État inconnu",
            item.SteamStatusLabel);
    }

    [Fact]
    public async Task VerifySteamAsync_exposes_Checking_disables_manual_refresh_then_reprojects_final_state()
    {
        var gameId = GameId.New();

        var store = new StubLibraryStore(
            new LibrarySnapshot(
                [Game(gameId, "Steam Game")],
                [
                    Installation(
                        gameId,
                        ProviderKind.Steam,
                        "730",
                        @"G:\SteamLibrary\steamapps\common\Game")
                ]));

        var runtime = new BlockingSteamRuntime(
            initial:
                Snapshot(
                    Entry(
                        "730",
                        SteamUpdateState.UpToDate,
                        SteamUpdateReason.DepotManifestsMatch)),
            final:
                Snapshot(
                    Entry(
                        "730",
                        SteamUpdateState.UpdateAvailable,
                        SteamUpdateReason.DepotManifestMismatch)));

        var sut = new LibraryViewModel(
            store,
            runtime);

        await sut.RefreshAsync(
            CancellationToken.None);

        Assert.True(sut.CanVerifySteam);
        Assert.False(sut.IsSteamChecking);

        var verification =
            sut.VerifySteamAsync(
                CancellationToken.None);

        await runtime.RefreshStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.True(sut.IsSteamChecking);
        Assert.False(sut.CanVerifySteam);

        var checkingItem =
            Assert.Single(sut.Items);

        Assert.Equal(
            SteamUpdateState.Checking,
            checkingItem.SteamState);

        Assert.Equal(
            "Vérification…",
            checkingItem.SteamStatusLabel);

        runtime.Release();

        await verification;

        Assert.False(sut.IsSteamChecking);
        Assert.True(sut.CanVerifySteam);
        Assert.Equal(1, runtime.RefreshAllCount);

        var finalItem =
            Assert.Single(sut.Items);

        Assert.Equal(
            SteamUpdateState.UpdateAvailable,
            finalItem.SteamState);

        Assert.Equal(
            "Mise à jour disponible",
            finalItem.SteamStatusLabel);
    }

    [Fact]
    public async Task VerifySteamAsync_does_not_start_a_second_forced_refresh_while_one_is_running()
    {
        var gameId = GameId.New();

        var runtime = new BlockingSteamRuntime(
            initial:
                Snapshot(
                    Entry(
                        "730",
                        SteamUpdateState.UpToDate,
                        SteamUpdateReason.DepotManifestsMatch)),
            final:
                Snapshot(
                    Entry(
                        "730",
                        SteamUpdateState.UpToDate,
                        SteamUpdateReason.DepotManifestsMatch)));

        var sut = new LibraryViewModel(
            new StubLibraryStore(
                new LibrarySnapshot(
                    [Game(gameId, "Steam Game")],
                    [
                        Installation(
                            gameId,
                            ProviderKind.Steam,
                            "730",
                            @"G:\SteamLibrary\steamapps\common\Game")
                    ])),
            runtime);

        await sut.RefreshAsync(
            CancellationToken.None);

        var first =
            sut.VerifySteamAsync(
                CancellationToken.None);

        await runtime.RefreshStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        var second =
            sut.VerifySteamAsync(
                CancellationToken.None);

        await Task.Delay(100);

        Assert.Equal(1, runtime.RefreshAllCount);

        runtime.Release();

        await Task.WhenAll(
            first,
            second);

        Assert.Equal(1, runtime.RefreshAllCount);
    }

    private static LogicalGame Game(
        GameId id,
        string title)
        => new(
            id,
            title,
            IsHidden: false,
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now);

    private static GameInstallation Installation(
        GameId gameId,
        ProviderKind provider,
        string externalId,
        string path)
        => new(
            InstallationId.New(),
            gameId,
            provider,
            externalId,
            path,
            42_000_000_000,
            IsPreferred: true,
            IsPresent: true,
            LastSeenUtc: Now);

    private static SteamReferenceSnapshot Snapshot(
        params SteamReferenceEntry[] entries)
        => new(entries);

    private static SteamReferenceEntry Entry(
        string appId,
        SteamUpdateState state,
        SteamUpdateReason reason)
        => new(
            appId,
            "public",
            new SteamUpdateEvaluation(
                state,
                reason,
                Now,
                Array.Empty<string>()));

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class MutableSteamRuntime :
        ISteamReferenceRuntime
    {
        public MutableSteamRuntime(
            SteamReferenceSnapshot current)
        {
            Current = current;
        }

        public SteamReferenceSnapshot Current
        {
            get;
            private set;
        }

        public Task<SteamReferenceSnapshot> LoadCachedAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public Task<SteamReferenceSnapshot> RefreshStaleAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public Task<SteamReferenceSnapshot> RefreshAllAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(Current);
    }

    private sealed class BlockingSteamRuntime :
        ISteamReferenceRuntime
    {
        private readonly SteamReferenceSnapshot _final;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingSteamRuntime(
            SteamReferenceSnapshot initial,
            SteamReferenceSnapshot final)
        {
            Current = initial;
            _final = final;
        }

        public TaskCompletionSource RefreshStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RefreshAllCount { get; private set; }

        public SteamReferenceSnapshot Current
        {
            get;
            private set;
        }

        public void Release()
            => _release.TrySetResult();

        public Task<SteamReferenceSnapshot> LoadCachedAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public Task<SteamReferenceSnapshot> RefreshStaleAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public async Task<SteamReferenceSnapshot> RefreshAllAsync(
            CancellationToken cancellationToken)
        {
            RefreshAllCount++;
            RefreshStarted.TrySetResult();

            await _release.Task.WaitAsync(
                cancellationToken);

            Current = _final;

            return Current;
        }
    }
}
