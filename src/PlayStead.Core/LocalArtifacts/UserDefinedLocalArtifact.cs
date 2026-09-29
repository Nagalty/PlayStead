using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed record UserDefinedLocalArtifact(
    Guid Id,
    GameId GameId,
    GameLocalArtifactKind Kind,
    string Path,
    string? DisplayName,
    DateTimeOffset CreatedAtUtc);

public interface IUserDefinedLocalArtifactStore
{
    Task<IReadOnlyList<UserDefinedLocalArtifact>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken);
    Task AddAsync(UserDefinedLocalArtifact artifact, CancellationToken cancellationToken);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken);
}
