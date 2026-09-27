using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderGameMetadata;

public interface IProviderGameMetadataStore
{
    Task<IReadOnlyList<ProviderGameMetadata>> GetAllAsync(CancellationToken cancellationToken);
    Task<ProviderGameMetadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken);
    Task UpsertAsync(ProviderGameMetadata metadata, CancellationToken cancellationToken);
}
