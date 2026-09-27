using PlayStead.Core.Library;

namespace PlayStead.Core.Shortlist;

public enum GamesDuMomentAddResult
{
    Added,
    AlreadyMember,
    Full
}

public interface IGamesDuMomentService
{
    Task<IReadOnlyList<GamesDuMomentEntry>> GetAsync(CancellationToken cancellationToken);
    Task<GamesDuMomentAddResult> AddAsync(GameId gameId, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(GameId gameId, CancellationToken cancellationToken);
    Task ReorderAsync(IReadOnlyList<GameId> orderedGameIds, CancellationToken cancellationToken);
}
