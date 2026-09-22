using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryDispatcherShutdownTests
{
    private static readonly GameId GameId =
        new(Guid.Parse("11111111-aaaa-4444-8888-111111111111"));

    private static readonly DateTimeOffset Now =
        new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispatcher_shutdown_prevents_late_library_notifications(bool publishBeforeShutdown)
    {
        RunSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            using var cancellation = new CancellationTokenSource();
            using var monitor = CreateMonitor(cancellation);
            var library = new LibraryViewModel(new LibraryStore(), monitor);
            library.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            var originalItems = library.Items;
            var notifications = 0;
            library.PropertyChanged += (_, _) => Interlocked.Increment(ref notifications);

            if (!publishBeforeShutdown) dispatcher.InvokeShutdown();
            Task.Run(() => monitor.RunAsync(cancellation.Token))
                .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            if (publishBeforeShutdown) dispatcher.InvokeShutdown();

            Assert.Equal(0, notifications);
            Assert.Same(originalItems, library.Items);
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

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { error = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "STA test did not complete.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
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
}
