using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailActivityTests
{
    [Fact]
    public void Activity_module_uses_existing_projection_and_collapses_without_history()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.Contains(
            "x:Name=\"GameActivity\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Activity.HasSessionHistory",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Activity.LastSessionDateLabel",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Activity.TotalPlayTimeLabel",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Activity.SessionCountLabel",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "BooleanToVisibilityConverter",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_keeps_activity_scoped_to_the_detail_game()
    {
        var gameId =
            Guid.Parse("11111111-2222-3333-4444-555555555555");

        var otherGameId =
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var targetSession =
            EndedSession(gameId, TimeSpan.FromHours(1));

        var otherSession =
            EndedSession(otherGameId, TimeSpan.FromHours(4));

        var item =
            Item(gameId);

        var activity =
            new GameQuickPanelViewModel(
                item,
                new NavigationService(),
                launch: null,
                new FakeSessionStore([targetSession, otherSession]),
                new FakeCorrectionStore(),
                new SessionCorrectionPolicy());

        var detail =
            new GameDetailViewModel(
                item,
                launch: null,
                activity);

        await detail.LoadAsync(CancellationToken.None);

        Assert.True(activity.HasSessionHistory);
        Assert.Equal("1 session", activity.SessionCountLabel);
        Assert.Equal("1 h 00 min", activity.TotalPlayTimeLabel);
    }

    [Fact]
    public async Task LoadAsync_exposes_no_activity_module_data_without_sessions()
    {
        var item =
            Item(Guid.NewGuid());

        var activity =
            new GameQuickPanelViewModel(
                item,
                new NavigationService(),
                launch: null,
                new FakeSessionStore([]),
                new FakeCorrectionStore(),
                new SessionCorrectionPolicy());

        var detail =
            new GameDetailViewModel(
                item,
                launch: null,
                activity);

        await detail.LoadAsync(CancellationToken.None);

        Assert.False(activity.HasSessionHistory);
        Assert.Equal("0 session", activity.SessionCountLabel);
        Assert.Equal("0 min", activity.TotalPlayTimeLabel);
    }

    private static LibraryItemViewModel Item(Guid gameId) =>
        new(
            new GameId(gameId),
            "Game",
            ProviderKind.Steam,
            "Steam",
            @"C:\Games\Game",
            null);

    private static GameSession EndedSession(Guid gameId, TimeSpan duration)
    {
        var endedAt =
            new DateTimeOffset(
                2026, 9, 10, 18, 0, 0,
                TimeSpan.Zero);

        return new GameSession(
            Guid.NewGuid(),
            gameId,
            endedAt - duration,
            endedAt,
            endedAt,
            SessionState.Ended,
            SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            endedAt - duration,
            endedAt);
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

    private sealed class FakeSessionStore(
        IReadOnlyList<GameSession> sessions) : ISessionStore
    {
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<GameSession?>(sessions.FirstOrDefault(item => item.SessionId == sessionId));

        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);

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
