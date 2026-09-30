using PlayStead.Core.Persistence;

namespace PlayStead.Core.Library;

public sealed class ManualGameService
{
    private readonly IManualGameStore _store;

    public ManualGameService(IManualGameStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<GameInstallation> CreateAsync(
        ManualGameDefinition definition,
        CancellationToken cancellationToken) =>
        _store.CreateAsync(definition, cancellationToken);

    public Task<GameInstallation?> UpdateAsync(
        GameId gameId,
        ManualGameDefinition definition,
        CancellationToken cancellationToken) =>
        _store.UpdateAsync(gameId, definition, cancellationToken);

    public Task<bool> RemoveAsync(
        GameId gameId,
        CancellationToken cancellationToken) =>
        _store.RemoveAsync(gameId, cancellationToken);
}
