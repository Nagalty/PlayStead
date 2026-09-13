using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionWpfResilienceTests
{
    [Fact]
    [Trait("Task11Cycle", "C3")]
    public void C3_page_load_without_history_engine_does_not_crash()
    {
        RunSta(() =>
        {
            var runtime =
                new NoopSessionRuntime();

            var monitor =
                new SessionMonitor(
                    runtime,
                    SessionMonitorOptions.Default);

            var viewModel =
                new SessionViewModel(
                    new EmptyLibraryStore(),
                    monitor,
                    TimeProvider.System);

            var view =
                new SessionsView
                {
                    DataContext =
                        viewModel
                };

            var unhandled =
                RaiseLoadedAndCaptureUnhandled(
                    view);

            Assert.Null(
                unhandled);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C3")]
    public void C3_reopening_page_reloads_recent_history()
    {
        RunSta(() =>
        {
            var sessionStore =
                new CountingSessionStore();

            var runtime =
                new NoopSessionRuntime();

            var monitor =
                new SessionMonitor(
                    runtime,
                    SessionMonitorOptions.Default);

            var viewModel =
                new SessionViewModel(
                    new EmptyLibraryStore(),
                    monitor,
                    TimeProvider.System,
                    sessionStore,
                    new EmptyCorrectionStore(),
                    runtime,
                    new SessionCorrectionPolicy());

            var view =
                new SessionsView
                {
                    DataContext =
                        viewModel
                };

            var firstUnhandled =
                RaiseLoadedAndCaptureUnhandled(
                    view);

            var secondUnhandled =
                RaiseLoadedAndCaptureUnhandled(
                    view);

            Assert.Null(
                firstUnhandled);

            Assert.Null(
                secondUnhandled);

            Assert.Equal(
                2,
                sessionStore.GetRecentCallCount);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C3")]
    public void C3_refresh_projection_updates_visible_history_and_selected_detail()
    {
        RunSta(() =>
        {
            var initialDetail =
                new object();

            var refreshedDetail =
                new object();

            var state =
                new MutableSessionsState(
                    [
                        new FakeRecentSession(
                            "Test",
                            "1:00:00")
                    ],
                    initialDetail);

            var view =
                new SessionsView
                {
                    DataContext =
                        state
                };

            FlushBindings(
                view);

            var host =
                Assert.IsType<ContentControl>(
                    view.FindName(
                        "SessionDetailHost"));

            var historyHost =
                Assert.IsType<Border>(
                    view.FindName(
                        "RecentHistoryHost"));

            var list =
                FindLogicalDescendant<ItemsControl>(
                    historyHost);

            Assert.NotNull(
                list);

            Assert.Same(
                initialDetail,
                host.Content);

            Assert.Equal(
                "1:00:00",
                Assert.IsType<FakeRecentSession>(
                    Assert.Single(
                        list!.Items))
                    .DurationLabel);

            state.ApplyCorrectionProjection(
                [
                    new FakeRecentSession(
                        "Test",
                        "1:30:00")
                ],
                refreshedDetail);

            FlushBindings(
                view);

            Assert.Same(
                refreshedDetail,
                host.Content);

            Assert.Equal(
                "1:30:00",
                Assert.IsType<FakeRecentSession>(
                    Assert.Single(
                        list.Items))
                    .DurationLabel);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C3")]
    public void C3_page_load_read_error_is_handled_without_dispatcher_crash()
    {
        RunSta(() =>
        {
            var runtime =
                new NoopSessionRuntime();

            var monitor =
                new SessionMonitor(
                    runtime,
                    SessionMonitorOptions.Default);

            var viewModel =
                new SessionViewModel(
                    new ThrowingLibraryStore(),
                    monitor,
                    TimeProvider.System);

            var view =
                new SessionsView
                {
                    DataContext =
                        viewModel
                };

            var unhandled =
                RaiseLoadedAndCaptureUnhandled(
                    view);

            Assert.Null(
                unhandled);
        });
    }

    private static Exception?
        RaiseLoadedAndCaptureUnhandled(
            SessionsView view)
    {
        var dispatcher =
            Dispatcher.CurrentDispatcher;

        Exception? captured =
            null;

        DispatcherUnhandledExceptionEventHandler
            handler =
                (_, args) =>
                {
                    captured =
                        args.Exception;

                    args.Handled =
                        true;
                };

        dispatcher.UnhandledException +=
            handler;

        try
        {
            view.RaiseEvent(
                new RoutedEventArgs(
                    FrameworkElement.LoadedEvent));

            PumpDispatcher(
                dispatcher);

            return captured;
        }
        finally
        {
            view.RaiseEvent(
                new RoutedEventArgs(
                    FrameworkElement.UnloadedEvent));

            dispatcher.UnhandledException -=
                handler;
        }
    }

    private static void PumpDispatcher(
        Dispatcher dispatcher)
    {
        for (var index = 0;
             index < 3;
             index++)
        {
            var frame =
                new DispatcherFrame();

            dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(
                    () =>
                        frame.Continue =
                            false));

            Dispatcher.PushFrame(
                frame);
        }
    }

    private static void FlushBindings(
        FrameworkElement element)
    {
        element.ApplyTemplate();
        element.UpdateLayout();

        Dispatcher.CurrentDispatcher.Invoke(
            () => { },
            DispatcherPriority.DataBind);

        element.UpdateLayout();
    }

    private static T? FindLogicalDescendant<T>(
        DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in
                 LogicalTreeHelper.GetChildren(
                     root))
        {
            if (child is T match)
            {
                return match;
            }

            if (child is
                DependencyObject dependencyObject)
            {
                var nested =
                    FindLogicalDescendant<T>(
                        dependencyObject);

                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static void RunSta(
        Action action)
    {
        Exception? failure =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        SynchronizationContext
                            .SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                        action();
                    }
                    catch (Exception exception)
                    {
                        failure =
                            exception;
                    }
                });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(
                    failure)
                .Throw();
        }
    }

    private sealed record FakeRecentSession(
        string Title,
        string DurationLabel);

    private sealed class MutableSessionsState :
        INotifyPropertyChanged
    {
        private IReadOnlyList<FakeRecentSession>
            _recentSessions;

        private object?
            _selectedSessionDetail;

        public MutableSessionsState(
            IReadOnlyList<FakeRecentSession>
                recentSessions,
            object? selectedSessionDetail)
        {
            _recentSessions =
                recentSessions;

            _selectedSessionDetail =
                selectedSessionDetail;
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;

        public bool HasActiveSessions =>
            false;

        public IReadOnlyList<object>
            ActiveSessions =>
                [];

        public IReadOnlyList<FakeRecentSession>
            RecentSessions =>
                _recentSessions;

        public bool HasRecentSessions =>
            RecentSessions.Count > 0;

        public object?
            SelectedSessionDetail =>
                _selectedSessionDetail;

        public void ApplyCorrectionProjection(
            IReadOnlyList<FakeRecentSession>
                recentSessions,
            object? selectedSessionDetail)
        {
            _recentSessions =
                recentSessions;

            _selectedSessionDetail =
                selectedSessionDetail;

            OnPropertyChanged(
                nameof(RecentSessions));

            OnPropertyChanged(
                nameof(HasRecentSessions));

            OnPropertyChanged(
                nameof(SelectedSessionDetail));
        }

        private void OnPropertyChanged(
            string propertyName) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    propertyName));
    }

    private sealed class EmptyLibraryStore :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot>
            LoadSnapshotAsync(
                CancellationToken cancellationToken) =>
            Task.FromResult(
                new LibrarySnapshot(
                    Array.Empty<LogicalGame>(),
                    Array.Empty<GameInstallation>()));
    }

    private sealed class ThrowingLibraryStore :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<LibrarySnapshot>
            LoadSnapshotAsync(
                CancellationToken cancellationToken)
        {
            await Task.Yield();

            throw new IOException(
                "Synthetic history read failure.");
        }
    }

    private sealed class CountingSessionStore :
        ISessionStore
    {
        public int GetRecentCallCount
        {
            get;
            private set;
        }

        public Task UpsertAsync(
            GameSession session,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<GameSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<GameSession?>(
                null);

        public Task<IReadOnlyList<GameSession>>
            GetActiveAsync(
                CancellationToken cancellationToken) =>
            Task.FromResult<
                IReadOnlyList<GameSession>>(
                    Array.Empty<GameSession>());

        public Task<IReadOnlyList<GameSession>>
            GetRecentAsync(
                int limit,
                CancellationToken cancellationToken)
        {
            GetRecentCallCount++;

            return Task.FromResult<
                IReadOnlyList<GameSession>>(
                    Array.Empty<GameSession>());
        }
    }

    private sealed class EmptyCorrectionStore :
        ISessionCorrectionStore
    {
        public Task UpsertAsync(
            SessionCorrection correction,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<SessionCorrection?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<
                SessionCorrection?>(
                    null);
    }

    private sealed class NoopSessionRuntime :
        ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot>
            RefreshAsync(
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CorrectSessionAsync(
            SessionCorrectionRequest request,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
