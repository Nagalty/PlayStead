using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Notifications;

public sealed class IdentityDecisionNotificationOrchestrator : IIdentityDecisionNotificationOrchestrator
{
    private readonly IIdentityDecisionService _decisions;
    private readonly IIdentityDecisionContextProvider _contexts;
    private readonly INotificationCenterService _notifications;

    public IdentityDecisionNotificationOrchestrator(IIdentityDecisionService decisions, IIdentityDecisionContextProvider contexts, INotificationCenterService notifications)
    { ArgumentNullException.ThrowIfNull(decisions); ArgumentNullException.ThrowIfNull(contexts); ArgumentNullException.ThrowIfNull(notifications); _decisions = decisions; _contexts = contexts; _notifications = notifications; }

    public async Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = await RequireCandidateAsync(gameId, catalogContentId, cancellationToken);
        await _decisions.ConfirmAsync(gameId, catalogContentId, DateTimeOffset.UtcNow, cancellationToken);
        try { await ResolveAsync(gameId, context.State, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { }
    }

    public async Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = await RequireCandidateAsync(gameId, catalogContentId, cancellationToken);
        await _decisions.RejectAsync(gameId, catalogContentId, DateTimeOffset.UtcNow, cancellationToken);
        try
        {
            var next = await _contexts.GetAsync(gameId, cancellationToken);
            await ApplyRejectTransitionAsync(gameId, context.State, next, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { }
    }

    public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken) =>
        _decisions.GetActiveConfirmedAsync(gameId, cancellationToken);

    public Task RevokeConfirmedAsync(GameId gameId, CancellationToken cancellationToken) =>
        _decisions.RevokeConfirmedAsync(gameId, DateTimeOffset.UtcNow, cancellationToken);

    private async Task<IdentityDecisionContext> RequireCandidateAsync(GameId gameId, CatalogContentId candidate, CancellationToken token)
    {
        var context = await _contexts.GetAsync(gameId, token) ?? throw new InvalidOperationException("No identity decision context is available.");
        if (!context.Candidates.Any(x => x.CatalogContentId == candidate)) throw new InvalidOperationException("The candidate is not present in the current identity decision context.");
        return context;
    }

    private Task ResolveAsync(GameId gameId, IdentityResolutionState state, CancellationToken token) =>
        ResolveKeyAsync($"identity:{gameId}:{(state == IdentityResolutionState.Ambiguous ? "ambiguous" : "match-probable")}", token);

    private async Task ApplyRejectTransitionAsync(GameId gameId, IdentityResolutionState previous, IdentityDecisionContext? next, CancellationToken token)
    {
        if (next is null || next.State == IdentityResolutionState.New)
        { await ResolveAsync(gameId, previous, token); return; }
        if (previous == IdentityResolutionState.Ambiguous && next.State == IdentityResolutionState.MatchProbable)
        { await ResolveKeyAsync($"identity:{gameId}:ambiguous", token); await PublishMatchProbableAsync(gameId, token); return; }
        if (next.State == IdentityResolutionState.Ambiguous) await PublishAmbiguousAsync(gameId, token);
    }

    private async Task ResolveKeyAsync(string key, CancellationToken token)
    {
        var active = await _notifications.ListAsync(NotificationListFilter.Active, token);
        foreach (var notification in active.Where(x => x.DeduplicationKey == key)) await _notifications.ResolveAsync(notification.NotificationId, token);
    }

    private Task PublishMatchProbableAsync(GameId gameId, CancellationToken token) => PublishAsync(gameId, "match-probable", "Identité du jeu à vérifier", "Une correspondance probable nécessite votre vérification.", token);
    private Task PublishAmbiguousAsync(GameId gameId, CancellationToken token) => PublishAsync(gameId, "ambiguous", "Identité du jeu ambiguë", "Plusieurs correspondances sont possibles et nécessitent votre vérification.", token);
    private Task<NotificationRecord> PublishAsync(GameId gameId, string reason, string title, string message, CancellationToken token) => _notifications.PublishOrRefreshAsync(new NotificationPublishRequest(NotificationProducer.IdentityResolution, gameId.ToString(), reason, new NotificationDeduplicationKey($"identity:{gameId}:{reason}"), NotificationPriority.ActionRequired, title, message, null), token);
}
