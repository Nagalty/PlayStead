using PlayStead.Core.Library;

namespace PlayStead.Core.Home;

public interface IHomeSuggestionSelectionStore
{
    Task<GameId?> GetLastSuggestionGameIdAsync(CancellationToken cancellationToken);
    Task SetLastSuggestionGameIdAsync(GameId? gameId, CancellationToken cancellationToken);
}
