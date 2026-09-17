using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;

namespace PlayStead.Core.Tests.Scanning;

public sealed class LocalIdentityResolutionCoordinatorTests
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-09-16T18:00:00Z");

    [Fact]
    public async Task Exact_provider_match_reconciles_once_without_creating_provisional()
    {
        var gameId = Game(1);
        var contentId = CatalogContentId.New();
        var evidence = ExactMatch(contentId);
        var store = new RecordingResolutionStore();
        var reconciler = new RecordingReconciler();
        var sut = CreateCoordinator(
            gameId,
            new IdentityResolutionResult(
                IdentityResolutionState.MatchConfirmed,
                contentId,
                evidence),
            store,
            reconciler);

        await sut.ResolveAfterScanAsync(
            SteamScan(),
            CancellationToken.None);

        var call = Assert.Single(reconciler.Calls);
        Assert.Equal(gameId, call.GameId);
        Assert.Equal(contentId, call.ContentId);
        Assert.Equal(evidence, call.Evidence);
        Assert.Equal(ObservedAt, call.ObservedAtUtc);
        Assert.Equal(0, store.GetOrCreateCount);
    }

    [Fact]
    public async Task New_result_creates_provisional_once()
    {
        var gameId = Game(1);
        var evidence = NoMatch();
        var store = new RecordingResolutionStore();
        var reconciler = new RecordingReconciler();
        var sut = CreateCoordinator(
            gameId,
            new IdentityResolutionResult(
                IdentityResolutionState.New,
                CandidateContentId: null,
                evidence),
            store,
            reconciler);

        await sut.ResolveAfterScanAsync(
            SteamScan(),
            CancellationToken.None);

        Assert.Equal(1, store.GetOrCreateCount);
        Assert.Equal(gameId, store.LastGameId);
        Assert.Equal(evidence, store.LastEvidence);
        Assert.Equal(ObservedAt, store.LastObservedAtUtc);
        Assert.Empty(reconciler.Calls);
    }

    [Fact]
    public async Task Unsupported_provider_is_ignored_conservatively()
    {
        var lookup = new RecordingLookup(Game(1));
        var resolver = new RecordingResolver(NewResult());
        var store = new RecordingResolutionStore();
        var reconciler = new RecordingReconciler();
        var sut = new LocalIdentityResolutionCoordinator(
            lookup,
            resolver,
            store,
            reconciler);

        await sut.ResolveAfterScanAsync(
            Scan(ProviderKind.Epic, "epic-ref"),
            CancellationToken.None);

        Assert.Equal(0, lookup.CallCount);
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.GetOrCreateCount);
        Assert.Empty(reconciler.Calls);
    }

    [Fact]
    public async Task Missing_local_game_is_skipped()
    {
        var lookup = new RecordingLookup(gameId: null);
        var resolver = new RecordingResolver(NewResult());
        var store = new RecordingResolutionStore();
        var reconciler = new RecordingReconciler();
        var sut = new LocalIdentityResolutionCoordinator(
            lookup,
            resolver,
            store,
            reconciler);

        await sut.ResolveAfterScanAsync(
            SteamScan(),
            CancellationToken.None);

        Assert.Equal(1, lookup.CallCount);
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.GetOrCreateCount);
        Assert.Empty(reconciler.Calls);
    }

    [Fact]
    public async Task Already_cancelled_token_is_propagated()
    {
        var sut = CreateCoordinator(
            Game(1),
            NewResult(),
            new RecordingResolutionStore(),
            new RecordingReconciler());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ResolveAfterScanAsync(
                SteamScan(),
                cancellation.Token));
    }

    [Fact]
    public async Task Match_probable_preserves_provisional_and_persists_candidate()
    {
        var candidate = CatalogContentId.New();
        var store = new RecordingResolutionStore();
        var sut = CreateCoordinator(
            Game(1),
            new IdentityResolutionResult(
                IdentityResolutionState.MatchProbable,
                candidate,
                NoMatch()),
            store,
            new RecordingReconciler());

        await sut.ResolveAfterScanAsync(
            SteamScan(),
            CancellationToken.None);

        Assert.Equal(1, store.GetOrCreateCount);
        var persisted = Assert.Single(store.Upserts);
        Assert.Equal(IdentityResolutionState.MatchProbable, persisted.State);
        Assert.Equal(candidate, persisted.CandidateContentId);
        Assert.NotNull(persisted.ProvisionalIdentityId);
    }

    [Fact]
    public async Task Ambiguous_preserves_provisional_without_canonical_attachment()
    {
        var store = new RecordingResolutionStore();
        var reconciler = new RecordingReconciler();
        var sut = CreateCoordinator(
            Game(1),
            new IdentityResolutionResult(
                IdentityResolutionState.Ambiguous,
                CatalogContentId.New(),
                NoMatch()),
            store,
            reconciler);

        await sut.ResolveAfterScanAsync(
            SteamScan(),
            CancellationToken.None);

        Assert.Equal(1, store.GetOrCreateCount);
        var persisted = Assert.Single(store.Upserts);
        Assert.Equal(IdentityResolutionState.Ambiguous, persisted.State);
        Assert.Null(persisted.CandidateContentId);
        Assert.NotNull(persisted.ProvisionalIdentityId);
        Assert.Empty(reconciler.Calls);
    }

    [Fact]
    public async Task Observation_uses_exact_scanned_provider_ref_title_and_time()
    {
        var resolver = new RecordingResolver(NewResult());
        var sut = new LocalIdentityResolutionCoordinator(
            new RecordingLookup(Game(1)),
            resolver,
            new RecordingResolutionStore(),
            new RecordingReconciler());

        await sut.ResolveAfterScanAsync(
            SteamScan(),
            CancellationToken.None);

        var observation = Assert.Single(resolver.Observations);
        Assert.Equal(CatalogProviderKind.Steam, observation.Provider);
        Assert.Equal("1874880", observation.ExternalId);
        Assert.Equal("Arma Reforger", observation.Title);
        Assert.Equal(ObservedAt, observation.ObservedAtUtc);
    }

    [Fact]
    public async Task Active_human_confirmation_skips_automatic_resolver()
    {
        var content = CatalogContentId.New();
        var resolver = new RecordingResolver(new IdentityResolutionResult(IdentityResolutionState.New, null, NoMatch()));
        var store = new RecordingDecisionStore(new GameIdentityDecision(IdentityDecisionId.New(), Game(1), content, IdentityDecisionType.UserConfirmed, ObservedAt, ObservedAt, null));
        var reconciler = new RecordingReconciler();
        var sut = new LocalIdentityResolutionCoordinator(new RecordingLookup(Game(1)), resolver, new RecordingResolutionStore(), reconciler, store);

        await sut.ResolveAfterScanAsync(SteamScan(), CancellationToken.None);

        Assert.Equal(0, resolver.CallCount);
        Assert.Empty(reconciler.Calls);
    }

    [Fact]
    public async Task Rejected_exact_match_becomes_provisional_without_reconciliation()
    {
        var content = CatalogContentId.New();
        var resolver = new RecordingResolver(new IdentityResolutionResult(IdentityResolutionState.MatchConfirmed, content, ExactMatch(content)));
        var store = new RecordingDecisionStore(rejected: [new GameIdentityDecision(IdentityDecisionId.New(), Game(1), content, IdentityDecisionType.UserRejected, ObservedAt, ObservedAt, null)]);
        var resolutionStore = new RecordingResolutionStore();
        var reconciler = new RecordingReconciler();
        var sut = new LocalIdentityResolutionCoordinator(new RecordingLookup(Game(1)), resolver, resolutionStore, reconciler, store);

        await sut.ResolveAfterScanAsync(SteamScan(), CancellationToken.None);

        Assert.Equal(1, resolutionStore.GetOrCreateCount);
        Assert.Empty(reconciler.Calls);
    }

    [Fact]
    public async Task Notification_failure_does_not_break_new_identity_persistence()
    {
        var store = new RecordingResolutionStore();
        var sut = new LocalIdentityResolutionCoordinator(
            new RecordingLookup(Game(1)),
            new RecordingResolver(NewResult()),
            store,
            new RecordingReconciler(),
            new ThrowingNotificationProducer(new IOException("notification failure")));

        await sut.ResolveAfterScanAsync(SteamScan(), CancellationToken.None);

        Assert.Equal(1, store.GetOrCreateCount);
    }

    [Fact]
    public async Task Notification_cancellation_is_propagated_after_confirmed_reconciliation()
    {
        using var cancellation = new CancellationTokenSource();
        var reconciler = new RecordingReconciler();
        var contentId = CatalogContentId.New();
        var sut = new LocalIdentityResolutionCoordinator(
            new RecordingLookup(Game(1)),
            new RecordingResolver(new IdentityResolutionResult(
                IdentityResolutionState.MatchConfirmed,
                contentId,
                ExactMatch(contentId))),
            new RecordingResolutionStore(),
            reconciler,
            new CancellingNotificationProducer(cancellation));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.ResolveAfterScanAsync(SteamScan(), cancellation.Token));

        Assert.Single(reconciler.Calls);
    }

    private static LocalIdentityResolutionCoordinator CreateCoordinator(
        GameId gameId,
        IdentityResolutionResult result,
        RecordingResolutionStore store,
        RecordingReconciler reconciler) =>
        new(
            new RecordingLookup(gameId),
            new RecordingResolver(result),
            store,
            reconciler);

    private static SourceScanResult SteamScan() =>
        Scan(ProviderKind.Steam, "1874880");

    private static SourceScanResult Scan(
        ProviderKind provider,
        string externalId) =>
        SourceScanResult.Success(
            provider,
            ObservedAt,
            [
                DiscoveredInstallation.Create(
                    provider,
                    externalId,
                    "Arma Reforger",
                    @"G:\Games\Arma Reforger",
                    42,
                    ObservedAt)
            ]);

    private static IdentityResolutionResult NewResult() =>
        new(
            IdentityResolutionState.New,
            CandidateContentId: null,
            NoMatch());

    private static IdentityResolutionEvidence NoMatch() =>
        new(
            IdentityResolutionEvidenceKind.NoExactProviderRefMatch,
            CatalogProviderKind.Steam,
            "1874880",
            MatchedContentId: null);

    private static IdentityResolutionEvidence ExactMatch(
        CatalogContentId contentId) =>
        new(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            CatalogProviderKind.Steam,
            "1874880",
            contentId);

    private static GameId Game(int index) =>
        new(Guid.Parse(
            $"00000000-0000-4000-8000-{index:000000000000}"));

    private sealed class RecordingLookup(GameId? gameId) : ILibraryGameLookup
    {
        public int CallCount { get; private set; }

        public Task<GameId?> FindGameIdByProviderRefAsync(
            ProviderKind provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(gameId);
        }
    }

    private sealed class RecordingResolver(
        IdentityResolutionResult result) : IGameIdentityResolver
    {
        public List<GameIdentityObservation> Observations { get; } = [];

        public int CallCount => Observations.Count;

        public Task<IdentityResolutionResult> ResolveAsync(
            GameIdentityObservation observation,
            CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingResolutionStore : IIdentityResolutionStore
    {
        private static readonly ProvisionalIdentityId Provisional =
            ProvisionalIdentityId.Parse(
                "PS-TEMP-01K5C0YQ8S0000000000000000");

        public int GetOrCreateCount { get; private set; }
        public GameId? LastGameId { get; private set; }
        public IdentityResolutionEvidence? LastEvidence { get; private set; }
        public DateTimeOffset? LastObservedAtUtc { get; private set; }
        public List<GameIdentityResolution> Upserts { get; } = [];

        public Task<GameIdentityResolution> GetOrCreateProvisionalAsync(
            GameId gameId,
            IdentityResolutionEvidence evidence,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            GetOrCreateCount++;
            LastGameId = gameId;
            LastEvidence = evidence;
            LastObservedAtUtc = observedAtUtc;
            return Task.FromResult(
                new GameIdentityResolution(
                    gameId,
                    Provisional,
                    IdentityResolutionState.New,
                    CandidateContentId: null,
                    evidence,
                    observedAtUtc,
                    observedAtUtc));
        }

        public Task UpsertAsync(
            GameIdentityResolution resolution,
            CancellationToken cancellationToken)
        {
            Upserts.Add(resolution);
            return Task.CompletedTask;
        }

        public Task<GameIdentityResolution?> GetAsync(
            GameId gameId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GameIdentityResolution>> ListAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDecisionStore(GameIdentityDecision? confirmed = null, IReadOnlyList<GameIdentityDecision>? rejected = null) : IIdentityDecisionStore
    {
        private readonly GameIdentityDecision? _confirmed = confirmed;
        private readonly IReadOnlyList<GameIdentityDecision> _rejected = rejected ?? [];
        public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult(_confirmed?.GameId == gameId ? _confirmed : null);
        public Task<IReadOnlyList<GameIdentityDecision>> ListActiveRejectedAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameIdentityDecision>>(_rejected.Where(x => x.GameId == gameId).ToArray());
        public Task<IReadOnlyList<GameIdentityDecision>> ListActiveAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameIdentityDecision>>(_rejected.Where(x => x.GameId == gameId).ToArray());
        public Task InsertAsync(GameIdentityDecision decision, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RevokeAsync(IdentityDecisionId decisionId, DateTimeOffset revokedUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingReconciler : ILocalIdentityReconciler
    {
        public List<ReconcileCall> Calls { get; } = [];

        public Task ReconcileAsync(
            GameId localGameId,
            CatalogContentId canonicalContentId,
            IdentityResolutionEvidence evidence,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            Calls.Add(
                new ReconcileCall(
                    localGameId,
                    canonicalContentId,
                    evidence,
                    observedAtUtc));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotificationProducer(Exception exception)
        : IIdentityNotificationProducer
    {
        public Task PublishForResolutionAsync(
            GameId gameId,
            IdentityResolutionResult result,
            CancellationToken cancellationToken) =>
            Task.FromException(exception);
    }

    private sealed class CancellingNotificationProducer(CancellationTokenSource cancellation)
        : IIdentityNotificationProducer
    {
        public Task PublishForResolutionAsync(
            GameId gameId,
            IdentityResolutionResult result,
            CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed record ReconcileCall(
        GameId GameId,
        CatalogContentId ContentId,
        IdentityResolutionEvidence Evidence,
        DateTimeOffset ObservedAtUtc);
}
