namespace PlayStead.Core.Sessions.Discovery;

public sealed class DiscoveryConfirmationSessionPromoter
{
    private readonly ISessionStore _sessionStore;
    private readonly SessionTransitionPolicy _transitions;

    public DiscoveryConfirmationSessionPromoter(
        ISessionStore sessionStore,
        SessionTransitionPolicy transitions)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
    }

    public async Task PersistAsync(
        LearningEpisodeSummary confirmation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        cancellationToken.ThrowIfCancellationRequested();
        if (confirmation.Quality != EpisodeQuality.Complete)
            throw new InvalidOperationException("Only a complete confirmation episode can become a session.");

        if (await _sessionStore.GetAsync(confirmation.EpisodeId, cancellationToken) is { } existing)
        {
            if (existing.GameId != confirmation.Scope.GameId.Value ||
                existing.ObservedStartedAtUtc != confirmation.StartedAtUtc ||
                existing.ObservedEndedAtUtc != confirmation.EndedAtUtc)
                throw new InvalidOperationException("The confirmation episode identity is already correlated to another session.");
            return;
        }

        var started = _transitions.Start(
            confirmation.Scope.GameId.Value,
            confirmation.StartedAtUtc,
            confirmation.EndedAtUtc).Session
            ?? throw new InvalidOperationException("Session start transition did not produce a session.");
        started = started with { SessionId = confirmation.EpisodeId };

        var ended = _transitions.End(
            started,
            confirmation.EndedAtUtc,
            SessionEndReason.ProcessExited,
            confirmation.EndedAtUtc).Session
            ?? throw new InvalidOperationException("Session end transition did not produce a session.");

        await _sessionStore.UpsertAsync(ended, cancellationToken);
    }
}
