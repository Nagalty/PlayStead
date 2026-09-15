using System.ComponentModel;
using System.Runtime.CompilerServices;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Launching;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Library;

public sealed class LibraryViewModel :
    INotifyPropertyChanged
{
    private readonly ILibraryStore _libraryStore;
    private readonly IGameMediaResolver _gameMediaResolver;
    private readonly UiPreferencesStore? _uiPreferencesStore;
    private readonly ISteamReferenceRuntime? _steamReferenceRuntime;
    private readonly object _verifySteamGate = new();

    private readonly SemaphoreSlim _mediaGate =
        new(
            initialCount: 4,
            maxCount: 4);

    private readonly Dictionary<GameId, Task> _coverLoads =
        new();

    private readonly object _coverLoadsGate =
        new();

    private SessionMonitor? _sessionMonitor;

    private IReadOnlyList<LibraryItemViewModel> _items =
        Array.Empty<LibraryItemViewModel>();

    private IReadOnlyList<GameInstallation> _installations =
        Array.Empty<GameInstallation>();

    private bool _isSteamChecking;
    private string _steamVerificationError = string.Empty;
    private Task? _verifySteamTask;

    private LibraryViewMode _viewMode = LibraryViewMode.Grid;
    private string _sortKey = "Title";
    private string? _filterKey;
    private string _searchQuery = string.Empty;
    private GameId? _selectedGameId;
    private LibraryItemViewModel? _selectedItem;
    private double _verticalOffset;
    private int _gridColumnCount = 1;

    public LibraryViewModel(
        ILibraryStore libraryStore)
    {
        ArgumentNullException.ThrowIfNull(libraryStore);

        _libraryStore = libraryStore;
        _gameMediaResolver =
            NullGameMediaResolver.Instance;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime)
        : this(libraryStore)
    {
        ArgumentNullException.ThrowIfNull(
            steamReferenceRuntime);

        _steamReferenceRuntime =
            steamReferenceRuntime;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        SessionMonitor sessionMonitor)
        : this(libraryStore)
    {
        AttachSessionMonitor(
            sessionMonitor);
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime,
        SessionMonitor sessionMonitor)
        : this(
            libraryStore,
            steamReferenceRuntime)
    {
        AttachSessionMonitor(
            sessionMonitor);
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime,
        SessionMonitor sessionMonitor,
        IGameMediaResolver gameMediaResolver)
        : this(
            libraryStore,
            steamReferenceRuntime,
            sessionMonitor)
    {
        ArgumentNullException.ThrowIfNull(
            gameMediaResolver);

        _gameMediaResolver =
            gameMediaResolver;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime,
        SessionMonitor sessionMonitor,
        UiPreferencesStore uiPreferencesStore)
        : this(
            libraryStore,
            steamReferenceRuntime,
            sessionMonitor)
    {
        ArgumentNullException.ThrowIfNull(uiPreferencesStore);

        _uiPreferencesStore = uiPreferencesStore;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime,
        SessionMonitor sessionMonitor,
        UiPreferencesStore uiPreferencesStore,
        IGameMediaResolver gameMediaResolver)
        : this(
            libraryStore,
            steamReferenceRuntime,
            sessionMonitor,
            gameMediaResolver)
    {
        ArgumentNullException.ThrowIfNull(uiPreferencesStore);

        _uiPreferencesStore = uiPreferencesStore;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public IReadOnlyList<LibraryItemViewModel> Items
    {
        get => _items;
        private set
        {
            if (ReferenceEquals(
                    _items,
                    value))
            {
                return;
            }

            _items = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasItems));
            OnPropertyChanged(
                nameof(GridRows));
            OnPropertyChanged(
                nameof(VisibleItems));

            ReconcileSelectedItem();
        }
    }

    public bool HasItems =>
        Items.Count > 0;

    public bool IsSteamChecking
    {
        get => _isSteamChecking;
        private set
        {
            if (_isSteamChecking == value)
            {
                return;
            }

            _isSteamChecking = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(CanVerifySteam));
        }
    }

    public bool CanVerifySteam =>
        _steamReferenceRuntime is not null &&
        !IsSteamChecking;

    public string SteamVerificationError
    {
        get => _steamVerificationError;
        private set
        {
            if (string.Equals(
                    _steamVerificationError,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _steamVerificationError = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasSteamVerificationError));
        }
    }

    public bool HasSteamVerificationError =>
        !string.IsNullOrWhiteSpace(
            SteamVerificationError);

    public LibraryViewMode ViewMode => _viewMode;

    public int GridColumnCount => _gridColumnCount;

    public IReadOnlyList<LibraryGridRow> GridRows =>
        LibraryGridRowBuilder.Build(
            VisibleItems,
            GridColumnCount);

    public bool IsGridMode =>
        ViewMode == LibraryViewMode.Grid;

    public bool IsListMode =>
        ViewMode == LibraryViewMode.List;

    public void SetGridColumnCount(
        int columnCount)
    {
        if (columnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columnCount));
        }

        if (_gridColumnCount == columnCount)
        {
            return;
        }

        _gridColumnCount = columnCount;

        OnPropertyChanged(
            nameof(GridColumnCount));
        OnPropertyChanged(
            nameof(GridRows));
    }

    public string SortKey => _sortKey;

    public string? FilterKey => _filterKey;

    public string SearchQuery =>
        _searchQuery;

    public bool IsSearchActive =>
        !string.IsNullOrWhiteSpace(
            SearchQuery);

    public IReadOnlyList<LibraryItemViewModel> VisibleItems =>
        LibrarySearchService.Search(
            Items,
            SearchQuery);

    public GameId? SelectedGameId =>
        _selectedGameId;

    public LibraryItemViewModel? SelectedItem =>
        _selectedItem;

    public bool HasSelectedItem =>
        SelectedItem is not null;

    public double VerticalOffset =>
        _verticalOffset;

    public void SetViewMode(
        LibraryViewMode viewMode)
    {
        if (_viewMode == viewMode)
        {
            return;
        }

        _viewMode = viewMode;

        OnPropertyChanged(
            nameof(ViewMode));
        OnPropertyChanged(
            nameof(IsGridMode));
        OnPropertyChanged(
            nameof(IsListMode));
    }

    public void SetSortKey(
        string sortKey)
    {
        ArgumentNullException.ThrowIfNull(
            sortKey);

        if (sortKey is not ("Title" or "Provider"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortKey));
        }

        if (_sortKey == sortKey)
        {
            return;
        }

        _sortKey = sortKey;

        OnPropertyChanged(
            nameof(SortKey));
    }

    public void SetFilterKey(
        string? filterKey)
    {
        if (filterKey is not
            (null or "Steam" or "Epic" or "GOG" or "Manual"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(filterKey));
        }

        if (_filterKey == filterKey)
        {
            return;
        }

        _filterKey = filterKey;

        OnPropertyChanged(
            nameof(FilterKey));
    }

    public void SetSearchQuery(
        string searchQuery)
    {
        ArgumentNullException.ThrowIfNull(
            searchQuery);

        if (_searchQuery == searchQuery)
        {
            return;
        }

        _searchQuery =
            searchQuery;

        OnPropertyChanged(
            nameof(SearchQuery));
        OnPropertyChanged(
            nameof(IsSearchActive));
        OnPropertyChanged(
            nameof(VisibleItems));
        OnPropertyChanged(
            nameof(GridRows));
    }

    public void ClearSearch()
    {
        SetSearchQuery(
            string.Empty);
    }

    public async Task LoadUiPreferencesAsync(
        CancellationToken cancellationToken)
    {
        var store =
            _uiPreferencesStore
            ?? throw new InvalidOperationException(
                "A UiPreferencesStore is required to load Library preferences.");

        var preferences =
            await store.LoadAsync(
                cancellationToken);

        SetViewMode(
            preferences.LibraryViewMode);

        SetSortKey(
            preferences.LibrarySortKey);

        SetFilterKey(
            preferences.LibraryFilterKey);
    }

    public async Task SaveUiPreferencesAsync(
        CancellationToken cancellationToken)
    {
        var store =
            _uiPreferencesStore
            ?? throw new InvalidOperationException(
                "A UiPreferencesStore is required to save Library preferences.");

        var existing =
            await store.LoadAsync(
                cancellationToken);

        await store.SaveAsync(
            new UiPreferences(
                existing.ReduceMotion,
                ViewMode,
                SortKey,
                FilterKey),
            cancellationToken);
    }

    public void SelectGame(
        LibraryItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        SetSelectedGame(
            item.GameId);

        SetSelectedItem(
            item);
    }

    public void ClearSelection()
    {
        SetSelectedItem(
            null);

        SetSelectedGame(
            null);
    }

    public void SetSelectedGame(
        GameId? gameId)
    {
        if (_selectedGameId == gameId)
        {
            return;
        }

        _selectedGameId = gameId;

        OnPropertyChanged(
            nameof(SelectedGameId));

        ReconcileSelectedItem();
    }

    public void SetVerticalOffset(
        double verticalOffset)
    {
        if (_verticalOffset.Equals(
                verticalOffset))
        {
            return;
        }

        _verticalOffset =
            verticalOffset;

        OnPropertyChanged(
            nameof(VerticalOffset));
    }

    public LibraryUiState CaptureUiState()
    {
        return new LibraryUiState(
            ViewMode,
            SortKey,
            FilterKey,
            SelectedGameId,
            VerticalOffset);
    }

    public void RestoreUiState(
        LibraryUiState state)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        SetViewMode(
            state.ViewMode);

        if (_sortKey != state.SortKey)
        {
            _sortKey =
                state.SortKey;

            OnPropertyChanged(
                nameof(SortKey));
        }

        if (_filterKey != state.FilterKey)
        {
            _filterKey =
                state.FilterKey;

            OnPropertyChanged(
                nameof(FilterKey));
        }

        SetSelectedGame(
            state.SelectedGameId);

        SetVerticalOffset(
            state.VerticalOffset);
    }

    public GameInstallation? GetDefaultLaunchInstallation(
        GameId gameId)
    {
        return GameLaunchInstallationSelector.SelectDefault(
            gameId,
            _installations);
    }

    public IReadOnlyList<GameInstallation> GetLaunchInstallations(GameId gameId) =>
        _installations.Where(installation => installation.GameId == gameId).ToArray();

    public Task EnsureCoverAsync(
        LibraryItemViewModel item,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(
                cancellationToken);
        }

        if (item.HasCover ||
            item.Provider != ProviderKind.Steam)
        {
            return Task.CompletedTask;
        }

        TaskCompletionSource completion;

        lock (_coverLoadsGate)
        {
            if (_coverLoads.TryGetValue(
                    item.GameId,
                    out var existingLoad))
            {
                return existingLoad;
            }

            completion =
                new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            _coverLoads[item.GameId] =
                completion.Task;
        }

        _ = CompleteCoverLoadAsync(
            item,
            cancellationToken,
            completion);

        return completion.Task;
    }

    private async Task CompleteCoverLoadAsync(
        LibraryItemViewModel item,
        CancellationToken cancellationToken,
        TaskCompletionSource completion)
    {
        try
        {
            await EnsureCoverCoreAsync(
                item,
                cancellationToken);

            completion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            if (exception.CancellationToken.CanBeCanceled)
            {
                completion.TrySetCanceled(
                    exception.CancellationToken);
            }
            else
            {
                completion.TrySetCanceled();
            }
        }
        catch (Exception exception)
        {
            completion.TrySetException(
                exception);
        }
        finally
        {
            lock (_coverLoadsGate)
            {
                if (_coverLoads.TryGetValue(
                        item.GameId,
                        out var currentLoad) &&
                    ReferenceEquals(
                        currentLoad,
                        completion.Task))
                {
                    _coverLoads.Remove(
                        item.GameId);
                }
            }
        }
    }

    private async Task EnsureCoverCoreAsync(
        LibraryItemViewModel item,
        CancellationToken cancellationToken)
    {
        if (item.HasCover)
        {
            return;
        }

        var installation =
            GameLaunchInstallationSelector.SelectDefault(
                item.GameId,
                _installations.Where(candidate =>
                    candidate.Provider == item.Provider &&
                    string.Equals(
                        candidate.InstallPath,
                        item.InstallPath,
                        StringComparison.OrdinalIgnoreCase)));

        if (installation is null ||
            string.IsNullOrWhiteSpace(
                installation.ExternalId))
        {
            return;
        }

        var identity =
            new GameMediaIdentity(
                installation.Provider,
                installation.ExternalId,
                item.Title);

        await _mediaGate.WaitAsync(
            cancellationToken);

        try
        {
            var path =
                await _gameMediaResolver.ResolveAndCacheAsync(
                    identity,
                    GameMediaAssetType.Cover,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (path is not null)
            {
                item.SetCoverPath(
                    path);
            }
        }
        finally
        {
            _mediaGate.Release();
        }
    }
    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        var snapshot =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);

        _installations =
            snapshot.Installations.ToArray();

        Items =
            BuildItems(
                snapshot,
                checking: false);
    }

    public Task VerifySteamAsync(
        CancellationToken cancellationToken)
    {
        if (_steamReferenceRuntime is null)
        {
            return Task.CompletedTask;
        }

        lock (_verifySteamGate)
        {
            if (_verifySteamTask is
                {
                    IsCompleted: false
                })
            {
                return _verifySteamTask;
            }

            _verifySteamTask =
                VerifySteamCoreAsync(
                    cancellationToken);

            return _verifySteamTask;
        }
    }

    private async Task VerifySteamCoreAsync(
        CancellationToken cancellationToken)
    {
        var runtime =
            _steamReferenceRuntime
            ?? throw new InvalidOperationException(
                "Steam reference runtime is unavailable.");

        SteamVerificationError =
            string.Empty;

        IsSteamChecking = true;

        Items =
            Items
                .Select(
                    item =>
                        item.Provider ==
                            ProviderKind.Steam
                            ? item with
                            {
                                SteamState =
                                    SteamUpdateState.Checking
                            }
                            : item)
                .ToArray();

        try
        {
            await runtime.RefreshAllAsync(
                cancellationToken);

            await RefreshAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            SteamVerificationError =
                "La vérification Steam a échoué. Réessaie dans quelques instants.";
        }
        finally
        {
            IsSteamChecking = false;
        }
    }

    private IReadOnlyList<LibraryItemViewModel>
        BuildItems(
            LibrarySnapshot snapshot,
            bool checking)
    {
        var presentInstallations =
            snapshot.Installations
                .Where(
                    installation =>
                        installation.IsPresent)
                .GroupBy(
                    installation =>
                        installation.GameId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .OrderByDescending(
                                installation =>
                                    installation.IsPreferred)
                            .ThenBy(
                                installation =>
                                    installation.Provider)
                            .ThenBy(
                                installation =>
                                    installation.InstallPath,
                                StringComparer.OrdinalIgnoreCase)
                            .First());

        var steamStates =
            (_steamReferenceRuntime?.Current.Entries
                ?? Array.Empty<SteamReferenceEntry>())
            .GroupBy(
                entry =>
                    entry.AppId,
                StringComparer.Ordinal)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.First().Evaluation.State,
                StringComparer.Ordinal);

        var activeGameIds =
            ActiveGameIds(
                _sessionMonitor?.LatestSnapshot);

        return snapshot.Games
            .Where(
                game =>
                    presentInstallations.ContainsKey(
                        game.Id))
            .Select(
                game =>
                {
                    var installation =
                        presentInstallations[
                            game.Id];

                    SteamUpdateState? steamState =
                        null;

                    if (installation.Provider ==
                        ProviderKind.Steam)
                    {
                        if (checking)
                        {
                            steamState =
                                SteamUpdateState.Checking;
                        }
                        else if (!steamStates.TryGetValue(
                                     installation.ExternalId,
                                     out var foundState))
                        {
                            steamState =
                                SteamUpdateState.Unknown;
                        }
                        else
                        {
                            steamState =
                                foundState;
                        }
                    }

                    var item =
                        new LibraryItemViewModel(
                            game.Id,
                            game.Title,
                            installation.Provider,
                            ProviderLabel(
                                installation.Provider),
                            installation.InstallPath,
                            installation.InstalledSizeBytes,
                            steamState,
                            activeGameIds.Contains(
                                game.Id));

                    ApplyCachedCover(
                        item,
                        installation);

                    return item;
                })
            .OrderBy(
                item =>
                    item.Title,
                StringComparer
                    .CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void ApplyCachedCover(
        LibraryItemViewModel item,
        GameInstallation installation)
    {
        if (installation.Provider != ProviderKind.Steam ||
            string.IsNullOrWhiteSpace(
                installation.ExternalId))
        {
            return;
        }

        var identity =
            new GameMediaIdentity(
                installation.Provider,
                installation.ExternalId,
                item.Title);

        item.SetCoverPath(
            _gameMediaResolver.TryGetCachedPath(
                identity,
                GameMediaAssetType.Cover));
    }

    private void AttachSessionMonitor(
        SessionMonitor sessionMonitor)
    {
        ArgumentNullException.ThrowIfNull(
            sessionMonitor);

        _sessionMonitor =
            sessionMonitor;

        _sessionMonitor.SnapshotUpdated +=
            SessionMonitor_OnSnapshotUpdated;
    }

    private void SessionMonitor_OnSnapshotUpdated(
        SessionRuntimeSnapshot snapshot)
    {
        var activeGameIds =
            ActiveGameIds(
                snapshot);

        Items =
            Items
                .Select(
                    item =>
                        item with
                        {
                            IsSessionActive =
                                activeGameIds.Contains(
                                    item.GameId)
                        })
                .ToArray();
    }

    private void ReconcileSelectedItem()
    {
        var selectedItem =
            _selectedGameId is GameId selectedGameId
                ? Items.FirstOrDefault(
                    item =>
                        item.GameId == selectedGameId)
                : null;

        SetSelectedItem(
            selectedItem);
    }

    private void SetSelectedItem(
        LibraryItemViewModel? item)
    {
        if (ReferenceEquals(
                _selectedItem,
                item))
        {
            return;
        }

        _selectedItem =
            item;

        OnPropertyChanged(
            nameof(SelectedItem));

        OnPropertyChanged(
            nameof(HasSelectedItem));
    }

    private static HashSet<GameId> ActiveGameIds(
        SessionRuntimeSnapshot? snapshot) =>
        snapshot?.ActiveSessions
            .Select(
                session =>
                    new GameId(
                        session.GameId))
            .ToHashSet()
        ?? [];

    private static string ProviderLabel(
        ProviderKind provider) =>
        provider switch
        {
            ProviderKind.Steam =>
                "Steam",

            ProviderKind.Epic =>
                "Epic",

            ProviderKind.Gog =>
                "GOG",

            ProviderKind.Manual =>
                "Manual",

            _ =>
                provider.ToString()
        };

    private sealed class NullGameMediaResolver :
        IGameMediaResolver
    {
        public static NullGameMediaResolver Instance { get; } =
            new();

        private NullGameMediaResolver()
        {
        }

        public string? TryGetCachedPath(
            GameMediaIdentity identity,
            GameMediaAssetType assetType) =>
            null;

        public Task<string?> ResolveAndCacheAsync(
            GameMediaIdentity identity,
            GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<string?>(
                null);
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
}
