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

    [Fact]
    public async Task Active_confirmation_and_revocation_delegate_exact_game_and_token()
    {
        var game = GameId.New();
        var decision = new GameIdentityDecision(IdentityDecisionId.New(), game, CatalogContentId.New(), IdentityDecisionType.UserConfirmed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        using var cts = new CancellationTokenSource();
        var orchestrator = new FakeOrchestrator { ActiveConfirmed = decision };
        var service = new IdentityDecisionApplicationService(new FakeGateway(null), orchestrator);

        Assert.Same(decision, await service.GetActiveConfirmedAsync(game, cts.Token));
        await service.RevokeConfirmedAsync(game, cts.Token);

        Assert.Equal((game, cts.Token), orchestrator.ActiveConfirmedArgs);
        Assert.Equal((game, cts.Token), orchestrator.RevokeArgs);
    }

    [Fact]
    public async Task Active_confirmation_propagates_null_and_cancellation()
    {
        var orchestrator = new FakeOrchestrator(); var service = new IdentityDecisionApplicationService(new FakeGateway(null), orchestrator);
        Assert.Null(await service.GetActiveConfirmedAsync(GameId.New(), CancellationToken.None));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RevokeConfirmedAsync(GameId.New(), cts.Token));
    }

    [Fact]
    public async Task Active_confirmation_propagates_cancellation()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var service = new IdentityDecisionApplicationService(new FakeGateway(null), new FakeOrchestrator());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetActiveConfirmedAsync(GameId.New(), cts.Token));
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
        public (GameId, CancellationToken) ActiveConfirmedArgs { get; private set; }
        public (GameId, CancellationToken) RevokeArgs { get; private set; }
        public GameIdentityDecision? ActiveConfirmed { get; set; }
        public Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken)
        { ConfirmArgs = (gameId, catalogContentId, cancellationToken); return Task.CompletedTask; }
        public Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken)
        { RejectArgs = (gameId, catalogContentId, cancellationToken); return Task.CompletedTask; }
        public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken)
        { ActiveConfirmedArgs = (gameId, cancellationToken); cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(ActiveConfirmed); }
        public Task RevokeConfirmedAsync(GameId gameId, CancellationToken cancellationToken)
        { RevokeArgs = (gameId, cancellationToken); cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    }
}
