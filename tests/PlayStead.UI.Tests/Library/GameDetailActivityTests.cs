using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Library;

[Collection(PlaySteadWpfApplicationCollection.Name)]
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

    [Fact]
    public async Task LoadAsync_projects_only_the_five_newest_reliable_sessions_for_the_selected_game()
    {
        var gameId = Guid.NewGuid();
        var sessions = Enumerable.Range(0, 7)
            .Select(index => CompletedSession(gameId, DateTimeOffset.Now.AddDays(-index), TimeSpan.FromMinutes(30 + index)))
            .ToArray();
        var active = new GameSession(Guid.NewGuid(), gameId, DateTimeOffset.Now, DateTimeOffset.Now, null,
            SessionState.Active, null, SessionDetectionSource.ProcessMonitor, DateTimeOffset.Now, DateTimeOffset.Now);
        var invalid = CompletedSession(gameId, DateTimeOffset.Now.AddDays(-10), TimeSpan.Zero);
        var otherGame = CompletedSession(Guid.NewGuid(), DateTimeOffset.Now.AddMinutes(1), TimeSpan.FromHours(4));
        var correctedStart = sessions[0].ObservedStartedAtUtc.AddMinutes(15);
        var correctedEnd = correctedStart.AddMinutes(45);
        var correction = new SessionCorrection(Guid.NewGuid(), sessions[0].SessionId, correctedStart, correctedEnd, null, DateTimeOffset.Now);
        var store = new FakeSessionStore([.. sessions.Reverse(), sessions[0], active, invalid, otherGame], returnUnscoped: true);
        var corrections = new FakeCorrectionStore([correction]);
        var item = Item(gameId);
        var activity = CreateActivity(item, store, corrections);

        await new GameDetailViewModel(item, launch: null, activity).LoadAsync(CancellationToken.None);

        Assert.True(ReadHasRecentActivity(activity));
        var rows = ReadRecentRows(activity);
        Assert.Equal(5, rows.Count);
        Assert.Equal(sessions.Take(5).Select(session => session.SessionId), rows.Select(row => row.SessionId));
        Assert.All(rows, row => Assert.Contains(sessions.Take(5).Select(session => session.SessionId), id => id == row.SessionId));
        Assert.Equal(GameQuickPanelViewModel.FormatTimestampForDisplay(correctedStart, DateTimeOffset.Now), rows[0].StartedAtLabel);
        Assert.Equal("45 min", rows[0].DurationLabel);
        Assert.Equal("34 min", rows[4].DurationLabel);
        Assert.DoesNotContain(rows, row => row.SessionId == active.SessionId || row.SessionId == invalid.SessionId || row.SessionId == otherGame.SessionId);
    }

    [Fact]
    public async Task No_completed_reliable_sessions_hides_recent_activity_even_if_a_session_is_active()
    {
        var gameId = Guid.NewGuid();
        var active = new GameSession(Guid.NewGuid(), gameId, DateTimeOffset.Now, DateTimeOffset.Now, null,
            SessionState.Active, null, SessionDetectionSource.ProcessMonitor, DateTimeOffset.Now, DateTimeOffset.Now);
        var item = Item(gameId);
        var activity = CreateActivity(item, new FakeSessionStore([active]), new FakeCorrectionStore([]));

        await new GameDetailViewModel(item, launch: null, activity).LoadAsync(CancellationToken.None);

        Assert.False(ReadHasRecentActivity(activity));
        Assert.Empty(ReadRecentRows(activity));
    }

    [Fact]
    public void Recent_activity_card_binding_tracks_data_and_collapses_when_empty()
    {
        RunSta(() =>
        {
            var gameId = Guid.NewGuid();
            var item = Item(gameId);
            var store = new FakeSessionStore([]);
            var activity = CreateActivity(item, store, new FakeCorrectionStore([]));
            var detail = new GameDetailViewModel(item, launch: null, activity);
            var view = new GameDetailView(detail);
            var window = new Window { Content = view, Width = 1200, Height = 900, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                Drain(view.Dispatcher);
                var card = Assert.IsType<Border>(view.FindName("RecentActivity"));
                Assert.Equal(Visibility.Collapsed, card.Visibility);

                store.Replace([CompletedSession(gameId, DateTimeOffset.Now.AddHours(-2), TimeSpan.FromMinutes(80))]);
                detail.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
                Drain(view.Dispatcher);
                Assert.Equal(Visibility.Visible, card.Visibility);

                var rows = Assert.IsType<ItemsControl>(view.FindName("RecentActivityItems"));
                Assert.Single(rows.Items);
                Assert.Equal("1 h 20 min", ReadRecentRows(activity)[0].DurationLabel);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public Task Live_session_completion_refreshes_recent_activity_without_navigation() =>
        PlaySteadWpfTestResources.RunAsync(async () =>
    {
        var gameId = Guid.NewGuid();
        var active = new GameSession(Guid.NewGuid(), gameId, DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, null,
            SessionState.Active, null, SessionDetectionSource.ProcessMonitor, DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
        var completed = active with { ObservedEndedAtUtc = DateTimeOffset.Now, State = SessionState.Ended, EndReason = SessionEndReason.ProcessExited };
        var store = new FakeSessionStore([active]);
        var item = Item(gameId);
        var activity = CreateActivity(item, store, new FakeCorrectionStore([]));
        var monitor = new SessionMonitor(new NoopRuntime(), SessionMonitorOptions.Default, (_, _) => Task.CompletedTask);
        var detail = new GameDetailViewModel(item with { IsSessionActive = true }, null, activity, null, monitor);
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "RecentActivitySessions") refreshed.TrySetResult();
        };

        await detail.LoadAsync(CancellationToken.None);
        Assert.False(ReadHasRecentActivity(activity));
        detail.Activate();
        store.Replace([completed]);
        Publish(monitor, new SessionRuntimeSnapshot(DateTimeOffset.Now, []));
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(ReadHasRecentActivity(activity));
        Assert.Single(ReadRecentRows(activity));
        Assert.False(detail.Game.IsSessionActive);
        detail.Deactivate();
    });

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

    private static GameSession CompletedSession(Guid gameId, DateTimeOffset startedAt, TimeSpan duration) =>
        new(Guid.NewGuid(), gameId, startedAt, startedAt + duration, startedAt + duration,
            SessionState.Ended, SessionEndReason.ProcessExited, SessionDetectionSource.ProcessMonitor,
            startedAt, startedAt + duration);

    private static GameQuickPanelViewModel CreateActivity(
        LibraryItemViewModel item,
        FakeSessionStore store,
        FakeCorrectionStore corrections) =>
        new(item, new NavigationService(), launch: null, store, corrections, new SessionCorrectionPolicy());

    private static bool ReadHasRecentActivity(GameQuickPanelViewModel activity) =>
        activity.HasRecentActivity;

    private static List<RecentRow> ReadRecentRows(GameQuickPanelViewModel activity)
        => activity.RecentActivitySessions
            .Select(row => new RecentRow(row.SessionId, row.StartedAtLabel, row.DurationLabel))
            .ToList();

    private static void Publish(SessionMonitor monitor, SessionRuntimeSnapshot snapshot)
    {
        var publish = (Action<SessionRuntimeSnapshot>?)typeof(SessionMonitor)
            .GetField("SnapshotUpdated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(monitor);
        publish?.Invoke(snapshot);
    }

    private static void Drain(Dispatcher dispatcher) => dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void RunSta(Action action) => PlaySteadWpfTestResources.Run(action);

    private sealed record RecentRow(Guid SessionId, string StartedAtLabel, string DurationLabel);

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
        IReadOnlyList<GameSession> sessions,
        bool returnUnscoped = false) : ISessionStore
    {
        private IReadOnlyList<GameSession> _sessions = sessions;
        public void Replace(IReadOnlyList<GameSession> value) => _sessions = value;
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<GameSession?>(_sessions.FirstOrDefault(item => item.SessionId == sessionId));

        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(_sessions.Take(limit).ToArray());

        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(returnUnscoped ? _sessions : _sessions.Where(item => item.GameId == gameId).ToArray());
    }

    private sealed class FakeCorrectionStore(IReadOnlyList<SessionCorrection> corrections) : ISessionCorrectionStore
    {
        public FakeCorrectionStore() : this([]) { }
        public Task UpsertAsync(SessionCorrection correction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SessionCorrection?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<SessionCorrection?>(corrections.FirstOrDefault(item => item.SessionId == sessionId));
    }

    private sealed class NoopRuntime : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) => Task.FromResult(new SessionRuntimeSnapshot(DateTimeOffset.Now, []));
        public Task CorrectSessionAsync(SessionCorrectionRequest correction, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
