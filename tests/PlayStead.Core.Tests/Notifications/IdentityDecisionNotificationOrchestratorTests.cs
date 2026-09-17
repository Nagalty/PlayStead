using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;

namespace PlayStead.Core.Tests.Notifications;

public sealed class IdentityDecisionNotificationOrchestratorTests
{
    [Fact]
    public async Task Confirm_match_probable_calls_decision_and_resolves_stable_notification()
    {
        var game = GameId.New(); var candidate = new IdentityDecisionCandidate(CatalogContentId.New()); var notifications = new FakeNotifications(game, "match-probable"); var decisions = new FakeDecisions();
        var sut = new IdentityDecisionNotificationOrchestrator(decisions, new FakeContexts(IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [candidate])), notifications);
        await sut.ConfirmAsync(game, candidate.CatalogContentId, CancellationToken.None);
        Assert.Equal(candidate.CatalogContentId, decisions.Confirmed);
        Assert.Contains("identity:" + game + ":match-probable", notifications.ResolvedKeys);
    }

    [Fact]
    public async Task Reject_ambiguous_leaving_one_publishes_match_probable_and_resolves_ambiguous()
    {
        var game = GameId.New(); var a = new IdentityDecisionCandidate(CatalogContentId.New()); var b = new IdentityDecisionCandidate(CatalogContentId.New()); var notifications = new FakeNotifications(game, "ambiguous"); var decisions = new FakeDecisions();
        var contexts = new FakeContexts(IdentityDecisionContext.Create(game, IdentityResolutionState.Ambiguous, [a, b]), IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [b]));
        var sut = new IdentityDecisionNotificationOrchestrator(decisions, contexts, notifications);
        await sut.RejectAsync(game, a.CatalogContentId, CancellationToken.None);
        Assert.Equal(a.CatalogContentId, decisions.Rejected); Assert.Contains("identity:" + game + ":ambiguous", notifications.ResolvedKeys); Assert.Contains("identity:" + game + ":match-probable", notifications.PublishedKeys);
    }

    [Fact]
    public async Task Decision_failure_does_not_touch_notifications()
    {
        var game = GameId.New(); var candidate = new IdentityDecisionCandidate(CatalogContentId.New()); var notifications = new FakeNotifications(game, "match-probable"); var decisions = new FakeDecisions { Throw = true };
        var sut = new IdentityDecisionNotificationOrchestrator(decisions, new FakeContexts(IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [candidate])), notifications);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ConfirmAsync(game, candidate.CatalogContentId, CancellationToken.None)); Assert.Empty(notifications.ResolvedKeys);
    }

    [Fact]
    public async Task Active_confirmation_and_revocation_delegate_without_touching_notifications()
    {
        var game = GameId.New(); var confirmed = new GameIdentityDecision(IdentityDecisionId.New(), game, CatalogContentId.New(), IdentityDecisionType.UserConfirmed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var notifications = new FakeNotifications(game, "match-probable"); var decisions = new FakeDecisions { ActiveConfirmed = confirmed }; using var cts = new CancellationTokenSource(); var before = DateTimeOffset.UtcNow;
        var sut = new IdentityDecisionNotificationOrchestrator(decisions, new FakeContexts(), notifications);

        Assert.Same(confirmed, await sut.GetActiveConfirmedAsync(game, cts.Token));
        await sut.RevokeConfirmedAsync(game, cts.Token);

        Assert.Equal(game, decisions.ReadGame); Assert.Equal(game, decisions.RevokedGame); Assert.Equal(cts.Token, decisions.ReadToken); Assert.Equal(cts.Token, decisions.RevokeToken); Assert.Equal(1, decisions.ReadCalls); Assert.Equal(1, decisions.RevokeCalls); Assert.InRange(decisions.RevokedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(0, notifications.CallCount);
    }

    [Fact]
    public async Task Revocation_failure_and_cancellation_do_not_touch_notifications()
    {
        var game = GameId.New(); var notifications = new FakeNotifications(game, "match-probable");
        var failing = new IdentityDecisionNotificationOrchestrator(new FakeDecisions { Throw = true }, new FakeContexts(), notifications);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.RevokeConfirmedAsync(game, CancellationToken.None));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var cancelled = new IdentityDecisionNotificationOrchestrator(new FakeDecisions(), new FakeContexts(), notifications);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.RevokeConfirmedAsync(game, cts.Token));
        Assert.Equal(0, notifications.CallCount);
    }

    [Fact]
    public async Task Revocation_with_no_active_confirmation_does_not_touch_notifications()
    {
        var game = GameId.New(); var notifications = new FakeNotifications(game, "match-probable"); var decisions = new FakeDecisions();
        var sut = new IdentityDecisionNotificationOrchestrator(decisions, new FakeContexts(), notifications);
        await sut.RevokeConfirmedAsync(game, CancellationToken.None);
        Assert.Equal(1, decisions.RevokeCalls); Assert.Equal(0, notifications.CallCount);
    }

    private sealed class FakeContexts(params IdentityDecisionContext[] values) : IIdentityDecisionContextProvider { private readonly Queue<IdentityDecisionContext?> _q = new(values); public Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken token) => Task.FromResult(_q.Count > 0 ? _q.Dequeue() : null); }
    private sealed class FakeDecisions : IIdentityDecisionService { public CatalogContentId? Confirmed; public CatalogContentId? Rejected; public bool Throw; public GameId ReadGame; public GameId RevokedGame; public GameIdentityDecision? ActiveConfirmed; public int ReadCalls; public int RevokeCalls; public CancellationToken ReadToken; public CancellationToken RevokeToken; public DateTimeOffset RevokedAt; public Task<GameIdentityDecision> ConfirmAsync(GameId g, CatalogContentId c, DateTimeOffset t, CancellationToken x) { if(Throw) throw new InvalidOperationException(); Confirmed=c; return Task.FromResult(new GameIdentityDecision(IdentityDecisionId.New(),g,c,IdentityDecisionType.UserConfirmed,t,t,null)); } public Task<GameIdentityDecision> RejectAsync(GameId g, CatalogContentId c, DateTimeOffset t, CancellationToken x) { Rejected=c; return Task.FromResult(new GameIdentityDecision(IdentityDecisionId.New(),g,c,IdentityDecisionType.UserRejected,t,t,null)); } public Task<GameIdentityDecision?> RevokeConfirmedAsync(GameId g, DateTimeOffset t, CancellationToken x) { RevokeCalls++; RevokeToken=x; RevokedAt=t; x.ThrowIfCancellationRequested(); if(Throw) throw new InvalidOperationException(); RevokedGame=g; return Task.FromResult(ActiveConfirmed); } public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId g, CancellationToken x) { ReadCalls++; ReadToken=x; ReadGame=g; x.ThrowIfCancellationRequested(); return Task.FromResult(ActiveConfirmed); } }
    private sealed class FakeNotifications(GameId game, string reason) : INotificationCenterService { public int CallCount { get; private set; } public List<string> ResolvedKeys { get; } = []; public List<string> PublishedKeys { get; } = []; private readonly NotificationRecord _active = new(NotificationId.New(),NotificationProducer.IdentityResolution,game.ToString(),reason,$"identity:{game}:{reason}",NotificationPriority.ActionRequired,NotificationState.Unread,"t","m",null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,null,null); public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest r, CancellationToken t) { CallCount++; PublishedKeys.Add(r.DeduplicationKey.Value); return Task.FromResult(_active with { DeduplicationKey = r.DeduplicationKey.Value }); } public Task<NotificationRecord> MarkReadAsync(NotificationId i,CancellationToken t) { CallCount++; throw new NotSupportedException(); } public Task<NotificationRecord> ResolveAsync(NotificationId i,CancellationToken t) { CallCount++; ResolvedKeys.Add(_active.DeduplicationKey); return Task.FromResult(_active with { State = NotificationState.Resolved }); } public Task<int> GetActiveCountAsync(CancellationToken t) { CallCount++; return Task.FromResult(0); } public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter f,CancellationToken t) { CallCount++; return Task.FromResult<IReadOnlyList<NotificationRecord>>(f == NotificationListFilter.Active ? [_active] : []); } public Task<int> PurgeExpiredResolvedAsync(CancellationToken t) { CallCount++; return Task.FromResult(0); } }
}
