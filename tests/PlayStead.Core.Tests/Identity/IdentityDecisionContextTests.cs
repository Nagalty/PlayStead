using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Identity;

public sealed class IdentityDecisionContextTests
{
    [Fact]
    public void Context_validates_candidate_cardinality()
    {
        var game = GameId.New();
        var one = new IdentityDecisionCandidate(CatalogContentId.New());
        var many = new[] { one, new IdentityDecisionCandidate(CatalogContentId.New()) };
        Assert.NotNull(IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [one]));
        Assert.NotNull(IdentityDecisionContext.Create(game, IdentityResolutionState.Ambiguous, many));
        Assert.NotNull(IdentityDecisionContext.Create(game, IdentityResolutionState.New, []));
        Assert.Throws<ArgumentException>(() => IdentityDecisionContext.Create(game, IdentityResolutionState.Ambiguous, [one]));
    }

    [Fact]
    public async Task Provider_filters_rejections_and_recomputes_state()
    {
        var game = GameId.New(); var a = new IdentityDecisionCandidate(CatalogContentId.New()); var b = new IdentityDecisionCandidate(CatalogContentId.New());
        var source = new Source([a, b]); var store = new Decisions([a.CatalogContentId]); var provider = new IdentityDecisionContextProvider(source, store);
        var context = await provider.GetAsync(game, CancellationToken.None);
        var candidate = Assert.Single(context!.Candidates); Assert.Equal(b.CatalogContentId, candidate.CatalogContentId); Assert.Equal(IdentityResolutionState.MatchProbable, context.State);
    }

    [Fact]
    public async Task Confirmed_game_returns_no_context_and_cancellation_propagates()
    {
        var game = GameId.New(); var confirmed = new Decisions([], true); var provider = new IdentityDecisionContextProvider(new Source([]), confirmed);
        Assert.Null(await provider.GetAsync(game, CancellationToken.None));
        using var c = new CancellationTokenSource(); c.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetAsync(game, c.Token));
    }

    private sealed class Source(IReadOnlyList<IdentityDecisionCandidate> candidates) : IIdentityDecisionCandidateSource { public Task<IReadOnlyList<IdentityDecisionCandidate>> GetCandidatesAsync(GameId gameId, CancellationToken token) => Task.FromResult(candidates); }
    private sealed class Decisions(IReadOnlyList<CatalogContentId> rejected, bool confirmed = false) : IIdentityDecisionStore
    {
        public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken token) => Task.FromResult<GameIdentityDecision?>(confirmed ? new(IdentityDecisionId.New(), gameId, CatalogContentId.New(), IdentityDecisionType.UserConfirmed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null) : null);
        public Task<IReadOnlyList<GameIdentityDecision>> ListActiveRejectedAsync(GameId gameId, CancellationToken token) => Task.FromResult<IReadOnlyList<GameIdentityDecision>>(rejected.Select(id => new GameIdentityDecision(IdentityDecisionId.New(), gameId, id, IdentityDecisionType.UserRejected, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)).ToArray());
        public Task<IReadOnlyList<GameIdentityDecision>> ListActiveAsync(GameId gameId, CancellationToken token) => ListActiveRejectedAsync(gameId, token);
        public Task InsertAsync(GameIdentityDecision decision, CancellationToken token) => Task.CompletedTask;
        public Task RevokeAsync(IdentityDecisionId id, DateTimeOffset at, CancellationToken token) => Task.CompletedTask;
    }
}
