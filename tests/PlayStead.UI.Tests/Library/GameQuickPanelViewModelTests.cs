using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.GameBuildHistory;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Library;

public sealed class GameQuickPanelViewModelTests
{
    [Fact]
    public async Task LoadSessionSummaryAsync_uses_corrected_completed_sessions_for_activity_and_totals()
    {
        var gameId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555");

        var first =
            EndedSession(
                Guid.Parse(
                    "aaaaaaaa-1111-1111-1111-111111111111"),
                gameId,
                new DateTimeOffset(
                    2026, 9, 10, 18, 0, 0,
                    TimeSpan.Zero),
                TimeSpan.FromHours(1));

        var second =
            EndedSession(
                Guid.Parse(
                    "bbbbbbbb-2222-2222-2222-222222222222"),
                gameId,
                new DateTimeOffset(
                    2026, 9, 12, 20, 0, 0,
                    TimeSpan.Zero),
                TimeSpan.FromMinutes(30));

        var correction =
            new SessionCorrection(
                Guid.NewGuid(),
                second.SessionId,
                second.ObservedStartedAtUtc.AddMinutes(-10),
                second.ObservedEndedAtUtc!.Value.AddMinutes(20),
                "test",
                second.UpdatedAtUtc.AddMinutes(1));

        var sessionStore =
            new FakeSessionStore(
                [second, first]);

        var correctionStore =
            new FakeCorrectionStore(
                new Dictionary<Guid, SessionCorrection>
                {
                    [second.SessionId] = correction
                });

        var game =
            new LibraryItemViewModel(
                new GameId(gameId),
                "Game",
                ProviderKind.Steam,
                "Steam",
                @"C:\Game",
                10_000_000_000);

        var viewModel =
            new GameQuickPanelViewModel(
                game,
                new NavigationService(),
                launch: null,
                sessionStore,
                correctionStore,
                new SessionCorrectionPolicy());

        await viewModel.LoadSessionSummaryAsync(
            CancellationToken.None);

        Assert.True(viewModel.HasSessionHistory);
        Assert.Equal("2 sessions connues", viewModel.SessionCountLabel);
        Assert.Equal("1 h 00 min", viewModel.LastSessionDurationLabel);
        Assert.Equal("Inconnu", viewModel.TotalPlayTimeLabel);
        Assert.NotEqual("—", viewModel.LastSessionDateLabel);
        Assert.NotEqual("Pas encore d’activité connue pour ce jeu.", viewModel.LastActivityLabel);
    }

    [Fact]
    public async Task LoadSessionSummaryAsync_reports_empty_history()
    {
        var gameId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555");

        var game =
            new LibraryItemViewModel(
                new GameId(gameId),
                "Game",
                ProviderKind.Steam,
                "Steam",
                @"C:\Game",
                null);

        var viewModel =
            new GameQuickPanelViewModel(
                game,
                new NavigationService(),
                launch: null,
                new FakeSessionStore([]),
                new FakeCorrectionStore(
                    new Dictionary<Guid, SessionCorrection>()),
                new SessionCorrectionPolicy());

        await viewModel.LoadSessionSummaryAsync(
            CancellationToken.None);

        Assert.False(viewModel.HasSessionHistory);
        Assert.Equal("0 session connue", viewModel.SessionCountLabel);
        Assert.Equal("Inconnu", viewModel.TotalPlayTimeLabel);
        Assert.Equal("—", viewModel.LastSessionDurationLabel);
        Assert.False(viewModel.HasKnownSessionHistory);
        Assert.Empty(viewModel.KnownSessionHistoryLabel);
    }

    [Fact]
    public async Task Provider_activity_is_primary_but_observed_playtime_remains_separate()
    {
        var gameId = new GameId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var game = new LibraryItemViewModel(gameId, "Game", ProviderKind.Steam, "Steam", @"C:\\Game", null);
        var providerLastPlayed = new DateTimeOffset(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);
        var providerStore = new FakeProviderActivityStore(new ProviderActivityMetadata(
            gameId,
            ProviderKind.Steam,
            "123",
            TimeSpan.FromHours(428),
            providerLastPlayed,
            DateTimeOffset.UtcNow,
            ProviderActivityAvailability.Complete));
        var viewModel = new GameQuickPanelViewModel(
            game,
            new NavigationService(),
            launch: null,
            new FakeSessionStore([]),
            new FakeCorrectionStore(new Dictionary<Guid, SessionCorrection>()),
            new SessionCorrectionPolicy(),
            providerStore);

        await viewModel.LoadSessionSummaryAsync(CancellationToken.None);

        Assert.Equal("428 h", viewModel.TotalPlayTimeLabel);
        Assert.Equal("0 min", viewModel.PlaySteadTotalPlayTimeLabel);
        Assert.Equal("Steam", viewModel.ProviderActivitySourceLabel);
        Assert.NotNull(viewModel.ProviderLastPlayedLabel);
    }

    [Fact]
    public async Task Session_count_label_uses_the_reconciled_effective_count()
    {
        var gameId = GameId.New();
        var game = new LibraryItemViewModel(gameId, "Game", ProviderKind.Steam, "Steam", @"C:\Game", null);
        var observed = EndedSession(Guid.NewGuid(), gameId.Value, DateTimeOffset.UtcNow.AddHours(-1), TimeSpan.FromMinutes(20));
        var viewModel = new GameQuickPanelViewModel(
            game,
            new NavigationService(),
            launch: null,
            new FakeSessionStore([observed]),
            new FakeCorrectionStore(new Dictionary<Guid, SessionCorrection>()),
            new SessionCorrectionPolicy());
        viewModel.AttachEffectiveActivityService(new FakeEffectiveActivityService(
            new EffectiveActivitySnapshot(
                gameId, null, EffectiveActivitySource.Unknown, EffectiveActivityCoverage.Unknown,
                DateTimeOffset.UtcNow, EffectiveActivitySource.PlaySteadObservedSessions,
                TimeSpan.FromMinutes(20), 1, TimeSpan.FromHours(2), 2,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            {
                EffectiveSessionCount = 3,
                KnownSessionHistoryStartUtc = new DateTimeOffset(2025, 2, 14, 10, 0, 0, TimeSpan.Zero)
            }));

        await viewModel.LoadSessionSummaryAsync(CancellationToken.None);

        Assert.Equal("3 sessions connues", viewModel.SessionCountLabel);
        Assert.StartsWith("Historique connu depuis ", viewModel.KnownSessionHistoryLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void Attention_state_exposes_one_compact_since_last_play_summary()
    {
        var game = new LibraryItemViewModel(
            GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Game", null);
        var viewModel = new GameQuickPanelViewModel(game, new NavigationService());

        Assert.False(viewModel.HasAttention);
        Assert.False(viewModel.HasSinceLastPlaySummary);

        viewModel.SetAttentionState(true);

        Assert.True(viewModel.HasAttention);
        Assert.True(viewModel.HasSinceLastPlaySummary);
        Assert.Equal("Mise à jour disponible", viewModel.SinceLastPlaySummary);
    }

    [Fact]
    public async Task Since_last_play_summary_reuses_build_history_service()
    {
        var gameId = GameId.New();
        var playedAt = new DateTimeOffset(2026, 9, 10, 18, 0, 0, TimeSpan.Zero);
        var game = new LibraryItemViewModel(gameId, "Game", ProviderKind.Steam, "Steam", @"C:\Game", null);
        var buildHistory = new GameBuildHistoryService(
            new FakeBuildHistoryStore(
                [
                    new GameBuildObservation(gameId, ProviderKind.Steam, "123", "1", playedAt.AddDays(-1)),
                    new GameBuildObservation(gameId, ProviderKind.Steam, "2", "2", playedAt.AddDays(1))
                ]),
            new FakeSessionStore([EndedSession(Guid.NewGuid(), gameId.Value, playedAt, TimeSpan.FromHours(1))]));
        var viewModel = new GameQuickPanelViewModel(
            game,
            new NavigationService(),
            launch: null,
            new FakeSessionStore([]),
            new FakeCorrectionStore(new Dictionary<Guid, SessionCorrection>()),
            new SessionCorrectionPolicy(),
            gameBuildHistoryService: buildHistory);

        await viewModel.LoadSessionSummaryAsync(CancellationToken.None);

        Assert.True(viewModel.HasSinceLastPlaySummary);
        Assert.Equal("Il s’est passé quelque chose depuis ta dernière partie.", viewModel.SinceLastPlaySummary);
    }

    private static GameSession EndedSession(
        Guid sessionId,
        Guid gameId,
        DateTimeOffset startedAtUtc,
        TimeSpan duration)
    {
        var endedAtUtc =
            startedAtUtc + duration;

        return new GameSession(
            sessionId,
            gameId,
            startedAtUtc,
            endedAtUtc,
            endedAtUtc,
            SessionState.Ended,
            SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            startedAtUtc,
            endedAtUtc);
    }

    private sealed class FakeSessionStore(
        IReadOnlyList<GameSession> sessions)
        : ISessionStore
    {
        public Task UpsertAsync(
            GameSession session,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<GameSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<GameSession?>(
                sessions.FirstOrDefault(
                    session =>
                        session.SessionId == sessionId));

        public Task<IReadOnlyList<GameSession>> GetActiveAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(
                sessions.Where(
                    session =>
                        session.State ==
                        SessionState.Active).ToArray());

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(
                sessions.Take(limit).ToArray());

        public Task<IReadOnlyList<GameSession>> GetByGameAsync(
            Guid gameId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(
                sessions.Where(
                    session =>
                        session.GameId ==
                        gameId).ToArray());
    }

    private sealed class FakeCorrectionStore(
        IReadOnlyDictionary<Guid, SessionCorrection> corrections)
        : ISessionCorrectionStore
    {
        public Task UpsertAsync(
            SessionCorrection correction,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<SessionCorrection?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                corrections.TryGetValue(
                    sessionId,
                    out var correction)
                    ? correction
                    : null);
    }

    private sealed class FakeProviderActivityStore(ProviderActivityMetadata value) : IProviderActivityMetadataStore
    {
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>([value]);

        public Task UpsertAsync(ProviderActivityMetadata metadata, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeEffectiveActivityService(EffectiveActivitySnapshot snapshot) : IEffectiveActivityService
    {
        public Task<EffectiveActivitySnapshot> GetAsync(
            GameId gameId,
            ProviderKind provider,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class FakeBuildHistoryStore(IReadOnlyList<GameBuildObservation> observations) : IGameBuildHistoryStore
    {
        public Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<GameBuildObservation?>(observations.LastOrDefault());

        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(observations.Where(item => item.GameId == gameId).ToArray());

        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(IReadOnlyCollection<GameId> gameIds, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(observations.Where(item => gameIds.Contains(item.GameId)).ToArray());

        public Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
