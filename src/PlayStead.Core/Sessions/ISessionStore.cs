namespace PlayStead.Core.Sessions;

public interface ISessionStore
{
    Task UpsertAsync(
        GameSession session,
        CancellationToken cancellationToken);

    Task<GameSession?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameSession>> GetActiveAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameSession>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameSession>> GetByGameAsync(
        Guid gameId,
        CancellationToken cancellationToken);
}
