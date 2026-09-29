using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderActivity;

public interface IEffectiveActivityService
{
    Task<EffectiveActivitySnapshot> GetAsync(
        GameId gameId,
        ProviderKind provider,
        CancellationToken cancellationToken);
}

