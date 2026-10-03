using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.ProviderActivity;
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
    public void Activity_module_stays_visible_and_exposes_no_activity_state()
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

        Assert.DoesNotContain(
            "Visibility=\"{Binding Activity.HasAnyActivity",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Activity.HasNoActivity",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Je n’ai encore aucune session connue pour ce jeu.",
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
        Assert.Equal("1 session connue", activity.SessionCountLabel);
        Assert.Equal("Inconnu", activity.TotalPlayTimeLabel);
        Assert.False(activity.HasNoActivity);
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
        Assert.Equal("0 session connue", activity.SessionCountLabel);
        Assert.Equal("Inconnu", activity.TotalPlayTimeLabel);
        Assert.True(activity.HasNoActivity);
    }

    [Fact]
    public void Game_activity_card_shows_empty_state_without_activity()
    {
        RunSta(() =>
        {
            var item = Item(Guid.NewGuid());
            var activity = CreateActivity(
                item,
                new FakeSessionStore([]),
                new FakeCorrectionStore([]));
            var detail = new GameDetailViewModel(item, launch: null, activity);
            var view = new GameDetailView(detail);
            var window = new Window
            {
                Content = view,
                Width = 1200,
                Height = 900,
                ShowInTaskbar = false,
                ShowActivated = false
            };

            try
            {
                window.Show();
                detail.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
                Drain(view.Dispatcher);

                var card = Assert.IsType<Border>(view.FindName("GameActivity"));
                var message = Assert.IsType<TextBlock>(view.FindName("NoActivityMessage"));
                Assert.Equal(Visibility.Visible, card.Visibility);
                Assert.Equal(Visibility.Visible, message.Visibility);
                Assert.Equal(
                    "Je n’ai encore aucune session connue pour ce jeu.",
                    message.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task LoadAsync_projects_complete_provider_observed_session_when_playstead_was_closed()
    {
        var gameId = Guid.NewGuid();
        var started = new DateTimeOffset(2026, 9, 28, 7, 52, 13, TimeSpan.Zero);
        var ended = new DateTimeOffset(2026, 9, 28, 9, 51, 27, TimeSpan.Zero);
        var observed = new ProviderObservedSession(
            Guid.NewGuid(), new GameId(gameId), ProviderKind.Steam, "1172710",
            started, ended, "SteamProcessLog", ProviderObservedSessionCompleteness.Complete);
        var item = Item(gameId);
        var activity = new GameQuickPanelViewModel(
            item, new NavigationService(), launch: null,
            new FakeSessionStore([]), new FakeCorrectionStore(), new SessionCorrectionPolicy(),
            providerSessionStore: new FakeProviderObservedSessionStore([observed]));

        await new GameDetailViewModel(item, launch: null, activity).LoadAsync(CancellationToken.None);

        Assert.True(activity.HasRecentActivity);
        Assert.Equal("1 session connue", activity.SessionCountLabel);
        Assert.Equal("0 min", activity.PlaySteadTotalPlayTimeLabel);
        Assert.Equal("Inconnu", activity.TotalPlayTimeLabel);
        Assert.Single(activity.RecentActivitySessions);
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
    public void Recent_activity_card_binding_tracks_data_and_shows_empty_state()
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
                Assert.Equal(Visibility.Visible, card.Visibility);
                Assert.True(activity.HasNoRecentActivity);

                store.Replace([CompletedSession(gameId, DateTimeOffset.Now.AddHours(-2), TimeSpan.FromMinutes(80))]);
                detail.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
                Drain(view.Dispatcher);
                Assert.Equal(Visibility.Visible, card.Visibility);
                Assert.False(activity.HasNoRecentActivity);

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

    [Fact]
    public void Cover_changes_are_relayed_only_while_detail_is_active()
    {
        var item = Item(Guid.NewGuid());
        var detail = new GameDetailViewModel(item, launch: null, activity: null);
        var changed = new List<string?>();
        detail.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        detail.Activate();
        item.SetCoverPath(@"C:\Cache\cover.jpg");

        Assert.Contains(nameof(GameDetailViewModel.CoverPath), changed);
        Assert.Contains(nameof(GameDetailViewModel.HasCover), changed);
        Assert.True(detail.HasCover);

        changed.Clear();
        detail.Deactivate();
        item.SetCoverPath(null);

        Assert.Empty(changed);
    }

    [Fact]
    public void Reactivating_detail_does_not_duplicate_cover_notifications()
    {
        var item = Item(Guid.NewGuid());
        var detail = new GameDetailViewModel(item, launch: null, activity: null);
        var coverChanges = 0;
        detail.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GameDetailViewModel.CoverPath))
                coverChanges++;
        };

        detail.Activate();
        detail.Deactivate();
        detail.Activate();
        item.SetCoverPath(@"C:\Cache\cover.jpg");

        Assert.Equal(1, coverChanges);
        detail.Deactivate();
    }

    [Fact]
    public void Session_game_replacement_rebinds_cover_notifications()
    {
        PlaySteadWpfTestResources.Run(() =>
        {
            var gameId = Guid.NewGuid();
            var oldItem = Item(gameId);
            var monitor = new SessionMonitor(new NoopRuntime(), SessionMonitorOptions.Default, (_, _) => Task.CompletedTask);
            var detail = new GameDetailViewModel(oldItem, null, null, null, monitor, () => Task.CompletedTask);
            var changed = new List<string?>();
            detail.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            detail.Activate();
            Publish(monitor, new SessionRuntimeSnapshot(DateTimeOffset.UtcNow,
                [new GameSession(Guid.NewGuid(), gameId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null,
                    SessionState.Active, null, SessionDetectionSource.ProcessMonitor, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]));

            changed.Clear();
            oldItem.SetCoverPath(@"C:\Cache\old.jpg");
            Assert.DoesNotContain(nameof(GameDetailViewModel.CoverPath), changed);

            changed.Clear();
            detail.Game.SetCoverPath(@"C:\Cache\new.jpg");
            Assert.Contains(nameof(GameDetailViewModel.CoverPath), changed);
            detail.Deactivate();
        });
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

    private sealed class FakeProviderObservedSessionStore(IReadOnlyList<ProviderObservedSession> sessions) : IProviderObservedSessionStore
    {
        public Task UpsertAsync(ProviderObservedSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            CancellationToken cancellationToken) => Task.FromResult(sessions);
    }

    private sealed class NoopRuntime : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) => Task.FromResult(new SessionRuntimeSnapshot(DateTimeOffset.Now, []));
        public Task CorrectSessionAsync(SessionCorrectionRequest correction, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
