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

    private sealed class FakeContexts(params IdentityDecisionContext[] values) : IIdentityDecisionContextProvider { private readonly Queue<IdentityDecisionContext?> _q = new(values); public Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken token) => Task.FromResult(_q.Count > 0 ? _q.Dequeue() : null); }
    private sealed class FakeDecisions : IIdentityDecisionService { public CatalogContentId? Confirmed; public CatalogContentId? Rejected; public bool Throw; public Task<GameIdentityDecision> ConfirmAsync(GameId g, CatalogContentId c, DateTimeOffset t, CancellationToken x) { if(Throw) throw new InvalidOperationException(); Confirmed=c; return Task.FromResult(new GameIdentityDecision(IdentityDecisionId.New(),g,c,IdentityDecisionType.UserConfirmed,t,t,null)); } public Task<GameIdentityDecision> RejectAsync(GameId g, CatalogContentId c, DateTimeOffset t, CancellationToken x) { Rejected=c; return Task.FromResult(new GameIdentityDecision(IdentityDecisionId.New(),g,c,IdentityDecisionType.UserRejected,t,t,null)); } public Task<GameIdentityDecision?> RevokeConfirmedAsync(GameId g, DateTimeOffset t, CancellationToken x) => Task.FromResult<GameIdentityDecision?>(null); }
    private sealed class FakeNotifications(GameId game, string reason) : INotificationCenterService { public List<string> ResolvedKeys { get; } = []; public List<string> PublishedKeys { get; } = []; private readonly NotificationRecord _active = new(NotificationId.New(),NotificationProducer.IdentityResolution,game.ToString(),reason,$"identity:{game}:{reason}",NotificationPriority.ActionRequired,NotificationState.Unread,"t","m",null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,null,null); public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest r, CancellationToken t) { PublishedKeys.Add(r.DeduplicationKey.Value); return Task.FromResult(_active with { DeduplicationKey = r.DeduplicationKey.Value }); } public Task<NotificationRecord> MarkReadAsync(NotificationId i,CancellationToken t)=>throw new NotSupportedException(); public Task<NotificationRecord> ResolveAsync(NotificationId i,CancellationToken t) { ResolvedKeys.Add(_active.DeduplicationKey); return Task.FromResult(_active with { State = NotificationState.Resolved }); } public Task<int> GetActiveCountAsync(CancellationToken t)=>Task.FromResult(0); public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter f,CancellationToken t)=>Task.FromResult<IReadOnlyList<NotificationRecord>>(f == NotificationListFilter.Active ? [_active] : []); public Task<int> PurgeExpiredResolvedAsync(CancellationToken t)=>Task.FromResult(0); }
}
