using PlayStead.Core.Library;
using PlayStead.Core.GameBuildHistory;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Launching;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailFoundationTests
{
    [Fact]
    public void Projects_the_library_item_and_local_availability_flags()
    {
        var item =
            new LibraryItemViewModel(
                GameId.New(),
                "Game",
                ProviderKind.Steam,
                "Steam",
                @"C:\Games\Game",
                1_000_000,
                SteamUpdateState.UpToDate);

        item.SetCoverPath(@"C:\Cache\cover.jpg");

        var viewModel =
            new GameDetailViewModel(
                item);

        Assert.Same(item, GetProperty(viewModel, "Game"));
        Assert.True((bool)GetProperty(viewModel, "HasCover")!);
        Assert.Equal(@"C:\Cache\cover.jpg", GetProperty(viewModel, "CoverPath"));
        Assert.True((bool)GetProperty(viewModel, "HasInstallPath")!);
        Assert.True((bool)GetProperty(viewModel, "HasInstalledSize")!);
        Assert.True((bool)GetProperty(viewModel, "HasSteamStatus")!);
    }

    [Fact]
    public void Preserves_existing_launch_constructor_and_exposes_activity()
    {
        var item =
            new LibraryItemViewModel(
                GameId.New(),
                "Game",
                ProviderKind.Steam,
                "Steam",
                string.Empty,
                null);

        var launch =
            new GameLaunchViewModel(
                item.GameId,
                Array.Empty<GameInstallation>(),
                new GameLaunchService(
                    new FakeExternalUriLauncher()));

        var activity =
            new GameQuickPanelViewModel(
                item,
                new NavigationService(),
                launch);

        var viewModel =
            CreateThreeArgumentViewModel(item, launch, activity);

        Assert.Same(launch, GetProperty(viewModel, "Launch"));
        Assert.Same(activity, GetProperty(viewModel, "Activity"));
        Assert.False((bool)GetProperty(viewModel, "HasInstallPath")!);
        Assert.False((bool)GetProperty(viewModel, "HasInstalledSize")!);
    }

    [Fact]
    public async Task LoadAsync_delegates_to_the_existing_activity_projection()
    {
        var gameId =
            Guid.Parse("11111111-2222-3333-4444-555555555555");

        var endedAt =
            new DateTimeOffset(
                2026, 9, 10, 18, 0, 0,
                TimeSpan.Zero);

        var session =
            new GameSession(
                Guid.NewGuid(),
                gameId,
                endedAt.AddHours(-1),
                endedAt,
                endedAt,
                SessionState.Ended,
                SessionEndReason.ProcessExited,
                SessionDetectionSource.ProcessMonitor,
                endedAt.AddHours(-1),
                endedAt);

        var item =
            new LibraryItemViewModel(
                new GameId(gameId),
                "Game",
                ProviderKind.Steam,
                "Steam",
                @"C:\Games\Game",
                null);

        var activity =
            new GameQuickPanelViewModel(
                item,
                new NavigationService(),
                launch: null,
                new FakeSessionStore([session]),
                new FakeCorrectionStore(),
                new SessionCorrectionPolicy());

        var viewModel =
            CreateThreeArgumentViewModel(item, null, activity);

        await InvokeLoadAsync(viewModel, CancellationToken.None);

        Assert.True(activity.HasSessionHistory);
        Assert.Equal("1 session connue", activity.SessionCountLabel);
    }

    [Fact]
    public async Task LoadAsync_projects_build_history_newest_first_and_marks_changes_since_last_play()
    {
        var gameId = GameId.New();
        var first = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var lastPlay = first.AddDays(1);
        var second = first.AddDays(2);
        var history = new[]
        {
            new GameBuildObservation(gameId, ProviderKind.Steam, "123", "100", first),
            new GameBuildObservation(gameId, ProviderKind.Steam, "123", "101", second)
        };
        var sessions = new[]
        {
            new GameSession(Guid.NewGuid(), gameId.Value, lastPlay.AddHours(-1), lastPlay,
                lastPlay, SessionState.Ended, SessionEndReason.ProcessExited,
                SessionDetectionSource.ProcessMonitor, lastPlay.AddHours(-1), lastPlay)
        };
        var service = new GameBuildHistoryService(new FakeBuildHistoryStore(history), new FakeSessionStore(sessions));
        var item = new LibraryItemViewModel(gameId, "Game", ProviderKind.Steam, "Steam", string.Empty, null);
        var viewModel = new GameDetailViewModel(item, null, null, null, null, null, null, service);

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.Equal(2, viewModel.BuildHistory.Count);
        Assert.Equal("101", viewModel.BuildHistory[0].BuildId);
        Assert.Equal("100 → 101", viewModel.BuildHistory[0].BuildTransitionLabel);
        Assert.True(viewModel.BuildHistory[0].IsSinceLastPlay);
        Assert.Equal(1, viewModel.BuildChangeCountSinceLastPlay);
        Assert.Contains("depuis ta dernière partie", viewModel.BuildHistorySummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_history_uses_playstead_voice_for_first_reference_and_empty_change_state()
    {
        var entry = new GameDetailViewModel.BuildHistoryEntryViewModel(
            null,
            "25480438",
            DateTimeOffset.UtcNow,
            true,
            false);

        Assert.Equal("Premier point de repère · 25480438", entry.BuildTransitionLabel);
        Assert.Contains("Rien n’a bougé depuis que je garde un œil dessus.",
            "Rien n’a bougé depuis que je garde un œil dessus.",
            StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_route_supplies_the_activity_projection()
    {
        var source =
            File.ReadAllText(
                FindUiFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "CreateActivityModel",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "CreateHeroPath",
            source,
            StringComparison.Ordinal);

        Assert.Contains("var activity = CreateActivityModel(libraryViewModel, gameId);", source, StringComparison.Ordinal);
    }

    private static object? GetProperty(
        object instance,
        string name) =>
        instance.GetType().GetProperty(name)?.GetValue(instance);

    private static GameDetailViewModel CreateThreeArgumentViewModel(
        LibraryItemViewModel item,
        GameLaunchViewModel? launch,
        GameQuickPanelViewModel? activity)
    {
        var constructor =
            typeof(GameDetailViewModel).GetConstructor(
                [
                    typeof(LibraryItemViewModel),
                    typeof(GameLaunchViewModel),
                    typeof(GameQuickPanelViewModel)
                ]);

        Assert.NotNull(constructor);

        return (GameDetailViewModel)constructor!.Invoke(
            [item, launch, activity]);
    }

    private static async Task InvokeLoadAsync(
        GameDetailViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var method =
            typeof(GameDetailViewModel).GetMethod("LoadAsync");

        Assert.NotNull(method);

        var task =
            (Task)method!.Invoke(
                viewModel,
                [cancellationToken])!;

        await task;
    }

    private static string FindUiFile(string relativePath)
    {
        var directory =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(directory.FullName, "src", "PlayStead.UI");

            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }

    private sealed class FakeExternalUriLauncher : IExternalUriLauncher
    {
        public void Open(Uri uri)
        {
        }
    }

    private sealed class FakeSessionStore(
        IReadOnlyList<GameSession> sessions) : ISessionStore
    {
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<GameSession?>(sessions.FirstOrDefault(item => item.SessionId == sessionId));

        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(sessions.Where(item => item.State == SessionState.Active).ToArray());

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(sessions.Take(limit).ToArray());

        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(sessions.Where(item => item.GameId == gameId).ToArray());
    }

    private sealed class FakeCorrectionStore : ISessionCorrectionStore
    {
        public Task UpsertAsync(SessionCorrection correction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SessionCorrection?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<SessionCorrection?>(null);
    }

    private sealed class FakeBuildHistoryStore(IReadOnlyList<GameBuildObservation> history) : IGameBuildHistoryStore
    {
        public Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<GameBuildObservation?>(history.Where(x => x.GameId == gameId && x.Provider == provider).OrderByDescending(x => x.ObservedAtUtc).FirstOrDefault());

        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(history.Where(x => x.GameId == gameId && x.Provider == provider).ToArray());

        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(IReadOnlyCollection<GameId> gameIds, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(history.Where(x => gameIds.Contains(x.GameId) && x.Provider == provider).ToArray());

        public Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
