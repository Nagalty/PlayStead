using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class DiscoveryConfirmationSessionPromoterTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 19, 13, 24, 4, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = new(2026, 9, 19, 13, 26, 18, TimeSpan.Zero);

    [Fact]
    public async Task Confirmation_becomes_one_completed_process_monitor_session()
    {
        var store = new SessionStore();
        var promoter = new DiscoveryConfirmationSessionPromoter(store, new SessionTransitionPolicy());
        var episode = Episode();

        await promoter.PersistAsync(episode, CancellationToken.None);

        var session = Assert.Single(store.Sessions.Values);
        Assert.Equal(episode.EpisodeId, session.SessionId);
        Assert.Equal(episode.Scope.GameId.Value, session.GameId);
        Assert.Equal(Started, session.ObservedStartedAtUtc);
        Assert.Equal(Ended, session.LastSeenAtUtc);
        Assert.Equal(Ended, session.ObservedEndedAtUtc);
        Assert.Equal(SessionState.Ended, session.State);
        Assert.Equal(SessionEndReason.ProcessExited, session.EndReason);
        Assert.Equal(SessionDetectionSource.ProcessMonitor, session.DetectionSource);
    }

    [Fact]
    public async Task Same_confirmation_is_idempotent_across_retry_and_new_promoter_instance()
    {
        var store = new SessionStore();
        var episode = Episode();

        await new DiscoveryConfirmationSessionPromoter(store, new()).PersistAsync(episode, CancellationToken.None);
        await new DiscoveryConfirmationSessionPromoter(store, new()).PersistAsync(episode, CancellationToken.None);

        Assert.Single(store.Sessions);
        Assert.Equal(1, store.UpsertCount);
    }

    [Fact]
    public async Task Incomplete_episode_never_creates_session()
    {
        var store = new SessionStore();
        var promoter = new DiscoveryConfirmationSessionPromoter(store, new());
        var incomplete = Episode() with { };
        incomplete = new LearningEpisodeSummary(incomplete.EpisodeId, incomplete.SequenceNumber,
            incomplete.Scope, incomplete.PolicyVersion, incomplete.StartedAtUtc, incomplete.EndedAtUtc,
            incomplete.FirstSnapshot, incomplete.LastSnapshot, EpisodeQuality.Partial, incomplete.Candidates);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            promoter.PersistAsync(incomplete, CancellationToken.None));
        Assert.Empty(store.Sessions);
    }

    private static LearningEpisodeSummary Episode()
    {
        var scope = new InstallationScope(GameId.New(), InstallationId.New(), @"C:\Games\Example",
            Guid.NewGuid(), true);
        return new LearningEpisodeSummary(Guid.NewGuid(), 2, scope,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Started, Ended, 24, 87,
            EpisodeQuality.Complete,
            [new CandidateEpisodeEvidence(@"C:\Games\Example\Game.exe", null, true, true,
                [new SnapshotRange(27, 84)])]);
    }

    private sealed class SessionStore : ISessionStore
    {
        public Dictionary<Guid, GameSession> Sessions { get; } = [];
        public int UpsertCount { get; private set; }

        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sessions[session.SessionId] = session;
            UpsertCount++;
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
}
