using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class ProcessDiscoveryCaptureObserverTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Observer_trace_marks_inventory_matched_snapshot_as_forwarded_without_changing_decision()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var driver = new Driver();
            await driver.InitializeAsync();

            await driver.TickAsync([driver.Process(100, 1)], 1);

            Assert.Null(driver.Store.Signature);
            listener.Flush();
            var trace = output.ToString();
            Assert.Contains("[PROCESS-FORENSIC] ObserverForward", trace, StringComparison.Ordinal);
            Assert.Contains($"GameId={driver.First.GameId}", trace, StringComparison.Ordinal);
            Assert.Contains("ProcessName=Game.exe", trace, StringComparison.Ordinal);
            Assert.Contains("ObservationForwarded=true", trace, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Empty_captures_are_forwarded_for_published_learning_participant()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var driver = new Driver();
            await driver.InitializeAsync();

            await driver.TickAsync([driver.Process(100, 1)], 1);
            await driver.TickAsync([], 2);
            await driver.TickAsync([], 3);

            listener.Flush();
            var trace = output.ToString();
            Assert.Contains($"[DISCOVERY-ABSENCE] GameId={driver.First.GameId}", trace,
                StringComparison.Ordinal);
            Assert.Contains("RelevantObservationCount=0 SupportObservationCount=0", trace,
                StringComparison.Ordinal);
            Assert.Contains("AbsenceForwarded=true BaselineEstablished=true", trace,
                StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Empty_capture_does_not_create_absence_processing_without_learning_participant()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var driver = new Driver();
            await driver.TickAsync([], 1);

            listener.Flush();
            Assert.DoesNotContain("[DISCOVERY-ABSENCE]", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(0, driver.Store.StateCount);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task External_absent_present_absent_episodes_promote_without_launch_intent()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.CompleteEpisodeAsync(100, 1);
        Assert.Null(d.Store.Signature);
        Assert.NotNull(d.Store.State(d.First.Id)!.Reference);
        await d.CompleteEpisodeAsync(200, 20);
        Assert.Equal(ProcessSignatureOrigin.Discovered, d.Store.Signature!.Origin);
        Assert.Equal("Game.exe", Assert.Single(d.Store.Signature.Entries).ExecutableName);
        Assert.Equal(1, d.Store.AcceptedWrites);
    }

    [Fact]
    public async Task One_completed_episode_keeps_NO_SIGNATURE()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.CompleteEpisodeAsync(100, 1);
        Assert.Null(d.Store.Signature);
        Assert.NotNull(d.Store.State(d.First.Id)!.Reference);
        Assert.Null(d.Store.State(d.First.Id)!.Confirmation);
    }

    [Fact]
    public async Task Promotion_waits_for_second_complete_episode()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.CompleteEpisodeAsync(100, 1);
        await d.TickAsync([d.Process(200, 20)], 20);
        await d.TickAsync([d.Process(200, 20)], 21);
        Assert.Null(d.Store.Signature);
        await d.TickAsync([], 22);
        Assert.Null(d.Store.Signature);
        await d.TickAsync([], 23);
        Assert.NotNull(d.Store.Signature);
    }

    [Fact]
    public async Task Observer_uses_exact_shared_batch_once_per_installation()
    {
        var d = new Driver();
        var second = d.Installation(@"C:\Games\Second");
        await d.InitializeAsync(d.First, second);
        var firstPath = d.First.InstallPath + @"\Game.exe";
        var secondPath = second.InstallPath + @"\Game.exe";
        var both = new ProcessSnapshot[] { new(101, "Game.exe", firstPath, Epoch.AddSeconds(3)),
            new(202, "Game.exe", secondPath, Epoch.AddSeconds(3)) };
        var initializationWrites = d.Store.LearningWrites;
        await d.TickAsync([], 1);
        await d.TickAsync([], 2);
        await d.TickAsync(both, 3);
        await d.TickAsync(both, 4);
        Assert.Equal(initializationWrites, d.Store.LearningWrites);
        Assert.Equal(0, d.Inventory.ExtraCalls);
        await d.TickAsync([], 5);
        await d.TickAsync([], 6);
        Assert.Equal(2, d.Store.StateCount);
        Assert.Equal(1, d.Store.State(d.First.Id)!.Reference!.FirstSnapshot);
        Assert.Equal(1, d.Store.State(second.Id)!.Reference!.FirstSnapshot);
        Assert.Equal(6, d.Store.State(d.First.Id)!.Reference!.LastSnapshot);
        Assert.Equal(6, d.Store.State(second.Id)!.Reference!.LastSnapshot);
        Assert.Equal(0, d.Store.AcceptedWrites);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(2, d.Inventory.ExtraCalls); // one post-episode inventory per scope
    }

    [Fact]
    public async Task Missing_or_pending_inventory_does_not_observe()
    {
        var d = new Driver();
        await d.TickAsync([d.Process(100, 1)], 1);
        Assert.Null(d.Store.State(d.First.Id));
        await d.InitializeAsync();
        d.Manager.MarkRefreshing();
        await d.TickAsync([d.Process(100, 2)], 2);
        Assert.Null(d.Store.Signature);
        Assert.Equal(0, d.Store.AcceptedWrites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Incomplete_capture_and_gap_clear_qualification_not_fake_end(bool gap)
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.TickAsync([], 1);
        await d.TickAsync([], 2);
        await d.TickAsync([d.Process(100, 3)], 3);
        await d.TickAsync([d.Process(100, 3)], 4);
        if (gap) d.Observer.MarkCaptureGap();
        await d.TickAsync([], 5, complete: gap);
        await d.TickAsync([], 6);
        Assert.Null(d.Store.Signature);
        Assert.Null(d.Store.State(d.First.Id)!.Reference);
    }

    [Fact]
    public async Task Unknown_preexisting_unrelated_process_does_not_invent_new_identity()
    {
        var d = new Driver();
        await d.InitializeAsync();
        var unrelated = new ProcessSnapshot(900, "Other.exe", null, null);
        await d.TickAsync([unrelated], 1);
        await d.TickAsync([unrelated], 2);
        await d.TickAsync([unrelated, d.Process(100, 3)], 3);
        await d.TickAsync([unrelated, d.Process(100, 3)], 4);
        await d.TickAsync([unrelated], 5);
        await d.TickAsync([unrelated], 6);
        Assert.NotNull(d.Store.State(d.First.Id)!.Reference);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task New_unrelated_unknown_identity_during_episode_does_not_block_promotion()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.CompleteEpisodeAsync(100, 1);

        await d.TickAsync([d.Process(200, 20)], 20);
        await d.TickAsync(
            [
                d.Process(200, 20),
                new ProcessSnapshot(
                    900,
                    "Other.exe",
                    null,
                    null)
            ],
            21);

        await d.TickAsync([], 22);
        await d.TickAsync([], 23);

        Assert.NotNull(d.Store.Signature);
        Assert.Equal(
            ProcessSignatureOrigin.Discovered,
            d.Store.Signature!.Origin);
        Assert.Equal(
            "Game.exe",
            Assert.Single(
                d.Store.Signature.Entries)
                .ExecutableName);
        Assert.Equal(
            1,
            d.Store.AcceptedWrites);
    }

    [Fact]
    public async Task Ambiguous_installations_remain_NO_SIGNATURE()
    {
        var d = new Driver();
        var nested = d.Installation(d.First.InstallPath + @"\Child");
        await d.InitializeAsync(d.First, nested);
        await d.CompleteEpisodeAsync(100, 1);
        await d.CompleteEpisodeAsync(200, 20);
        Assert.Null(d.Store.Signature);
        Assert.Equal(0, d.Store.AcceptedWrites);
    }

    [Fact]
    public async Task New_under_root_path_requests_inventory_outside_tick()
    {
        var d = new Driver();
        await d.InitializeAsync();
        d.Inventory.HoldNext();
        var extra = new ProcessSnapshot(500, "New.exe", d.First.InstallPath + @"\New.exe", Epoch.AddSeconds(3));
        await d.TickAsync([extra], 1);
        await d.Inventory.WaitForHoldAsync();
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        d.Inventory.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(1, d.Inventory.ExtraCalls);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Repeated_same_under_root_path_does_not_inventory_each_tick()
    {
        var d = new Driver();
        await d.InitializeAsync();
        var extra = new ProcessSnapshot(500, "New.exe", d.First.InstallPath + @"\New.exe", Epoch.AddSeconds(3));
        await d.TickAsync([extra], 1);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        await d.TickAsync([extra], 2);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(1, d.Inventory.ExtraCalls);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Changed_new_under_root_path_refreshes_even_when_known_game_is_first()
    {
        var d = new Driver();
        await d.InitializeAsync();
        var game = d.Process(100, 1);
        var first = new ProcessSnapshot(500, "New.exe", d.First.InstallPath + @"\New.exe",
            Epoch.AddSeconds(1));
        var changed = new ProcessSnapshot(501, "New.exe", d.First.InstallPath + @"\Changed\New.exe",
            Epoch.AddSeconds(2));
        await d.TickAsync([game, first], 1);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(1, d.Inventory.ExtraCalls);
        await d.TickAsync([game, changed], 2);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(2, d.Inventory.ExtraCalls);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Additional_simultaneous_unknown_under_root_path_refreshes_once()
    {
        var d = new Driver();
        await d.InitializeAsync();
        var game = d.Process(100, 1);
        var first = new ProcessSnapshot(500, "A.exe", d.First.InstallPath + @"\A.exe",
            Epoch.AddSeconds(1));
        var additional = new ProcessSnapshot(501, "B.exe", d.First.InstallPath + @"\B.exe",
            Epoch.AddSeconds(2));
        await d.TickAsync([game, first], 1);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(1, d.Inventory.ExtraCalls);
        await d.TickAsync([game, first, additional], 2);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(2, d.Inventory.ExtraCalls);
        await d.TickAsync([game, first, additional], 3);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal(2, d.Inventory.ExtraCalls);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Pending_publication_during_decision_discards_old_generation_actions()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.TickAsync([], 1);
        await d.TickAsync([], 2);
        await d.TickAsync([d.Process(100, 3)], 3);
        await d.TickAsync([d.Process(100, 3)], 4);
        await d.TickAsync([], 5);
        d.Store.HoldNextSignatureRead();
        var lastTick = d.TickAsync([], 6);
        await d.Store.WaitForSignatureReadAsync();
        d.Manager.MarkRefreshing();
        d.Store.ReleaseSignatureRead();
        await lastTick;
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.DoesNotContain(d.Logger.Messages, message => message.Contains("AwaitingIndependentEpisode"));
        Assert.Equal(0, d.Inventory.ExtraCalls);
        Assert.Null(d.Store.Signature);
    }

    [Fact]
    public async Task Capture_quality_transition_is_logged_once_not_per_tick()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.TickAsync([], 1, complete: false);
        await d.TickAsync([], 2, complete: false);
        Assert.Single(d.Logger.Messages, message => message.Contains("PartialEpisode"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acceptance_failure_never_reports_promotion(bool throwOnWrite)
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.CompleteEpisodeAsync(100, 1);
        d.Store.RejectWrite = !throwOnWrite;
        d.Store.ThrowOnWrite = throwOnWrite;
        if (throwOnWrite)
            await Assert.ThrowsAsync<InvalidOperationException>(() => d.CompleteEpisodeAsync(200, 20));
        else await d.CompleteEpisodeAsync(200, 20);
        Assert.Null(d.Store.Signature);
        Assert.Equal(0, d.Store.AcceptedWrites);
        Assert.DoesNotContain(d.Logger.Messages, message => message.Contains("PromoteMain") ||
            message.Contains("signature accepted"));
    }

    [Fact]
    public async Task Cancellation_does_not_promote_or_write_late()
    {
        var d = new Driver();
        await d.InitializeAsync();
        await d.CompleteEpisodeAsync(100, 1);
        await d.TickAsync([d.Process(200, 20)], 20);
        await d.TickAsync([d.Process(200, 20)], 21);
        await d.TickAsync([], 22);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            d.TickAsync([], 23, cancellationToken: cancelled.Token));
        Assert.Null(d.Store.Signature);
        Assert.Equal(0, d.Store.AcceptedWrites);
    }

    private sealed class Driver
    {
        private readonly ProcessSignatureLearningCoordinator _coordinator;
        private readonly ProcessSignatureAcceptanceService _acceptance;
        public Driver()
        {
            First = Installation(@"C:\Games\Example");
            _coordinator = new(Store, Store, Store, new());
            Manager = new(Inventory, Store, _coordinator, NullLogger<DiscoveryInventoryManager>.Instance);
            _acceptance = new(Store, Store, Store, Store, new(), Manager.GetCurrent,
                TimeProvider.System,
                new DiscoveryConfirmationSessionPromoter(new SessionStore(), new SessionTransitionPolicy()));
            Observer = new(Manager, _coordinator, _acceptance, Logger, TimeProvider.System);
        }
        public GameInstallation First { get; }
        public TestStore Store { get; } = new();
        public InventorySource Inventory { get; } = new();
        public TestLogger Logger { get; } = new();
        public DiscoveryInventoryManager Manager { get; }
        public ProcessDiscoveryCaptureObserver Observer { get; }
        public GameInstallation Installation(string root) => new(InstallationId.New(), GameId.New(),
            ProviderKind.Manual, Guid.NewGuid().ToString(), root, null, false, true, Epoch);
        public ProcessSnapshot Process(int pid, int second) =>
            new(pid, "Game.exe", First.InstallPath + @"\Game.exe", Epoch.AddSeconds(second));
        public async Task InitializeAsync(params GameInstallation[] installations)
        {
            Manager.Schedule(new LibrarySnapshot([], installations.Length == 0 ? [First] : installations),
                CancellationToken.None);
            await Manager.AwaitIdleAsync(CancellationToken.None);
            Inventory.ExtraCalls = 0;
        }
        public async Task TickAsync(IReadOnlyList<ProcessSnapshot> processes, int second,
            bool complete = true, CancellationToken cancellationToken = default)
        {
            await Observer.ObserveAsync(new ProcessCaptureResult(processes, complete),
                Epoch.AddMinutes(second), cancellationToken);
        }
        public async Task CompleteEpisodeAsync(int pid, int startingSecond)
        {
            await TickAsync([], startingSecond);
            await TickAsync([], startingSecond + 1);
            await TickAsync([Process(pid, startingSecond + 2)], startingSecond + 2);
            await TickAsync([Process(pid, startingSecond + 2)], startingSecond + 3);
            await TickAsync([], startingSecond + 4);
            await TickAsync([], startingSecond + 5);
            await Manager.AwaitIdleAsync(CancellationToken.None);
        }
    }

    private sealed class SessionStore : ISessionStore
    {
        private readonly Dictionary<Guid, GameSession> _sessions = [];
        public Task UpsertAsync(GameSession session, CancellationToken token)
        {
            _sessions[session.SessionId] = session;
            return Task.CompletedTask;
        }
        public Task<GameSession?> GetAsync(Guid id, CancellationToken token) =>
            Task.FromResult(_sessions.GetValueOrDefault(id));
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameSession>>(_sessions.Values.Take(limit).ToArray());
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameSession>>(_sessions.Values.Where(x => x.GameId == gameId).ToArray());
    }

    private sealed class TestLogger : Microsoft.Extensions.Logging.ILogger<ProcessDiscoveryCaptureObserver>
    {
        public List<string> Messages { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level,
            Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, error));
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class InventorySource : IExecutableInventorySource
    {
        private TaskCompletionSource<bool>? _hold;
        private TaskCompletionSource<bool>? _holdHandle;
        private TaskCompletionSource<bool>? _release;
        private int _extraCalls;
        public int ExtraCalls
        {
            get => Volatile.Read(ref _extraCalls);
            set => Volatile.Write(ref _extraCalls, value);
        }
        public void HoldNext()
        {
            _hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _holdHandle = _hold;
            _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public Task WaitForHoldAsync() => _holdHandle!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Release() => _release!.TrySetResult(true);
        public async Task<ExecutableInventory> InventoryAsync(InstallationScope scope, CancellationToken token)
        {
            Interlocked.Increment(ref _extraCalls);
            var hold = Interlocked.Exchange(ref _hold, null);
            if (hold is not null)
            {
                hold.TrySetResult(true);
                await _release!.Task.WaitAsync(token);
            }
            return new ExecutableInventory(scope, InventoryCompleteness.Complete,
                [new(scope.RootPath + @"\Game.exe", "Game.exe", new FileRevision(10, Epoch))], []);
        }
    }

    private sealed class TestStore : IProcessSignatureLearningStore, IProcessSignatureStore,
        IProcessSignatureDiscoveryStore, IExecutableRevisionSource
    {
        private TaskCompletionSource<bool>? _signatureReadEntered;
        private TaskCompletionSource<bool>? _signatureReadObserved;
        private TaskCompletionSource<bool>? _signatureReadRelease;
        private readonly Dictionary<InstallationId, ProcessSignatureLearningState> _states = [];
        private readonly object _stateGate = new();
        public ProcessSignature? Signature { get; private set; }
        public int StateCount { get { lock (_stateGate) return _states.Count; } }
        public int AcceptedWrites { get; private set; }
        public int LearningWrites { get; private set; }
        public bool RejectWrite { get; set; }
        public bool ThrowOnWrite { get; set; }
        public void HoldNextSignatureRead()
        {
            _signatureReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _signatureReadObserved = _signatureReadEntered;
            _signatureReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public Task WaitForSignatureReadAsync() =>
            _signatureReadObserved!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void ReleaseSignatureRead() => _signatureReadRelease!.TrySetResult(true);
        public ProcessSignatureLearningState? State(InstallationId id)
        {
            lock (_stateGate) return _states.GetValueOrDefault(id);
        }
        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(State(id));
        }
        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var id = state.Inventory.Scope.InstallationId;
            lock (_stateGate)
            {
                if (_states.GetValueOrDefault(id)?.ConcurrencyToken != expected)
                    return Task.FromResult(false);
                _states[id] = state;
                LearningWrites++;
                return Task.FromResult(true);
            }
        }
        public async Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken token)
        {
            var entered = Interlocked.Exchange(ref _signatureReadEntered, null);
            if (entered is not null)
            {
                entered.TrySetResult(true);
                await _signatureReadRelease!.Task.WaitAsync(token);
            }
            return Signature?.GameId == gameId ? Signature : null;
        }
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ProcessSignature>>(Signature is null ? [] : [Signature]);
        public Task UpsertAsync(ProcessSignature signature, CancellationToken token) =>
            throw new InvalidOperationException("Conditional writes only.");
        public Task<bool> TryInsertDiscoveredIfAbsentAsync(DiscoveredSignatureWrite write, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ThrowOnWrite) throw new InvalidOperationException("Acceptance failed.");
            if (RejectWrite || Signature is not null) return Task.FromResult(false);
            Signature = write.Signature;
            AcceptedWrites++;
            return Task.FromResult(true);
        }
        public Task<bool> TryRevalidateDiscoveredAsync(DiscoveredSignatureWrite write,
            DiscoveredSignatureExpectation expected, CancellationToken token) =>
            TryInsertDiscoveredIfAbsentAsync(write, token);
        public Task<bool> TryInvalidateDiscoveredAsync(Guid gameId,
            DiscoveredSignatureExpectation expected, CancellationToken token) => Task.FromResult(false);
        public Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new ExecutableRevisionResult(new FileRevision(10, Epoch), null));
        }
        public Task<bool> TryRestoreDiscoveredValidationAsync(Guid gameId,
            DiscoveredSignatureExpectation expected, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> TryRefreshDiscoveredValidationAsync(ProcessSignature signature,
            DiscoveredSignatureExpectation expected, DiscoveryInventoryContext current,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
