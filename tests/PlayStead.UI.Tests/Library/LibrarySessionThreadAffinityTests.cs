using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.UI.Home;
using PlayStead.UI.Launching;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Shell;
using PlayStead.UI.State;
using PlayStead.UI.Tray;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Library;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class LibrarySessionThreadAffinityTests
{
    private static readonly GameId GameId =
        new(Guid.Parse("11111111-aaaa-4444-8888-111111111111"));

    private static readonly DateTimeOffset Now =
        new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public Task Snapshot_publication_returns_before_library_projection_is_applied() =>
        PlaySteadWpfTestResources.RunAsync(async () =>
    {
            using var monitor = CreateMonitor(new CancellationTokenSource());
            var library = new LibraryViewModel(new LibraryStore(), monitor);
            await library.RefreshAsync(CancellationToken.None);

            Assert.False(Assert.Single(library.Items).IsSessionActive);

            var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            library.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(LibraryViewModel.Items))
                    applied.TrySetResult();
            };

            PublishSnapshot(monitor, CreateSnapshot(GameId));

            // The snapshot callback must not perform the projection inline on the UI thread.
            Assert.False(Assert.Single(library.Items).IsSessionActive);

            await applied.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(Assert.Single(library.Items).IsSessionActive);
        });

    [Fact]
    public Task Background_snapshots_reach_MainWindow_on_UI_thread_without_blocking_monitor() =>
        PlaySteadWpfTestResources.RunAsync(async () =>
    {
            var dispatcher = Dispatcher.CurrentDispatcher;
            using var monitor = CreateMonitor(new CancellationTokenSource());
            var store = new LibraryStore();
            var library = new LibraryViewModel(store, monitor);
            await library.RefreshAsync(CancellationToken.None);
            library.SelectGame(Assert.Single(library.Items));

            var navigation = new NavigationService();
            var sessions = new SessionViewModel(store, monitor, TimeProvider.System);
            var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
            var closePolicy = new WindowClosePolicy();
            var window = new MainWindow(library,
                new WindowPlacementService(Path.Combine(root, "window.json")), closePolicy,
                sessions, navigation, new ShellViewModel(navigation),
                new SettingsViewModel(new UiPreferencesStore(Path.Combine(root, "preferences.json"))),
                new UiMotionController(), new HomeViewModel(library, sessions, navigation),
                new GameLaunchService(new NeverLaunch()));

            try
            {
                window.Left = 100;
                window.Top = 100;
                navigation.Navigate(new NavigationRequest(AppRoute.Library));
                var view = Assert.IsType<LibraryView>(
                    Assert.IsType<ContentControl>(window.FindName("MainContent")).Content);
                var notificationThreads = new List<bool>();
                var activeProjectionApplied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                library.PropertyChanged += (_, e) =>
                {
                    // Check every notification, not only the selected-item callback.
                    notificationThreads.Add(dispatcher.CheckAccess());
                    if (e.PropertyName == nameof(LibraryViewModel.SelectedItem))
                    {
                        var selected = Assert.IsType<LibraryItemViewModel>(library.SelectedItem);
                        Assert.Equal(GameId, selected.GameId);
                        Assert.Same(Assert.Single(library.Items), selected);
                        Assert.Same(selected, view.QuickPanelViewModel!.Game);
                        if (selected.IsSessionActive)
                            activeProjectionApplied.TrySetResult();
                    }
                };

                // The monitor can publish faster than the UI applies projections; the latest
                // snapshot wins while every applied notification remains on the UI thread.
                await Task.Run(() => PublishSnapshot(monitor, CreateSnapshot(GameId)));
                await activeProjectionApplied.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Task.Run(() => PublishSnapshot(monitor, new SessionRuntimeSnapshot(Now.AddSeconds(2), [])));
                await Task.Delay(100);

                Assert.NotEmpty(notificationThreads);
                Assert.All(notificationThreads, onUiThread => Assert.True(onUiThread));
                Assert.False(Assert.Single(library.Items).IsSessionActive);
                Assert.Equal("Test Game", library.SelectedItem!.Title);
            }
            finally
            {
                closePolicy.RequestExit();
                window.Close();
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });

    private static SessionMonitor CreateMonitor(CancellationTokenSource cancellation)
    {
        var session = new GameSession(Guid.NewGuid(), GameId.Value,
            Now, Now, null, SessionState.Active, null,
            SessionDetectionSource.ProcessMonitor, Now, Now);
        var runtime = new SnapshotRuntime(new Queue<SessionRuntimeSnapshot>(
            [new(Now, [session]), new(Now.AddSeconds(2), [])]));
        var publications = 0;
        return new SessionMonitor(runtime, SessionMonitorOptions.Default, (_, _) =>
        {
            if (++publications == 2) cancellation.Cancel();
            return Task.CompletedTask;
        });
    }

    private static void PublishSnapshot(SessionMonitor monitor, SessionRuntimeSnapshot snapshot)
    {
        var handlers = (Action<SessionRuntimeSnapshot>?)typeof(SessionMonitor)
            .GetField("SnapshotUpdated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(monitor);
        Assert.NotNull(handlers);
        handlers!(snapshot);
    }

    private static SessionRuntimeSnapshot CreateSnapshot(GameId activeGameId)
    {
        var session = new GameSession(Guid.NewGuid(), activeGameId.Value,
            Now, Now, null, SessionState.Active, null,
            SessionDetectionSource.ProcessMonitor, Now, Now);
        return new SessionRuntimeSnapshot(Now, [session]);
    }

    private sealed class SnapshotRuntime(Queue<SessionRuntimeSnapshot> snapshots) : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshots.Dequeue());
        }

        public Task CorrectSessionAsync(SessionCorrectionRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class LibraryStore : ILibraryStore
    {
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult(new LibrarySnapshot(
                [new LogicalGame(GameId, "Test Game", false, Now, Now)],
                [new GameInstallation(InstallationId.New(), GameId, ProviderKind.Steam,
                    "123", @"C:\Games\TestGame", 100, true, true, Now)]));

        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class NeverLaunch : IExternalUriLauncher
    {
        public void Open(Uri uri) => throw new InvalidOperationException("This test must not launch a game.");
    }
}
