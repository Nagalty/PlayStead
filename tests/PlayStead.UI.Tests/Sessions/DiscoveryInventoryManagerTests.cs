using Microsoft.Extensions.Logging.Abstractions;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class DiscoveryInventoryManagerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Initial_snapshot_schedules_without_waiting_for_scan()
    {
        var d = new Driver();
        d.Source.HoldNext();
        d.Manager.Schedule(d.Snapshot(), CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.NotNull(d.Manager.GetCurrent(d.First.Id));
    }

    [Fact]
    public async Task Only_present_installations_are_inventoried()
    {
        var d = new Driver();
        var absent = d.Installation(@"C:\Games\Absent", false);
        d.Manager.Schedule(d.Snapshot(d.First, absent), CancellationToken.None);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal([d.First.Id], d.Source.Calls);
        Assert.Null(d.Manager.GetCurrent(absent.Id));
    }

    [Fact]
    public async Task Restart_same_inventory_reuses_persisted_generation_and_reference()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        await d.MakeReferenceAsync();
        var before = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        var restartedCoordinator = new ProcessSignatureLearningCoordinator(d.Store,
            new SignatureStore(), new RevisionSource(), new ProcessSignatureDiscoveryPolicy());
        var restarted = d.NewManager(restartedCoordinator);
        restarted.Schedule(d.Snapshot(), CancellationToken.None);
        await restarted.AwaitIdleAsync(CancellationToken.None);
        var after = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        Assert.Equal(before.Inventory.Scope.GenerationId, restarted.GetCurrent(d.First.Id)!.Inventory.Scope.GenerationId);
        Assert.Same(before.Reference, after.Reference);
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Same(before.Reference, restartedCoordinator.GetState(d.First.Id)!.Reference);
    }

    [Fact]
    public async Task Changed_revision_rotates_generation_and_suspends_old_context()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        var generation = d.Manager.GetCurrent(d.First.Id)!.Inventory.Scope.GenerationId;
        d.Source.RevisionSize = 20;
        d.Source.HoldNext();
        d.Manager.Schedule(d.Snapshot(), CancellationToken.None);
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        await d.Source.WaitForHoldAsync();
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.NotEqual(generation, d.Manager.GetCurrent(d.First.Id)!.Inventory.Scope.GenerationId);
    }

    [Fact]
    public async Task Unchanged_rescan_retains_reference()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        await d.MakeReferenceAsync();
        var prior = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        await d.ScheduleAsync(d.Snapshot());
        var after = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        Assert.Same(prior.Reference, after.Reference);
        Assert.Equal(prior.ConcurrencyToken, after.ConcurrencyToken);
    }

    [Fact]
    public async Task Removed_installation_suspends_discovery_and_discards_old_publication()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        d.Manager.Schedule(d.Snapshot(d.First with { IsPresent = false }), CancellationToken.None);
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
    }

    [Fact]
    public async Task Reappearing_changed_installation_starts_new_generation()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        var old = d.Manager.GetCurrent(d.First.Id)!.Inventory.Scope.GenerationId;
        await d.ScheduleAsync(d.Snapshot(d.First with { IsPresent = false }));
        d.Source.RevisionSize = 30;
        await d.ScheduleAsync(d.Snapshot());
        Assert.NotEqual(old, d.Manager.GetCurrent(d.First.Id)!.Inventory.Scope.GenerationId);
    }

    [Fact]
    public async Task Restart_absent_then_reappearing_installation_discards_persisted_proof()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        await d.MakeReferenceAsync();
        var prior = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        var restartedCoordinator = new ProcessSignatureLearningCoordinator(d.Store,
            new SignatureStore(), new RevisionSource(), new ProcessSignatureDiscoveryPolicy());
        var restarted = d.NewManager(restartedCoordinator);
        restarted.Schedule(d.Snapshot(d.First with { IsPresent = false }), CancellationToken.None);
        await restarted.AwaitIdleAsync(CancellationToken.None);
        restarted.Schedule(d.Snapshot(), CancellationToken.None);
        await restarted.AwaitIdleAsync(CancellationToken.None);
        var current = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        Assert.NotEqual(prior.Inventory.Scope.GenerationId, current.Inventory.Scope.GenerationId);
        Assert.Null(current.Reference);
    }

    [Fact]
    public async Task Rescan_pending_returns_null_for_validation()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        d.Manager.MarkRefreshing();
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        d.Source.HoldNext();
        d.Manager.Schedule(d.Snapshot(), CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stale_inventory_result_never_publishes()
    {
        var d = new Driver();
        d.Source.HoldNextIgnoringCancellation();
        d.Manager.Schedule(d.Snapshot(), CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        d.Manager.Schedule(d.Snapshot(d.First with { IsPresent = false }), CancellationToken.None);
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
    }

    [Fact]
    public async Task Overlapping_roots_are_ambiguous()
    {
        var d = new Driver();
        var child = d.Installation(@"C:\Games\Example\Child");
        await d.ScheduleAsync(d.Snapshot(d.First, child));
        Assert.True(d.Manager.GetCurrent(d.First.Id)!.HasAmbiguousInstallation);
        Assert.True(d.Manager.GetCurrent(child.Id)!.HasAmbiguousInstallation);
    }

    [Fact]
    public async Task Incomplete_or_inaccessible_root_never_authorizes_promotion()
    {
        var d = new Driver();
        d.Source.Complete = false;
        await d.ScheduleAsync(d.Snapshot());
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        Assert.Equal(InventoryCompleteness.Incomplete,
            (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!.Inventory.Completeness);
    }

    [Fact]
    public async Task Inventory_exception_quarantines_previous_complete_proof()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        await d.MakeReferenceAsync();
        d.Source.ThrowIoFailure = true;
        await d.ScheduleAsync(d.Snapshot());
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
        var state = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        Assert.Equal(InventoryCompleteness.Incomplete, state.Inventory.Completeness);
        Assert.Null(state.Reference);
    }

    [Fact]
    public async Task Post_episode_preparation_inventories_only_its_installation()
    {
        var d = new Driver();
        var second = d.Installation(@"C:\Games\Second");
        await d.ScheduleAsync(d.Snapshot(d.First, second));
        d.Source.Calls.Clear();
        d.Manager.RequestEpisodePreparation(d.First.Id, CancellationToken.None);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Equal([d.First.Id], d.Source.Calls);
    }

    [Fact]
    public async Task Preparation_during_full_snapshot_preserves_unrelated_publication()
    {
        var d = new Driver();
        var second = d.Installation(@"C:\Games\Second");
        d.Source.HoldNext();
        d.Manager.Schedule(d.Snapshot(d.First, second), CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        var heldId = Assert.Single(d.Source.Calls);
        var requestedId = heldId == d.First.Id ? second.Id : d.First.Id;
        d.Manager.RequestEpisodePreparation(requestedId, CancellationToken.None);
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.NotNull(d.Manager.GetCurrent(heldId));
        Assert.NotNull(d.Manager.GetCurrent(requestedId));
    }

    [Fact]
    public async Task Overlapping_preparations_for_different_installations_both_publish()
    {
        var d = new Driver();
        var second = d.Installation(@"C:\Games\Second");
        await d.ScheduleAsync(d.Snapshot(d.First, second));
        d.Source.HoldNext();
        d.Manager.RequestEpisodePreparation(d.First.Id, CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        d.Manager.RequestEpisodePreparation(second.Id, CancellationToken.None);
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.NotNull(d.Manager.GetCurrent(d.First.Id));
        Assert.NotNull(d.Manager.GetCurrent(second.Id));
    }

    [Fact]
    public async Task Cancelled_full_snapshot_does_not_leave_later_preparation_queued()
    {
        var d = new Driver();
        var second = d.Installation(@"C:\Games\Second");
        await d.ScheduleAsync(d.Snapshot(d.First, second));
        d.Source.HoldNextIgnoringCancellation();
        using var cancellation = new CancellationTokenSource();
        d.Manager.Schedule(d.Snapshot(d.First, second), cancellation.Token);
        await d.Source.WaitForHoldAsync();
        d.Manager.RequestEpisodePreparation(second.Id, CancellationToken.None);
        cancellation.Cancel();
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        d.Manager.RequestEpisodePreparation(d.First.Id, CancellationToken.None);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.NotNull(d.Manager.GetCurrent(d.First.Id));
    }

    [Fact]
    public async Task Fresh_unchanged_preparation_preserves_reference_and_trailing_absences()
    {
        var d = new Driver();
        await d.ScheduleAsync(d.Snapshot());
        await d.MakeReferenceAsync();
        var before = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        d.Manager.RequestEpisodePreparation(d.First.Id, CancellationToken.None);
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        var after = (await d.Store.LoadAsync(d.First.Id, CancellationToken.None))!;
        Assert.Same(before.Reference, after.Reference);
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        var process = d.Process(200, 13);
        await d.Coordinator.ObserveAsync(d.First.Id, d.Batch(7, [process]), CancellationToken.None);
        await d.Coordinator.ObserveAsync(d.First.Id, d.Batch(8, [process]), CancellationToken.None);
        await d.Coordinator.ObserveAsync(d.First.Id, d.Batch(9, []), CancellationToken.None);
        var decision = await d.Coordinator.ObserveAsync(d.First.Id, d.Batch(10, []), CancellationToken.None);
        Assert.Equal(DiscoveryDecisionKind.PromoteMain, decision!.Kind);
    }

    [Fact]
    public async Task Cancellation_drops_late_publication()
    {
        var d = new Driver();
        d.Source.HoldNextIgnoringCancellation();
        using var cancellation = new CancellationTokenSource();
        d.Manager.Schedule(d.Snapshot(), cancellation.Token);
        await d.Source.WaitForHoldAsync();
        cancellation.Cancel();
        d.Source.Release();
        await d.Manager.AwaitIdleAsync(CancellationToken.None);
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
    }

    [Fact]
    public async Task Cancelled_stop_can_be_retried_and_joins_owned_inventory()
    {
        var d = new Driver();
        d.Source.HoldNextIgnoringCancellation();
        d.Manager.Schedule(d.Snapshot(), CancellationToken.None);
        await d.Source.WaitForHoldAsync();
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            d.Manager.StopAsync(shutdown.Token));
        var retry = d.Manager.StopAsync(CancellationToken.None);
        Assert.False(retry.IsCompleted);
        d.Source.Release();
        await retry.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(d.Manager.GetCurrent(d.First.Id));
    }

    private sealed class Driver
    {
        public Driver()
        {
            First = Installation(@"C:\Games\Example");
            Coordinator = new ProcessSignatureLearningCoordinator(Store, new SignatureStore(),
                new RevisionSource(), new ProcessSignatureDiscoveryPolicy());
            Manager = NewManager();
        }

        public GameInstallation First { get; }
        public InventorySource Source { get; } = new();
        public LearningStore Store { get; } = new();
        public ProcessSignatureLearningCoordinator Coordinator { get; }
        public DiscoveryInventoryManager Manager { get; }

        public DiscoveryInventoryManager NewManager(ProcessSignatureLearningCoordinator? coordinator = null) =>
            new(Source, Store, coordinator ?? Coordinator,
                NullLogger<DiscoveryInventoryManager>.Instance);

        public GameInstallation Installation(string root, bool present = true) =>
            new(InstallationId.New(), GameId.New(), ProviderKind.Manual, Guid.NewGuid().ToString(),
                root, null, false, present, Now);

        public LibrarySnapshot Snapshot(params GameInstallation[] installations) =>
            new([], installations.Length == 0 ? [First] : installations);

        public async Task ScheduleAsync(LibrarySnapshot snapshot)
        {
            Manager.Schedule(snapshot, CancellationToken.None);
            await Manager.AwaitIdleAsync(CancellationToken.None);
        }

        public ProcessSnapshot Process(int pid, int startedSecond) =>
            new(pid, "Game.exe", First.InstallPath + @"\Game.exe", Now.AddSeconds(startedSecond));

        public ProcessObservationBatch Batch(long sequence, IReadOnlyList<ProcessSnapshot> processes) =>
            new(sequence, Now.AddSeconds(sequence * 2), EpisodeQuality.Complete, processes);

        public async Task MakeReferenceAsync()
        {
            await Coordinator.PrepareEpisodeAsync(Manager.GetCurrent(First.Id)!,
                Coordinator.GetState(First.Id)!.ConcurrencyToken, CancellationToken.None);
            await Coordinator.ObserveAsync(First.Id, Batch(1, []), CancellationToken.None);
            await Coordinator.ObserveAsync(First.Id, Batch(2, []), CancellationToken.None);
            var process = Process(100, 5);
            await Coordinator.ObserveAsync(First.Id, Batch(3, [process]), CancellationToken.None);
            await Coordinator.ObserveAsync(First.Id, Batch(4, [process]), CancellationToken.None);
            await Coordinator.ObserveAsync(First.Id, Batch(5, []), CancellationToken.None);
            await Coordinator.ObserveAsync(First.Id, Batch(6, []), CancellationToken.None);
            Assert.NotNull(Coordinator.GetState(First.Id)!.Reference);
        }
    }

    private sealed class InventorySource : IExecutableInventorySource
    {
        private TaskCompletionSource<bool>? _held;
        private TaskCompletionSource<bool>? _heldHandle;
        private TaskCompletionSource<bool>? _release;
        private bool _ignoreCancellation;
        public List<InstallationId> Calls { get; } = [];
        public long RevisionSize { get; set; } = 10;
        public bool Complete { get; set; } = true;
        public bool ThrowIoFailure { get; set; }

        public void HoldNext() => HoldNextIgnoringCancellation(false);
        public void HoldNextIgnoringCancellation() => HoldNextIgnoringCancellation(true);
        private void HoldNextIgnoringCancellation(bool ignore)
        {
            _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _heldHandle = _held;
            _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _ignoreCancellation = ignore;
        }
        public Task WaitForHoldAsync() => _heldHandle!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Release() => _release!.TrySetResult(true);

        public async Task<ExecutableInventory> InventoryAsync(InstallationScope scope,
            CancellationToken cancellationToken)
        {
            lock (Calls) Calls.Add(scope.InstallationId);
            var held = Interlocked.Exchange(ref _held, null);
            if (held is not null)
            {
                held.TrySetResult(true);
                var release = _release!;
                if (_ignoreCancellation) await release.Task;
                else await release.Task.WaitAsync(cancellationToken);
            }
            if (!_ignoreCancellation) cancellationToken.ThrowIfCancellationRequested();
            if (ThrowIoFailure) throw new IOException("Simulated inventory failure.");
            return Complete
                ? new ExecutableInventory(scope, InventoryCompleteness.Complete,
                    [new ExecutableCandidate(scope.RootPath + @"\Game.exe", "Game.exe",
                        new FileRevision(RevisionSize, Now))], [])
                : new ExecutableInventory(scope, InventoryCompleteness.Incomplete, [],
                    [new InventoryIssue(scope.RootPath, InventoryIssueKind.AccessDenied)]);
        }
    }

    private sealed class LearningStore : IProcessSignatureLearningStore
    {
        private readonly Dictionary<InstallationId, ProcessSignatureLearningState> _states = [];
        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_states) return Task.FromResult(_states.GetValueOrDefault(id));
        }
        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expected,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_states)
            {
                var id = state.Inventory.Scope.InstallationId;
                if (_states.GetValueOrDefault(id)?.ConcurrencyToken != expected)
                    return Task.FromResult(false);
                _states[id] = state;
                return Task.FromResult(true);
            }
        }
    }

    private sealed class SignatureStore : IProcessSignatureStore
    {
        public Task UpsertAsync(ProcessSignature signature, CancellationToken token) => Task.CompletedTask;
        public Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken token) =>
            Task.FromResult<ProcessSignature?>(null);
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ProcessSignature>>([]);
    }

    private sealed class RevisionSource : IExecutableRevisionSource
    {
        public Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string path,
            CancellationToken token) =>
            Task.FromResult(new ExecutableRevisionResult(new FileRevision(10, Now), null));
    }
}
