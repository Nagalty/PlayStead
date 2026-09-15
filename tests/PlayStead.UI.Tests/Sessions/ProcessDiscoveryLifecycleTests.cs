using System.ComponentModel;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class ProcessDiscoveryLifecycleTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Expected_capture_error_marks_gap_without_confirming_absence()
    {
        var runtime = new FaultingRuntime(new ProcessCaptureUnavailableException(
            new Win32Exception("Enumeration unavailable.")));
        using var stop = new CancellationTokenSource();
        var notifications = 0;
        var monitor = new SessionMonitor(runtime, SessionMonitorOptions.Default,
            (_, _) =>
            {
                if (runtime.RefreshCount == 2) stop.Cancel();
                return Task.CompletedTask;
            });
        monitor.SnapshotUpdated += _ => notifications++;

        await monitor.RunAsync(stop.Token);

        Assert.Equal(2, runtime.RefreshCount);
        Assert.Equal(1, notifications);
        Assert.Equal(At.AddSeconds(2), monitor.LatestSnapshot!.ObservedAtUtc);
    }

    [Fact]
    public async Task Unexpected_capture_error_still_propagates()
    {
        var runtime = new FaultingRuntime(new InvalidOperationException("Unexpected capture defect."));
        var monitor = new SessionMonitor(runtime, SessionMonitorOptions.Default,
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.RunAsync(CancellationToken.None));
        Assert.Null(monitor.LatestSnapshot);
    }

    [Fact]
    public async Task Pre_capture_Windows_fault_is_not_misclassified_as_capture_gap()
    {
        var runtime = new FaultingRuntime(new Win32Exception("Revision validation failed."));
        using var stop = new CancellationTokenSource();
        var monitor = new SessionMonitor(runtime, SessionMonitorOptions.Default,
            (_, _) =>
            {
                stop.Cancel();
                return Task.CompletedTask;
            });

        await Assert.ThrowsAsync<Win32Exception>(() => monitor.RunAsync(stop.Token));
        Assert.Null(monitor.LatestSnapshot);
    }

    [Fact]
    public async Task Inventory_error_degrades_only_discovery_and_logs_once()
    {
        var d = new InventoryDriver();
        d.Source.ThrowIoFailure = true;
        await d.ScheduleAsync();
        await d.ScheduleAsync();

        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.Equal(2, d.Source.Calls);
        Assert.Single(d.Logger.Messages, message => message.Contains("inventory failed"));
    }

    [Fact]
    public async Task Learning_store_failure_leaves_discovery_pending()
    {
        var d = new InventoryDriver();
        d.Store.ThrowOnSave = true;
        await d.ScheduleAsync();

        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.Equal(1, d.Source.Calls);
        Assert.Equal(1, d.Store.SaveAttempts);
        Assert.Single(d.Logger.Messages, message => message.Contains("remains pending"));
        Assert.Contains(d.Logger.Messages,
            message => message.Contains(d.Store.LastAttemptGeneration.ToString("D")));
    }

    [Fact]
    public async Task Cancelled_shutdown_drops_late_inventory_publication_and_no_late_DB_write()
    {
        var d = new InventoryDriver();
        d.Source.HoldIgnoringCancellation();
        d.Manager.Schedule(d.Snapshot, CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        var stop = d.Manager.StopAsync(CancellationToken.None);
        Assert.False(stop.IsCompleted);
        d.Source.Release();
        await stop.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.Equal(0, d.Store.SaveAttempts);
    }

    [Fact]
    public async Task Acceptance_store_failure_never_reports_Valid()
    {
        var d = new InventoryDriver();
        await d.ScheduleAsync();
        await d.CompleteEpisodeAsync(100, 1);
        Assert.NotNull(d.Store.State(d.Installation.Id)!.Reference);
        d.Store.ThrowOnAcceptance = true;

        await d.CompleteEpisodeAsync(200, 20);

        Assert.Null(d.Store.Signature);
        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.Single(d.ObserverLogger.Messages,
            message => message.Contains("persistence unavailable"));
    }

    [Fact]
    public async Task Acceptance_IO_suspends_only_owning_installation_and_keeps_other_discovery_current()
    {
        var d = new InventoryDriver();
        d.Second = d.CreateSecondInstallation();
        await d.ScheduleAsync();
        await d.CompleteEpisodeAsync(100, 1);
        d.Store.ThrowOnAcceptance = true;

        await d.CompleteEpisodeAsync(200, 20);

        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.NotNull(d.Manager.GetCurrent(d.Second!.Id));
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Discovery_persistence_IO_does_not_stop_already_captured_Manual_session()
    {
        var d = new InventoryDriver();
        await d.ScheduleAsync();
        await d.CompleteEpisodeAsync(100, 1);
        var manualPath = @"C:\Manual\Manual.exe";
        var manual = new ProcessSnapshot(900, "Manual.exe", manualPath, At);
        d.Store.SetManualSignature(GameId.New().Value, manualPath);
        var source = new ProcessSource([manual]);
        var sessions = new SessionStore();
        var clock = new FixedClock { Now = At.AddMinutes(7) };
        var runtime = SessionRuntime.CreateWithObserver(source, d.Store, sessions,
            new ProcessSignatureMatcher(), new SessionTransitionPolicy(),
            new CorrectionStore(), new SessionCorrectionPolicy(),
            clock, d.Observer);
        await runtime.RefreshAsync(CancellationToken.None);
        clock.Now = At.AddMinutes(8);
        var active = Assert.Single((await runtime.RefreshAsync(CancellationToken.None)).ActiveSessions);
        d.Store.ThrowOnAcceptance = true;
        var game = new ProcessSnapshot(200, "Game.exe",
            d.Installation.InstallPath + @"\Game.exe", At.AddSeconds(22));
        await d.TickAsync([manual], 20);
        await d.TickAsync([manual], 21);
        await d.TickAsync([manual, game], 22);
        await d.TickAsync([manual, game], 23);
        await d.TickAsync([manual], 24);

        clock.Now = At.AddMinutes(25);
        var after = await runtime.RefreshAsync(CancellationToken.None);

        Assert.Equal(active.SessionId, Assert.Single(after.ActiveSessions).SessionId);
        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.Equal(ProcessSignatureOrigin.Manual, d.Store.Signature!.Origin);
        Assert.Collection(sessions.Writes,
            first => Assert.Equal(active.SessionId, first.SessionId),
            heartbeat =>
            {
                Assert.Equal(active.SessionId, heartbeat.SessionId);
                Assert.Equal(At.AddMinutes(25), heartbeat.LastSeenAtUtc);
            });
    }

    [Fact]
    public async Task Stale_fault_cannot_block_inflight_new_full_inventory()
    {
        var d = new InventoryDriver();
        await d.ScheduleAsync();
        var old = d.Manager.GetCurrent(d.Installation.Id)!;
        d.Second = d.CreateSecondInstallation();
        d.Source.HoldIgnoringCancellation(d.Second!.Id);
        d.Manager.Schedule(d.Snapshot, CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        Assert.False(d.Manager.MarkPending(d.Installation.Id, old));
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);

        Assert.NotNull(d.Manager.GetCurrent(d.Installation.Id));
        Assert.NotNull(d.Manager.GetCurrent(d.Second.Id));
    }

    [Fact]
    public async Task Healthy_scope_reprepares_after_other_scope_IO_gap_without_rescan()
    {
        var d = new InventoryDriver();
        d.Second = d.CreateSecondInstallation();
        await d.ScheduleAsync();
        await d.CompleteEpisodeAsync(100, 1);
        d.Store.ThrowOnAcceptance = true;
        await d.CompleteEpisodeAsync(200, 20);
        Assert.Null(d.Manager.GetCurrent(d.Installation.Id));
        Assert.NotNull(d.Manager.GetCurrent(d.Second.Id));
        var inventoriesBeforeGap = d.Source.Calls;

        await d.TickAsync([], 30); // the missed batch is nonqualifying for B
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(inventoriesBeforeGap + 1, d.Source.Calls); // B only, off tick
        await d.TickAsync([], 31);
        await d.TickAsync([], 32);
        var secondProcess = new ProcessSnapshot(300, "Game.exe",
            d.Second.InstallPath + @"\Game.exe", At.AddSeconds(33));
        await d.TickAsync([secondProcess], 33);
        await d.TickAsync([secondProcess], 34);
        await d.TickAsync([], 35);
        await d.TickAsync([], 36);

        Assert.NotNull(d.Store.State(d.Second.Id)!.Reference);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Old_acceptance_IO_cannot_suspend_newer_published_generation()
    {
        var d = new InventoryDriver();
        await d.ScheduleAsync();
        await d.CompleteEpisodeAsync(100, 1);
        var old = d.Manager.GetCurrent(d.Installation.Id)!;
        var game = new ProcessSnapshot(200, "Game.exe",
            d.Installation.InstallPath + @"\Game.exe", At.AddSeconds(22));
        await d.TickAsync([], 20);
        await d.TickAsync([], 21);
        await d.TickAsync([game], 22);
        await d.TickAsync([game], 23);
        await d.TickAsync([], 24);
        d.Store.HoldAcceptanceFailure();
        var oldTick = d.TickAsync([], 25);
        await d.Store.WaitForAcceptanceAsync();
        d.Source.RevisionSize = 11;
        await d.ScheduleAsync();
        var replacement = d.Manager.GetCurrent(d.Installation.Id)!;
        Assert.NotEqual(old.Inventory.Scope.GenerationId,
            replacement.Inventory.Scope.GenerationId);

        d.Store.ReleaseAcceptanceFailure();
        await oldTick;

        Assert.Same(replacement, d.Manager.GetCurrent(d.Installation.Id));
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Completed_unexpected_inventory_worker_fault_remains_observable_at_join()
    {
        var d = new InventoryDriver();
        d.Source.ThrowUnexpected = true;
        d.Manager.Schedule(d.Snapshot, CancellationToken.None);
        await d.Source.WaitForCallAsync();
        await d.Logger.WaitForWorkerFaultLogAsync(); // continuation has retired the task

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            d.Manager.AwaitIdleAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            d.Manager.StopAsync(CancellationToken.None));
        Assert.Single(d.Logger.Messages,
            message => message.Contains("unexpected inventory worker fault"));
    }

    [Fact]
    public async Task No_recursive_inventory_or_learning_write_on_stable_tick()
    {
        var d = new InventoryDriver();
        await d.ScheduleAsync();
        var inventories = d.Source.Calls;
        var saves = d.Store.SaveAttempts;

        await d.TickAsync([], 1);
        await d.TickAsync([], 2);

        Assert.Equal(inventories, d.Source.Calls);
        Assert.Equal(saves, d.Store.SaveAttempts);
    }

    [Fact]
    public async Task Revision_reads_are_targeted_only_for_observed_candidate()
    {
        var d = new InventoryDriver();
        d.Source.IncludeHelper = true;
        await d.ScheduleAsync();
        await d.TickAsync([], 1);
        await d.TickAsync([], 2);
        d.Store.RevisionPaths.Clear();

        await d.TickAsync([new ProcessSnapshot(100, "Game.exe",
            d.Installation.InstallPath + @"\Game.exe", At.AddSeconds(3))], 3);

        Assert.Equal([d.Installation.InstallPath + @"\Game.exe"], d.Store.RevisionPaths);
        Assert.DoesNotContain(d.Store.RevisionPaths, path => path.EndsWith("Helper.exe"));
        Assert.Equal(1, d.Source.Calls);
    }

    [Fact]
    public async Task Completed_proof_survives_shutdown_but_incomplete_Current_does_not()
    {
        var d = new InventoryDriver();
        await d.ScheduleAsync();
        await d.CompleteEpisodeAsync(100, 1);
        var reference = d.Store.State(d.Installation.Id)!.Reference;
        Assert.NotNull(reference);
        await d.TickAsync([], 20);
        await d.TickAsync([], 21);
        await d.TickAsync([new ProcessSnapshot(200, "Game.exe",
            d.Installation.InstallPath + @"\Game.exe", At.AddSeconds(22))], 22);
        await d.Manager.StopAsync(CancellationToken.None);

        var restartedCoordinator = new ProcessSignatureLearningCoordinator(d.Store, d.Store,
            d.Store, new ProcessSignatureDiscoveryPolicy());
        var restarted = new DiscoveryInventoryManager(d.Source, d.Store,
            restartedCoordinator, d.Logger);
        restarted.Schedule(d.Snapshot, CancellationToken.None);
        await restarted.AwaitIdleAsync(CancellationToken.None);

        Assert.Same(reference, d.Store.State(d.Installation.Id)!.Reference);
        Assert.Null(d.Store.State(d.Installation.Id)!.Confirmation);
        Assert.Same(reference, restartedCoordinator.GetState(d.Installation.Id)!.Reference);
        Assert.Null(d.Store.Signature);
    }

    private sealed class InventoryDriver
    {
        public readonly GameInstallation Installation = new(InstallationId.New(), GameId.New(),
            ProviderKind.Manual, "local", @"C:\Games\Example", null, false, true, At);
        public GameInstallation? Second;
        public readonly InventorySource Source = new();
        public readonly LearningStore Store = new();
        public readonly TestLogger<DiscoveryInventoryManager> Logger = new();
        public readonly TestLogger<ProcessDiscoveryCaptureObserver> ObserverLogger = new();
        public DiscoveryInventoryManager Manager { get; }
        public ProcessDiscoveryCaptureObserver Observer { get; }
        public ProcessSignatureLearningCoordinator Coordinator { get; }
        public LibrarySnapshot Snapshot => new([], Second is null ? [Installation] :
            [Installation, Second]);
        public GameInstallation CreateSecondInstallation() => new(InstallationId.New(),
            GameId.New(), ProviderKind.Manual, "second", @"C:\Games\Second", null,
            false, true, At);

        public InventoryDriver()
        {
            Coordinator = new ProcessSignatureLearningCoordinator(Store, Store, Store,
                new ProcessSignatureDiscoveryPolicy());
            Manager = new DiscoveryInventoryManager(Source, Store, Coordinator, Logger);
            var acceptance = new ProcessSignatureAcceptanceService(Store, Store, Store,
                Store, new ProcessSignatureDiscoveryPolicy(), Manager.GetCurrent,
                TimeProvider.System);
            Observer = new ProcessDiscoveryCaptureObserver(Manager, Coordinator,
                acceptance, ObserverLogger, TimeProvider.System);
        }

        public async Task ScheduleAsync()
        {
            Manager.Schedule(Snapshot, CancellationToken.None);
            await Manager.AwaitIdleAsync(CancellationToken.None);
        }

        public Task TickAsync(IReadOnlyList<ProcessSnapshot> processes, int second) =>
            Observer.ObserveAsync(new ProcessCaptureResult(processes, true),
                At.AddMinutes(second), CancellationToken.None);
        public async Task CompleteEpisodeAsync(int pid, int startingSecond)
        {
            await TickAsync([], startingSecond);
            await TickAsync([], startingSecond + 1);
            var process = new ProcessSnapshot(pid, "Game.exe",
                Installation.InstallPath + @"\Game.exe", At.AddSeconds(startingSecond + 2));
            await TickAsync([process], startingSecond + 2);
            await TickAsync([process], startingSecond + 3);
            await TickAsync([], startingSecond + 4);
            await TickAsync([], startingSecond + 5);
            await Manager.AwaitIdleAsync(CancellationToken.None);
        }
    }

    private sealed class InventorySource : IExecutableInventorySource
    {
        private TaskCompletionSource? _held;
        private TaskCompletionSource? _release;
        private InstallationId? _heldScope;
        private readonly TaskCompletionSource _called = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public bool ThrowIoFailure;
        public bool ThrowUnexpected;
        public bool IncludeHelper;
        public long RevisionSize = 10;
        public void HoldIgnoringCancellation(InstallationId? scopeId = null)
        {
            _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _heldScope = scopeId;
        }
        public Task WaitForHoldAsync() => _held!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public Task WaitForCallAsync() => _called.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Release() => _release!.TrySetResult();
        public async Task<ExecutableInventory> InventoryAsync(InstallationScope scope,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            _called.TrySetResult();
            if (_held is not null && (_heldScope is null || _heldScope == scope.InstallationId))
            {
                var held = Interlocked.Exchange(ref _held, null);
                held?.TrySetResult();
                await _release!.Task; // simulates an inventory source that ignores cancellation
            }
            if (ThrowIoFailure) throw new IOException("Inventory I/O failed.");
            if (ThrowUnexpected) throw new InvalidOperationException(
                "Unexpected inventory implementation defect.");
            var candidates = new List<ExecutableCandidate>
            {
                new(scope.RootPath + @"\Game.exe", "Game.exe", new FileRevision(RevisionSize, At))
            };
            if (IncludeHelper)
                candidates.Add(new(scope.RootPath + @"\Helper.exe", "Helper.exe",
                    new FileRevision(10, At)));
            return new ExecutableInventory(scope, InventoryCompleteness.Complete,
                candidates, []);
        }
    }

    private sealed class LearningStore : IProcessSignatureLearningStore,
        IProcessSignatureStore, IProcessSignatureDiscoveryStore, IExecutableRevisionSource
    {
        private readonly Dictionary<InstallationId, ProcessSignatureLearningState> _states = [];
        public bool ThrowOnSave;
        public bool ThrowOnAcceptance;
        public int SaveAttempts;
        public Guid LastAttemptGeneration;
        private TaskCompletionSource? _acceptanceHeld;
        private TaskCompletionSource? _acceptanceRelease;
        public List<string> RevisionPaths { get; } = [];
        public ProcessSignatureLearningState? State(InstallationId id) =>
            _states.GetValueOrDefault(id);
        public ProcessSignature? Signature { get; private set; }
        public void HoldAcceptanceFailure()
        {
            _acceptanceHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _acceptanceRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public Task WaitForAcceptanceAsync() =>
            _acceptanceHeld!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void ReleaseAcceptanceFailure() => _acceptanceRelease!.TrySetResult();
        public void SetManualSignature(Guid gameId, string path) => Signature = new
            ProcessSignature(gameId, [new ProcessSignatureEntry("Manual.exe",
                ProcessSignatureEntryKind.Main, path)], ProcessSignatureOrigin.Manual, At);
        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId installationId,
            CancellationToken cancellationToken) => Task.FromResult(State(installationId));
        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state,
            Guid? expectedConcurrencyToken, CancellationToken cancellationToken)
        {
            SaveAttempts++;
            LastAttemptGeneration = state.Inventory.Scope.GenerationId;
            if (ThrowOnSave) throw new IOException("Learning DB failed.");
            var id = state.Inventory.Scope.InstallationId;
            if (State(id)?.ConcurrencyToken != expectedConcurrencyToken) return Task.FromResult(false);
            _states[id] = state;
            return Task.FromResult(true);
        }
        public Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult(Signature?.GameId == gameId ? Signature : null);
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProcessSignature>>(Signature is null ? [] : [Signature]);
        public Task UpsertAsync(ProcessSignature signature, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public async Task<bool> TryInsertDiscoveredIfAbsentAsync(DiscoveredSignatureWrite write,
            CancellationToken cancellationToken)
        {
            if (_acceptanceHeld is not null)
            {
                _acceptanceHeld.TrySetResult();
                await _acceptanceRelease!.Task;
                throw new IOException("Old acceptance database I/O failed.");
            }
            if (ThrowOnAcceptance) throw new IOException("Acceptance database I/O failed.");
            if (Signature is not null) return false;
            Signature = write.Signature;
            return true;
        }
        public Task<bool> TryRevalidateDiscoveredAsync(DiscoveredSignatureWrite write,
            DiscoveredSignatureExpectation expected, CancellationToken cancellationToken) =>
            TryInsertDiscoveredIfAbsentAsync(write, cancellationToken);
        public Task<bool> TryInvalidateDiscoveredAsync(Guid gameId,
            DiscoveredSignatureExpectation expected, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string path,
            CancellationToken cancellationToken)
        {
            RevisionPaths.Add(path);
            return Task.FromResult(new ExecutableRevisionResult(new FileRevision(10, At), null));
        }
    }

    private sealed class ProcessSource(IReadOnlyList<ProcessSnapshot> processes) : IProcessSnapshotSource
    {
        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(CancellationToken token) =>
            Task.FromResult(processes);
    }

    private sealed class SessionStore : ISessionStore
    {
        public List<GameSession> Writes { get; } = [];
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<GameSession?> GetAsync(Guid id, CancellationToken token) =>
            Task.FromResult<GameSession?>(null);
        public Task UpsertAsync(GameSession session, CancellationToken token)
        {
            Writes.Add(session);
            return Task.CompletedTask;
        }
    }

    private sealed class CorrectionStore : ISessionCorrectionStore
    {
        public Task UpsertAsync(SessionCorrection correction, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<SessionCorrection?> GetAsync(Guid id, CancellationToken token) =>
            Task.FromResult<SessionCorrection?>(null);
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now = At;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource _workerFaultLogged = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Messages { get; } = [];
        public Task WaitForWorkerFaultLogAsync() =>
            _workerFaultLogged.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Messages.Add(message);
            if (message.Contains("unexpected inventory worker fault"))
                _workerFaultLogged.TrySetResult();
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FaultingRuntime(Exception firstError) : ISessionRuntime
    {
        public int RefreshCount { get; private set; }

        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCount++;
            if (RefreshCount == 1)
            {
                throw firstError;
            }
            return Task.FromResult(new SessionRuntimeSnapshot(At.AddSeconds(2), []));
        }

        public Task CorrectSessionAsync(SessionCorrectionRequest correction,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
