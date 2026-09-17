using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;

namespace PlayStead.Core.Identity;

public sealed class IdentityDecisionApplicationService : IIdentityDecisionApplicationService
{
    private readonly IIdentityDecisionContextGateway _contextGateway;
    private readonly IIdentityDecisionNotificationOrchestrator _orchestrator;

    public IdentityDecisionApplicationService(
        IIdentityDecisionContextGateway contextGateway,
        IIdentityDecisionNotificationOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(contextGateway);
        ArgumentNullException.ThrowIfNull(orchestrator);
        _contextGateway = contextGateway;
        _orchestrator = orchestrator;
    }

    public Task<IdentityDecisionContext?> GetContextAsync(GameId gameId, CancellationToken cancellationToken) =>
        _contextGateway.GetAsync(gameId, cancellationToken);

    public Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken) =>
        _orchestrator.ConfirmAsync(gameId, catalogContentId, cancellationToken);

    public Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken) =>
        _orchestrator.RejectAsync(gameId, catalogContentId, cancellationToken);

    public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken) =>
        _orchestrator.GetActiveConfirmedAsync(gameId, cancellationToken);

    public Task RevokeConfirmedAsync(GameId gameId, CancellationToken cancellationToken) =>
        _orchestrator.RevokeConfirmedAsync(gameId, cancellationToken);
}
