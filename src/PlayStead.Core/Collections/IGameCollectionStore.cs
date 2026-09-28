using PlayStead.Core.Library;

namespace PlayStead.Core.Collections;

public interface IGameCollectionStore
{
    Task<IReadOnlyList<GameCollection>> GetCollectionsAsync(CancellationToken cancellationToken);
    Task<GameCollection> CreateCollectionAsync(string name, CancellationToken cancellationToken);
    Task<GameCollection> RenameCollectionAsync(Guid collectionId, string name, CancellationToken cancellationToken);
    Task<bool> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameCollectionMembership>> GetMembershipsAsync(CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> GetMembershipsAsync(GameId gameId, CancellationToken cancellationToken);
    Task SetMembershipAsync(Guid collectionId, GameId gameId, bool enabled, CancellationToken cancellationToken);
}
