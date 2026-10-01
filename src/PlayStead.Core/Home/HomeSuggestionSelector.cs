using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Core.Sessions;
using Metadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Home;

public sealed class HomeSuggestionSelector
{
    private readonly IHomeSuggestionSelectionStore _selectionStore;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private bool _initialized;
    private GameId? _selectedGameId;
    private DayOfWeek _selectionDay;

    public HomeSuggestionSelector(
        IHomeSuggestionSelectionStore selectionStore,
        TimeProvider timeProvider,
        Random? random = null)
    {
        _selectionStore = selectionStore;
        _timeProvider = timeProvider;
        _random = random ?? Random.Shared;
    }

    public async Task<HomeSuggestion?> SelectAsync(
        LibrarySnapshot library,
        IReadOnlyCollection<Metadata> metadata,
        IReadOnlyCollection<GameId> activeGameIds,
        CancellationToken cancellationToken,
        IReadOnlyCollection<ProviderInstallUpdateState>? installUpdates = null)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(activeGameIds);
        var day = _timeProvider.GetLocalNow().DayOfWeek;
        var candidates = Eligible(library, metadata, activeGameIds, day, installUpdates ?? []);

        if (_initialized)
        {
            if (_selectedGameId is not GameId current)
                return null;
            if (candidates.All(x => x.GameId != current))
            {
                _selectedGameId = null;
                await _selectionStore.SetLastSuggestionGameIdAsync(null, cancellationToken);
                return null;
            }
            return candidates.First(x => x.GameId == current);
        }

        _initialized = true;
        _selectionDay = day;
        var previous = await _selectionStore.GetLastSuggestionGameIdAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            _selectedGameId = null;
            return null;
        }

        var drawPool = candidates.Count > 1 && previous is GameId previousId
            ? candidates.Where(x => x.GameId != previousId).ToArray()
            : candidates;
        var selected = drawPool[_random.Next(drawPool.Count)];
        _selectedGameId = selected.GameId;
        await _selectionStore.SetLastSuggestionGameIdAsync(_selectedGameId, cancellationToken);
        return selected;
    }

    private static IReadOnlyList<HomeSuggestion> Eligible(
        LibrarySnapshot library,
        IReadOnlyCollection<Metadata> metadata,
        IReadOnlyCollection<GameId> activeGameIds,
        DayOfWeek day,
        IReadOnlyCollection<ProviderInstallUpdateState> installUpdates)
    {
        var games = library.Games.ToDictionary(x => x.Id);
        var metadataByGame = metadata
            .GroupBy(x => x.GameId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var weekend = day is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;
        return library.Installations
            .Where(x => x.IsPresent && !string.IsNullOrWhiteSpace(x.ExternalId) && !string.IsNullOrWhiteSpace(x.InstallPath))
            .Where(x => games.ContainsKey(x.GameId) && !activeGameIds.Contains(x.GameId))
            .Where(x => !weekend || metadataByGame.TryGetValue(x.GameId, out var values) && values.Any(value => value.SupportsCoop is true))
            .GroupBy(x => x.GameId)
            .Select(group => group.First())
            .Select(installation =>
            {
                var game = games[installation.GameId];
                GameMediaIdentity? media = null;
                try { media = GameMediaIdentityFactory.Create(installation.GameId, installation, game.Title); }
                catch (ArgumentException) { }
                var gameMetadata = metadataByGame.TryGetValue(installation.GameId, out var values)
                    ? values.FirstOrDefault(value => value.Provider == installation.Provider && value.ProviderGameId == installation.ExternalId) ?? values.FirstOrDefault()
                    : null;
                var updateState = installUpdates.FirstOrDefault(state =>
                    state.GameId == installation.GameId &&
                    state.Provider == installation.Provider &&
                    state.Status is ProviderInstallUpdateStatus.UpdateAvailable
                        or ProviderInstallUpdateStatus.Downloading
                        or ProviderInstallUpdateStatus.Staging);
                return new HomeSuggestion(installation.GameId, game.Title, installation.Provider, installation.ExternalId, media, day, gameMetadata, updateState);
            })
            .OrderBy(x => x.GameId.Value)
            .ToArray();
    }
}
