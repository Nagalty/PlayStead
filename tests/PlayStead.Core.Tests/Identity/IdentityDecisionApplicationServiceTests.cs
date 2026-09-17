using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;

namespace PlayStead.Core.Tests.Identity;

public sealed class IdentityDecisionApplicationServiceTests
{
    [Fact]
    public async Task GetContext_delegates_exact_game_and_token_and_propagates_result()
    {
        var game = GameId.New();
        var context = IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [new IdentityDecisionCandidate(CatalogContentId.New())]);
        using var cts = new CancellationTokenSource();
        var gateway = new FakeGateway(context);
        var service = new IdentityDecisionApplicationService(gateway, new FakeOrchestrator());

        Assert.Same(context, await service.GetContextAsync(game, cts.Token));
        Assert.Equal(game, gateway.GameId);
        Assert.Equal(cts.Token, gateway.Token);
    }

    [Fact]
    public async Task GetContext_propagates_null_and_cancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var gateway = new FakeGateway(null);
        var service = new IdentityDecisionApplicationService(gateway, new FakeOrchestrator());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetContextAsync(GameId.New(), cts.Token));
    }

    [Fact]
    public async Task Confirm_and_reject_delegate_exact_arguments()
    {
        var game = GameId.New(); var content = CatalogContentId.New(); using var cts = new CancellationTokenSource();
        var orchestrator = new FakeOrchestrator();
        var service = new IdentityDecisionApplicationService(new FakeGateway(null), orchestrator);
        await service.ConfirmAsync(game, content, cts.Token);
        await service.RejectAsync(game, content, cts.Token);
        Assert.Equal((game, content, cts.Token), orchestrator.ConfirmArgs);
        Assert.Equal((game, content, cts.Token), orchestrator.RejectArgs);
    }

    private sealed class FakeGateway(IdentityDecisionContext? result) : IIdentityDecisionContextGateway
    {
        public GameId GameId { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken)
        { GameId = gameId; Token = cancellationToken; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(result); }
    }

    private sealed class FakeOrchestrator : IIdentityDecisionNotificationOrchestrator
    {
        public (GameId, CatalogContentId, CancellationToken) ConfirmArgs { get; private set; }
        public (GameId, CatalogContentId, CancellationToken) RejectArgs { get; private set; }
        public Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken)
        { ConfirmArgs = (gameId, catalogContentId, cancellationToken); return Task.CompletedTask; }
        public Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken)
        { RejectArgs = (gameId, catalogContentId, cancellationToken); return Task.CompletedTask; }
    }
}
