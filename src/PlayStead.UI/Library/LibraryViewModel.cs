using System.ComponentModel;
using System.Runtime.CompilerServices;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Library;

public sealed class LibraryViewModel :
    INotifyPropertyChanged
{
    private readonly ILibraryStore _libraryStore;
    private readonly UiPreferencesStore? _uiPreferencesStore;
    private readonly ISteamReferenceRuntime? _steamReferenceRuntime;
    private readonly object _verifySteamGate = new();

    private SessionMonitor? _sessionMonitor;

    private IReadOnlyList<LibraryItemViewModel> _items =
        Array.Empty<LibraryItemViewModel>();

    private bool _isSteamChecking;
    private Task? _verifySteamTask;

    private LibraryViewMode _viewMode = LibraryViewMode.Grid;
    private string _sortKey = "Title";
    private string? _filterKey;
    private GameId? _selectedGameId;
    private LibraryItemViewModel? _selectedItem;
    private double _verticalOffset;
    private int _gridColumnCount = 1;

    public LibraryViewModel(
        ILibraryStore libraryStore)
    {
        ArgumentNullException.ThrowIfNull(libraryStore);

        _libraryStore = libraryStore;
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
        UiPreferencesStore uiPreferencesStore)
        : this(
            libraryStore,
            steamReferenceRuntime,
            sessionMonitor)
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

    public LibraryViewMode ViewMode => _viewMode;

    public int GridColumnCount => _gridColumnCount;

    public IReadOnlyList<LibraryGridRow> GridRows =>
        LibraryGridRowBuilder.Build(
            Items,
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

    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        var snapshot =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);

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

                    return new LibraryItemViewModel(
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
                })
            .OrderBy(
                item =>
                    item.Title,
                StringComparer
                    .CurrentCultureIgnoreCase)
            .ToArray();
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

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
}
