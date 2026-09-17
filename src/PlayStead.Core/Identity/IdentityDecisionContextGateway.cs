using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed class IdentityDecisionContextGateway : IIdentityDecisionContextGateway
{
    private readonly IIdentityDecisionContextProvider _provider;
    public IdentityDecisionContextGateway(IIdentityDecisionContextProvider provider) { ArgumentNullException.ThrowIfNull(provider); _provider = provider; }
    public Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _provider.GetAsync(gameId, cancellationToken);
    }
}
