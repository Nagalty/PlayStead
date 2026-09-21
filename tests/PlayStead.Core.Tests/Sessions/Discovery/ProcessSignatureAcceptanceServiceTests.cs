using PlayStead.Core.Sessions.Discovery;
using PlayStead.Core.Sessions;
using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class ProcessSignatureAcceptanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void Acceptance_api_exists()
    {
        Assert.NotNull(typeof(ProcessSignatureDiscoveryPolicy).Assembly.GetType(
            "PlayStead.Core.Sessions.Discovery.ProcessSignatureAcceptanceService"));
    }

    [Fact]
    public async Task Exact_main_path_revision_scope_and_tokens_are_submitted()
    {
        var d = new Driver();
        Assert.True(await d.AcceptAsync());
        var write = Assert.Single(d.Writes);
        Assert.Equal(d.State.ConcurrencyToken, write.ExpectedLearningToken);
        Assert.Equal(d.State.Reference!.EpisodeId, write.ReferenceEpisodeId);
        Assert.Equal(d.State.Confirmation!.EpisodeId, write.ConfirmationEpisodeId);
        Assert.Equal(d.Scope.GameId.Value, write.Signature.GameId);
        Assert.Equal(ProcessSignatureOrigin.Discovered, write.Signature.Origin);
        Assert.Equal(Now, write.Signature.UpdatedAtUtc);
        var main = Assert.Single(write.Signature.Entries);
        Assert.Equal("Game.exe", main.ExecutableName);
        Assert.Equal(@"C:\Games\Example\Game.exe", main.ExecutablePath);
        Assert.Equal(ProcessSignatureEntryKind.Main, main.Kind);
        Assert.Equal(new FileRevision(10, Now.AddDays(-1)), main.ValidatedRevision);
        var metadata = write.Signature.Discovery!;
        Assert.Equal(d.Scope.InstallationId, metadata.InstallationId);
        Assert.Equal(d.Scope.GenerationId, metadata.GenerationId);
        Assert.Equal(ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            metadata.PolicyVersion);
        Assert.Equal(ProcessSignatureValidationState.Valid, metadata.ValidationState);
        Assert.NotEqual(Guid.Empty, metadata.ConcurrencyToken);
        Assert.NotEqual(d.State.ConcurrencyToken, metadata.ConcurrencyToken);
        Assert.Null(d.RevalidationExpectation);
        var session = Assert.Single(d.SessionStore.Sessions.Values);
        Assert.Equal(d.State.Confirmation!.EpisodeId, session.SessionId);
        Assert.Equal(["signature", "session"], d.PersistenceOrder);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_promote_decision_reaches_conditional_store(bool ambiguous)
    {
        var d = new Driver();
        d.State = new(d.State.Inventory, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            Guid.NewGuid(), 2, ambiguous,
            d.State.Reference, ambiguous ? d.State.Confirmation : d.State.Reference, []);
        d.Current = new(d.State.Inventory, ambiguous);
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
        Assert.Empty(d.SessionStore.Sessions);
    }

    [Fact]
    public async Task Reference_only_does_not_write_signature()
    {
        var d = new Driver();
        d.State = new(d.State.Inventory, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            Guid.NewGuid(), 1, false, d.State.Reference, null, []);
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
        Assert.Empty(d.SessionStore.Sessions);
    }

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public async Task Protected_origin_does_not_reach_acceptance(ProcessSignatureOrigin origin)
    {
        var d = new Driver();
        d.Existing = new(d.Scope.GameId.Value, [new("Explicit.exe", ProcessSignatureEntryKind.Main)], origin, Now);
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
        Assert.Empty(d.Invalidations);
    }

    [Fact]
    public async Task Existing_valid_discovered_is_not_replaced()
    {
        var d = new Driver();
        d.Existing = d.Discovered(ProcessSignatureValidationState.Valid);
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revalidation_uses_exact_old_expectation(bool legacy)
    {
        var d = new Driver();
        d.Existing = d.Discovered(ProcessSignatureValidationState.NeedsRevalidation, legacy);
        Assert.True(await d.AcceptAsync());
        Assert.Equal(new DiscoveredSignatureExpectation(d.Existing.Discovery!.ConcurrencyToken,
            d.Existing.Discovery.GenerationId, ProcessSignatureValidationState.NeedsRevalidation,
            d.Existing.Discovery.InstallationId), d.RevalidationExpectation);
        Assert.NotEqual(d.Existing.Discovery.ConcurrencyToken, Assert.Single(d.Writes).Signature.Discovery!.ConcurrencyToken);
    }

    [Fact]
    public async Task Pending_fresh_inventory_keeps_finished_proof()
    {
        var d = new Driver();
        var before = d.State;
        d.Current = null;
        Assert.False(await d.AcceptAsync());
        Assert.Same(before, d.State);
        Assert.Empty(d.Writes);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("revision")]
    [InlineData("candidate")]
    [InlineData("ambiguity")]
    public async Task Fresh_inventory_must_match_by_value_not_just_generation(string change)
    {
        var d = new Driver();
        var scope = change == "root" ? new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            @"C:\Other", d.Scope.GenerationId, true) : d.Scope;
        var candidates = change == "revision" ? new[] { new ExecutableCandidate(@"C:\Games\Example\Game.exe",
            "Game.exe", new FileRevision(11, Now)) } : d.State.Inventory.Candidates;
        if (change == "candidate") candidates = [];
        d.Current = new(new ExecutableInventory(scope, InventoryCompleteness.Complete, candidates, []), change == "ambiguity");
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
    }

    [Fact]
    public async Task Reconstructed_equivalent_inventory_is_accepted()
    {
        var d = new Driver();
        d.Current = new(new ExecutableInventory(new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            @"c:\games\example", d.Scope.GenerationId, true), InventoryCompleteness.Complete,
            [new ExecutableCandidate(@"c:\games\example\game.exe", "game.exe", new FileRevision(10, Now.AddDays(-1)))], []), false);
        Assert.True(await d.AcceptAsync());
    }

    [Fact]
    public async Task Stale_learning_conflict_is_not_retried()
    {
        var d = new Driver { CommitResult = false };
        Assert.False(await d.AcceptAsync());
        Assert.Single(d.Writes);
    }

    [Fact]
    public async Task Db_failure_is_not_reported_as_success()
    {
        var d = new Driver { FailCommit = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.AcceptAsync());
        Assert.Single(d.Writes);
        Assert.Empty(d.SessionStore.Sessions);
    }

    [Fact]
    public async Task Every_proof_revision_is_checked_before_acceptance()
    {
        var d = new Driver(companion: true);
        Assert.True(await d.AcceptAsync());
        Assert.Equal([@"C:\Games\Example\Game.exe", @"C:\Games\Example\Companion.exe"], d.ReadPaths);
        Assert.Equal(2, d.ReadCountAtCommit);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Changed_revision_prevents_write_and_clears_proof(bool existing, bool inaccessible)
    {
        var d = new Driver(companion: true);
        var before = d.State;
        if (existing) d.Existing = d.Discovered(ProcessSignatureValidationState.NeedsRevalidation);
        d.RevisionOverride = path => path.EndsWith("Companion.exe", StringComparison.Ordinal)
            ? inaccessible ? new(null, new InventoryIssue(path, InventoryIssueKind.AccessDenied))
                : new(new FileRevision(999, Now), null) : null;
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
        Assert.Null(d.State.Reference);
        Assert.Null(d.State.Confirmation);
        Assert.NotEqual(before.ConcurrencyToken, d.State.ConcurrencyToken);
        Assert.Equal(before.LastSequenceNumber, d.State.LastSequenceNumber);
        Assert.Same(before.Inventory, d.State.Inventory);
        Assert.Equal([DiscoveryReason.RevisionChanged], d.State.Reasons);
        if (existing)
        {
            Assert.Equal(new DiscoveredSignatureExpectation(d.Existing!.Discovery!.ConcurrencyToken,
                d.Existing.Discovery.GenerationId, d.Existing.Discovery.ValidationState,
                d.Existing.Discovery.InstallationId), Assert.Single(d.Invalidations));
        }
        else Assert.Empty(d.Invalidations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_before_acceptance_propagates(bool duringRevision)
    {
        var d = new Driver();
        using var cancellation = new CancellationTokenSource();
        if (duringRevision) d.AfterRead = () => cancellation.Cancel();
        else cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.AcceptAsync(cancellation.Token));
        Assert.Empty(d.Writes);
        Assert.Empty(d.Invalidations);
    }

    [Fact]
    public async Task Inventory_changed_during_revision_validation_prevents_submission()
    {
        var d = new Driver();
        d.AfterRead = () => d.Current = new(new ExecutableInventory(d.Scope, InventoryCompleteness.Complete,
            [new ExecutableCandidate(@"C:\Games\Example\Game.exe", "Game.exe", new FileRevision(100, Now))], []), false);
        Assert.False(await d.AcceptAsync());
        Assert.Empty(d.Writes);
    }

    [Fact]
    public async Task Lost_clear_proof_CAS_still_invalidates_exact_old_signature_and_never_accepts()
    {
        var d = new Driver { RejectClear = true };
        d.Existing = d.Discovered(ProcessSignatureValidationState.NeedsRevalidation);
        var old = d.State;
        d.RevisionOverride = _ => new(null, new InventoryIssue(@"C:\Games\Example\Game.exe", InventoryIssueKind.AccessDenied));
        Assert.False(await d.AcceptAsync());
        Assert.Same(old, d.State);
        Assert.Single(d.Invalidations);
        Assert.Empty(d.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Constructor_dependencies_are_required(int missing)
    {
        var d = new Driver();
        Assert.Throws<ArgumentNullException>(() => new ProcessSignatureAcceptanceService(
            missing == 0 ? null! : d, missing == 1 ? null! : d, missing == 2 ? null! : d,
            missing == 3 ? null! : d, missing == 4 ? null! : new(),
            missing == 5 ? null! : _ => d.Current, missing == 6 ? null! : new FixedTime(),
            missing == 7 ? null! : new DiscoveryConfirmationSessionPromoter(
                new TestSessionStore([]), new SessionTransitionPolicy())));
    }

    private sealed class Driver : IProcessSignatureLearningStore, IProcessSignatureStore,
        IProcessSignatureDiscoveryStore, IExecutableRevisionSource
    {
        public Driver(bool companion = false)
        {
            Scope = new(GameId.New(), InstallationId.New(), @"C:\Games\Example", Guid.NewGuid(), true);
            var candidates = new List<ExecutableCandidate> { new(@"C:\Games\Example\Game.exe", "Game.exe", new(10, Now.AddDays(-1))) };
            if (companion) candidates.Add(new(@"C:\Games\Example\Companion.exe", "Companion.exe", new(20, Now.AddDays(-1))));
            var inventory = new ExecutableInventory(Scope, InventoryCompleteness.Complete, candidates, []);
            LearningEpisodeSummary Episode(int n) => new(Guid.NewGuid(), n, Scope,
                ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
                Now.AddMinutes(n - 10), Now.AddMinutes(n - 10).AddSeconds(30), 1, 9, EpisodeQuality.Complete,
                candidates.Select(c => new CandidateEpisodeEvidence(c.ExecutablePath, c.Revision, true, true,
                    c.ExecutableName == "Game.exe" ? [new SnapshotRange(4, 7)] : [new SnapshotRange(3, 4)])).ToArray());
            State = new(inventory, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
                Guid.NewGuid(), 2, false, Episode(1), Episode(2), []);
            Current = new(inventory, false);
            SessionStore = new(PersistenceOrder);
            SessionPromoter = new(SessionStore, new());
        }
        public InstallationScope Scope { get; }
        public ProcessSignatureLearningState State { get; set; }
        public DiscoveryInventoryContext? Current { get; set; }
        public ProcessSignature? Existing { get; set; }
        public bool CommitResult { get; set; } = true;
        public bool FailCommit { get; set; }
        public bool RejectClear { get; set; }
        public List<DiscoveredSignatureWrite> Writes { get; } = [];
        public List<DiscoveredSignatureExpectation> Invalidations { get; } = [];
        public DiscoveredSignatureExpectation? RevalidationExpectation { get; private set; }
        public Func<string, ExecutableRevisionResult?>? RevisionOverride { get; set; }
        public Action? AfterRead { get; set; }
        public List<string> ReadPaths { get; } = [];
        public int ReadCountAtCommit { get; private set; }
        public TestSessionStore SessionStore { get; }
        public List<string> PersistenceOrder { get; } = [];
        public DiscoveryConfirmationSessionPromoter SessionPromoter { get; }
        public Task<bool> AcceptAsync(CancellationToken ct = default) => new ProcessSignatureAcceptanceService(
            this, this, this, this, new(), _ => Current, new FixedTime(), SessionPromoter)
            .TryAcceptAsync(Scope.InstallationId, ct);
        public ProcessSignature Discovered(ProcessSignatureValidationState state, bool legacy = false) =>
            new(Scope.GameId.Value, [new("Old.exe", ProcessSignatureEntryKind.Main)], ProcessSignatureOrigin.Discovered,
                Now.AddDays(-1), new(legacy ? null : Scope.InstallationId, legacy ? null : Scope.GenerationId,
                    legacy ? null : ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
                    state, Guid.NewGuid()));
        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId id, CancellationToken ct) => Task.FromResult<ProcessSignatureLearningState?>(State);
        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expected, CancellationToken ct)
        {
            Assert.Equal(State.ConcurrencyToken, expected);
            if (RejectClear) return Task.FromResult(false);
            State = state;
            return Task.FromResult(true);
        }
        public Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken ct) => Task.FromResult(Existing);
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken ct) => throw new InvalidOperationException("Not an acceptance operation.");
        public Task UpsertAsync(ProcessSignature signature, CancellationToken ct) => throw new InvalidOperationException("No Upsert fallback.");
        public Task<bool> TryInsertDiscoveredIfAbsentAsync(DiscoveredSignatureWrite write, CancellationToken ct)
        {
            Writes.Add(write);
            ReadCountAtCommit = ReadPaths.Count;
            if (FailCommit) throw new InvalidOperationException("Database unavailable.");
            if (CommitResult) PersistenceOrder.Add("signature");
            return Task.FromResult(CommitResult);
        }
        public Task<bool> TryRevalidateDiscoveredAsync(DiscoveredSignatureWrite write, DiscoveredSignatureExpectation expected, CancellationToken ct)
        {
            RevalidationExpectation = expected;
            return TryInsertDiscoveredIfAbsentAsync(write, ct);
        }
        public Task<bool> TryRestoreDiscoveredValidationAsync(Guid gameId,
            DiscoveredSignatureExpectation expected, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TryInvalidateDiscoveredAsync(Guid gameId, DiscoveredSignatureExpectation expected, CancellationToken ct)
        {
            Invalidations.Add(expected);
            return Task.FromResult(true);
        }
        public Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string path, CancellationToken ct)
        {
            Assert.Equal(Scope, scope);
            ReadPaths.Add(path);
            AfterRead?.Invoke();
            return Task.FromResult(RevisionOverride?.Invoke(path) ??
                new ExecutableRevisionResult(State.Inventory.Candidates.Single(c => c.ExecutablePath == path).Revision, null));
        }
    }

    private sealed class TestSessionStore(List<string> persistenceOrder) : ISessionStore
    {
        public Dictionary<Guid, GameSession> Sessions { get; } = [];
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken)
        {
            Sessions[session.SessionId] = session;
            persistenceOrder.Add("session");
            return Task.CompletedTask;
        }
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.GetValueOrDefault(sessionId));
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(Sessions.Values.Take(limit).ToArray());
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(Sessions.Values.Where(session => session.GameId == gameId).ToArray());
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
