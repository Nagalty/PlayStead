using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Identity;

public sealed class IdentityDecisionContextGatewayTests
{
    [Fact]
    public async Task Gateway_delegates_null_and_context_without_mutation()
    {
        var game = GameId.New(); var candidate = new IdentityDecisionCandidate(CatalogContentId.New());
        var provider = new FakeProvider(IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [candidate]));
        var gateway = new IdentityDecisionContextGateway(provider);
        var context = await gateway.GetAsync(game, CancellationToken.None);
        Assert.Equal(candidate, Assert.Single(context!.Candidates)); Assert.Equal(IdentityResolutionState.MatchProbable, context.State);
        Assert.Null(await new IdentityDecisionContextGateway(new FakeProvider(null)).GetAsync(game, CancellationToken.None));
    }

    [Fact]
    public async Task Empty_source_returns_no_candidates()
    {
        var result = await new EmptyIdentityDecisionCandidateSource().GetCandidatesAsync(GameId.New(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using var c = new CancellationTokenSource(); c.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new IdentityDecisionContextGateway(new FakeProvider(null)).GetAsync(GameId.New(), c.Token));
    }

    private sealed class FakeProvider(IdentityDecisionContext? context) : IIdentityDecisionContextProvider
    { public Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(context); } }
}
