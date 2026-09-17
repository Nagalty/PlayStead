using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed record IdentityDecisionContext
{
    private IdentityDecisionContext(GameId gameId, IdentityResolutionState state, IReadOnlyList<IdentityDecisionCandidate> candidates, DateTimeOffset observedAtUtc)
    { GameId = gameId; State = state; Candidates = candidates; ObservedAtUtc = observedAtUtc; }
    public GameId GameId { get; }
    public IdentityResolutionState State { get; }
    public IReadOnlyList<IdentityDecisionCandidate> Candidates { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public static IdentityDecisionContext Create(GameId gameId, IdentityResolutionState state, IReadOnlyList<IdentityDecisionCandidate> candidates, DateTimeOffset? observedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var count = candidates.Count;
        if (state == IdentityResolutionState.MatchProbable && count != 1 || state == IdentityResolutionState.Ambiguous && count < 2 || state == IdentityResolutionState.New && count != 0 || state == IdentityResolutionState.MatchConfirmed)
            throw new ArgumentException("The identity decision state and candidate count are inconsistent.", nameof(candidates));
        return new IdentityDecisionContext(gameId, state, candidates.ToArray(), observedAtUtc ?? DateTimeOffset.UtcNow);
    }
}
