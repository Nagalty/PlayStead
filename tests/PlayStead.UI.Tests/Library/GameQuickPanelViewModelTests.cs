using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
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
        Assert.Equal("2 sessions", viewModel.SessionCountLabel);
        Assert.Equal("1 h 00 min", viewModel.LastSessionDurationLabel);
        Assert.Equal("2 h 00 min", viewModel.TotalPlayTimeLabel);
        Assert.NotEqual("—", viewModel.LastSessionDateLabel);
        Assert.NotEqual("Aucune activité PlayStead", viewModel.LastActivityLabel);
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
        Assert.Equal("0 session", viewModel.SessionCountLabel);
        Assert.Equal("0 min", viewModel.TotalPlayTimeLabel);
        Assert.Equal("—", viewModel.LastSessionDurationLabel);
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
}
