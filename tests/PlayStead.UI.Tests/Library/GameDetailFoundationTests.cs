using PlayStead.Core.Library;
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
        Assert.Equal("1 session", activity.SessionCountLabel);
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
}
