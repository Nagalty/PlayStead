using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class ProcessSignatureLearningCoordinatorQualityTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Preexisting_unrelated_null_path_does_not_invalidate_complete_episode()
    {
        var d = new Driver();
        await d.PrepareAsync();
        var unrelated = new ProcessSnapshot(900, "Other.exe", null, null);
        var game = d.Game(100);
        await d.ObserveAsync(1, [unrelated]);
        await d.ObserveAsync(2, [unrelated]);
        await d.ObserveAsync(3, [unrelated, game]);
        await d.ObserveAsync(4, [unrelated, game]);
        await d.ObserveAsync(5, [unrelated]);
        var decision = await d.ObserveAsync(6, [unrelated]);
        Assert.NotNull(d.Store.State!.Reference);
        Assert.Equal([DiscoveryReason.AwaitingIndependentEpisode], decision!.Reasons);
    }

    [Fact]
    public async Task Same_named_executable_outside_installation_root_is_unrelated()
    {
        var d = new Driver();
        await d.PrepareAsync();
        var otherGame = new ProcessSnapshot(900, "Game.exe", @"C:\Games\Other\Game.exe",
            Epoch.AddSeconds(3));
        await d.ObserveAsync(1, [otherGame]);
        await d.ObserveAsync(2, [otherGame]);
        await d.ObserveAsync(3, [otherGame, d.Game(100)]);
        await d.ObserveAsync(4, [otherGame, d.Game(100)]);
        await d.ObserveAsync(5, [otherGame]);
        var decision = await d.ObserveAsync(6, [otherGame]);
        Assert.Equal([DiscoveryReason.AwaitingIndependentEpisode], decision!.Reasons);
        Assert.NotNull(d.Store.State!.Reference);
    }

    [Fact]
    public async Task Preexisting_unknown_identity_that_disappears_cannot_reappear_during_episode()
    {
        var d = new Driver();
        await d.PrepareAsync();
        var unrelated = new ProcessSnapshot(900, "Other.exe", null, null);
        var game = d.Game(100);
        await d.ObserveAsync(1, [unrelated]);
        await d.ObserveAsync(2, [unrelated]);
        await d.ObserveAsync(3, [unrelated, game]);
        await d.ObserveAsync(4, [game]);
        var decision = await d.ObserveAsync(5, [unrelated]);
        Assert.NotNull(decision);
        Assert.Equal([DiscoveryReason.UnknownProcessIdentity], decision.Reasons);
        Assert.Null(d.Store.State!.Reference);
    }

    private sealed class Driver
    {
        public Driver()
        {
            Scope = new(GameId.New(), InstallationId.New(), @"C:\Games\Example", Guid.NewGuid(), true);
            var inventory = new ExecutableInventory(Scope, InventoryCompleteness.Complete,
                [new(@"C:\Games\Example\Game.exe", "Game.exe", new FileRevision(10, Epoch))], []);
            Context = new(inventory, false);
            Coordinator = new(Store, Store, Store, new());
        }
        public InstallationScope Scope { get; }
        public DiscoveryInventoryContext Context { get; }
        public Store Store { get; } = new();
        public ProcessSignatureLearningCoordinator Coordinator { get; }
        public ProcessSnapshot Game(int pid) =>
            new(pid, "Game.exe", @"C:\Games\Example\Game.exe", Epoch.AddSeconds(3));
        public async Task PrepareAsync() => await Coordinator.InitializeAsync(Context, CancellationToken.None);
        public Task<DiscoveryDecision?> ObserveAsync(int sequence, IReadOnlyList<ProcessSnapshot> processes) =>
            Coordinator.ObserveAsync(Scope.InstallationId,
                new(sequence, Epoch.AddMinutes(sequence), EpisodeQuality.Complete, processes),
                CancellationToken.None);
    }

    private sealed class Store : IProcessSignatureLearningStore, IProcessSignatureStore,
        IExecutableRevisionSource
    {
        public ProcessSignatureLearningState? State { get; private set; }
        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId id, CancellationToken token) =>
            Task.FromResult(State);
        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expected, CancellationToken token)
        {
            if (State?.ConcurrencyToken != expected) return Task.FromResult(false);
            State = state;
            return Task.FromResult(true);
        }
        public Task UpsertAsync(ProcessSignature signature, CancellationToken token) => Task.CompletedTask;
        public Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken token) =>
            Task.FromResult<ProcessSignature?>(null);
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ProcessSignature>>([]);
        public Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string path,
            CancellationToken token) => Task.FromResult(new ExecutableRevisionResult(new FileRevision(10, Epoch), null));
    }
}
