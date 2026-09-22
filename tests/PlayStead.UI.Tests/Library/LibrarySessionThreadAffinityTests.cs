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
    public void Background_snapshots_reach_MainWindow_on_UI_thread_in_order_without_blocking_monitor()
    {
        PlaySteadWpfTestResources.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            using var cancellation = new CancellationTokenSource();
            using var monitor = CreateMonitor(cancellation);
            var store = new LibraryStore();
            var library = new LibraryViewModel(store, monitor);
            library.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
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
                var observedStates = new List<bool>();
                var notificationThreads = new List<bool>();
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
                        observedStates.Add(selected.IsSessionActive);
                    }
                };

                // The UI deliberately does not pump until both background publications finish.
                // A synchronous Dispatcher.Invoke would deadlock here and hit the timeout.
                Task.Run(() => monitor.RunAsync(cancellation.Token))
                    .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                Assert.NotEmpty(notificationThreads);
                Assert.All(notificationThreads, onUiThread => Assert.True(onUiThread));
                Assert.Equal(new[] { true, false }, observedStates);
                Assert.False(Assert.Single(library.Items).IsSessionActive);
                Assert.Equal("Test Game", library.SelectedItem!.Title);
                Assert.Empty(monitor.LatestSnapshot!.ActiveSessions);
            }
            finally
            {
                closePolicy.RequestExit();
                window.Close();
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

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
