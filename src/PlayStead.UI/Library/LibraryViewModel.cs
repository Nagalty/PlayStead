using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Launching;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Steam;
using PlayStead.Core.Shortlist;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.Notifications;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.Collections;

namespace PlayStead.UI.Library;

public sealed class LibraryViewModel :
    INotifyPropertyChanged,
    IDisposable
{
    private readonly ILibraryStore _libraryStore;
    private readonly IGameMediaResolver _gameMediaResolver;
    public ICanonicalCatalogStore? CanonicalCatalogStore { get; }
    public IGamesDuMomentService? GamesDuMomentService { get; }
    private readonly UiPreferencesStore? _uiPreferencesStore;
    private readonly ISteamReferenceRuntime? _steamReferenceRuntime;
    private readonly IProviderActivityMetadataStore? _providerActivityStore;
    private readonly ISessionStore? _sessionStore;
    private IGameCollectionStore? _collectionStore;
    private readonly object _verifySteamGate = new();
    private readonly Dispatcher? _uiDispatcher =
        Application.Current?.Dispatcher
        ?? Dispatcher.FromThread(Thread.CurrentThread);

    private readonly SemaphoreSlim _mediaGate =
        new(
            initialCount: 4,
            maxCount: 4);

    private readonly Dictionary<GameId, Task> _coverLoads =
        new();

    private readonly object _coverLoadsGate =
        new();

    private readonly Dictionary<GameId, Task> _logoLoads =
        new();

    private readonly object _logoLoadsGate =
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
    private LibraryQuickFilter _quickFilter = LibraryQuickFilter.Installed;
    private IAttentionService? _attentionService;
    private HashSet<GameId> _attentionGameIds = [];
    private IReadOnlyDictionary<GameId, LibraryActivityProjection> _activityByGame =
        new Dictionary<GameId, LibraryActivityProjection>();
    private IReadOnlyList<LibraryFilterOption> _providerFilterOptions = [];
    private IReadOnlyList<LibraryFilterOption> _driveFilterOptions = [];
    private IReadOnlyList<LibraryCollectionOption> _collectionOptions = [];
    private IReadOnlySet<GameCollectionMembership> _collectionMemberships = new HashSet<GameCollectionMembership>();
    private bool _updatingCollectionOption;
    private bool _isAdvancedFiltersOpen;
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
        IGameMediaResolver gameMediaResolver,
        ICanonicalCatalogStore? canonicalCatalogStore = null,
        IGamesDuMomentService? gamesDuMomentService = null,
        IProviderGameMetadataStore? providerGameMetadataStore = null)
        : this(
            libraryStore,
            steamReferenceRuntime,
            sessionMonitor)
    {
        ArgumentNullException.ThrowIfNull(
            gameMediaResolver);

        _gameMediaResolver =
            gameMediaResolver;

        CanonicalCatalogStore = canonicalCatalogStore;
        GamesDuMomentService = gamesDuMomentService;
        ProviderGameMetadataStore = providerGameMetadataStore;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime,
        SessionMonitor sessionMonitor,
        UiPreferencesStore uiPreferencesStore,
        IGameMediaResolver gameMediaResolver,
        ICanonicalCatalogStore? canonicalCatalogStore,
        IGamesDuMomentService? gamesDuMomentService,
        IProviderGameMetadataStore? providerGameMetadataStore,
        IProviderActivityMetadataStore? providerActivityStore,
        ISessionStore? sessionStore)
        : this(libraryStore, steamReferenceRuntime, sessionMonitor, uiPreferencesStore,
            gameMediaResolver, canonicalCatalogStore, gamesDuMomentService, providerGameMetadataStore)
    {
        _providerActivityStore = providerActivityStore;
        _sessionStore = sessionStore;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        IProviderActivityMetadataStore providerActivityStore,
        ISessionStore sessionStore)
        : this(libraryStore)
    {
        _providerActivityStore = providerActivityStore;
        _sessionStore = sessionStore;
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
        IGameMediaResolver gameMediaResolver,
        ICanonicalCatalogStore? canonicalCatalogStore = null,
        IGamesDuMomentService? gamesDuMomentService = null,
        IProviderGameMetadataStore? providerGameMetadataStore = null)
        : this(
            libraryStore,
            steamReferenceRuntime,
            sessionMonitor,
            gameMediaResolver,
            canonicalCatalogStore,
            gamesDuMomentService,
            providerGameMetadataStore)
    {
        ArgumentNullException.ThrowIfNull(uiPreferencesStore);

        _uiPreferencesStore = uiPreferencesStore;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public IProviderGameMetadataStore? ProviderGameMetadataStore { get; }

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

            RefreshAdvancedFilterOptions();

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

    public LibraryQuickFilter QuickFilter => _quickFilter;

    public string QuickFilterKey => _quickFilter.ToString();

    public bool IsInstalledQuickFilter => _quickFilter == LibraryQuickFilter.Installed;

    public bool IsAttentionQuickFilter => _quickFilter == LibraryQuickFilter.Attention;

    public IReadOnlyList<LibraryFilterOption> ProviderFilterOptions => _providerFilterOptions;

    public IReadOnlyList<LibraryFilterOption> DriveFilterOptions => _driveFilterOptions;

    public IReadOnlyList<LibraryCollectionOption> CollectionOptions => _collectionOptions;

    public IReadOnlyDictionary<GameId, IReadOnlyList<string>> CollectionNamesByGame =>
        _collectionMemberships
            .Join(_collectionOptions,
                membership => membership.CollectionId,
                option => option.Id,
                (membership, option) => new { membership.GameId, option.Name })
            .GroupBy(value => value.GameId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(value => value.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    public bool HasCollections => _collectionOptions.Count > 0;

    public bool HasDriveFilterOptions => _driveFilterOptions.Count > 0;

    public bool IsAdvancedFiltersOpen
    {
        get => _isAdvancedFiltersOpen;
        private set
        {
            if (_isAdvancedFiltersOpen == value)
                return;
            _isAdvancedFiltersOpen = value;
            OnPropertyChanged();
        }
    }

    public int ActiveAdvancedFilterCategoryCount =>
        (_providerFilterOptions.Any(option => option.IsSelected) ? 1 : 0) +
        (_driveFilterOptions.Any(option => option.IsSelected) ? 1 : 0) +
        (_collectionOptions.Any(option => option.IsFilterSelected) ? 1 : 0);

    public string AdvancedFilterButtonLabel =>
        ActiveAdvancedFilterCategoryCount == 0
            ? "Filtres"
            : $"Filtres ({ActiveAdvancedFilterCategoryCount})";

    public bool HasAdvancedFilters => ActiveAdvancedFilterCategoryCount > 0;

    public bool IsAdvancedFilterEmpty =>
        HasAdvancedFilters &&
        Items.Count > 0 &&
        string.IsNullOrWhiteSpace(SearchQuery) &&
        VisibleItems.Count == 0;

    public string? FilterKey => _filterKey;

    public string SearchQuery =>
        _searchQuery;

    public bool IsSearchActive =>
        !string.IsNullOrWhiteSpace(
            SearchQuery);

    public IReadOnlyList<LibraryItemViewModel> VisibleItems =>
        ApplyProjection();

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

        if (sortKey is not ("Recent" or "Playtime" or "Title" or "Provider" or "Size"))
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
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(GridRows));
        ReconcileSelectedItem();
    }

    public void SetQuickFilter(LibraryQuickFilter quickFilter)
    {
        if (_quickFilter == quickFilter)
            return;

        _quickFilter = quickFilter;
        OnPropertyChanged(nameof(QuickFilter));
        OnPropertyChanged(nameof(QuickFilterKey));
        OnPropertyChanged(nameof(IsInstalledQuickFilter));
        OnPropertyChanged(nameof(IsAttentionQuickFilter));
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(GridRows));
        ReconcileSelectedItem();
    }

    public void ToggleAdvancedFilters() => IsAdvancedFiltersOpen = !IsAdvancedFiltersOpen;

    public void CloseAdvancedFilters() => IsAdvancedFiltersOpen = false;

    public void ResetAdvancedFilters()
    {
        foreach (var option in _providerFilterOptions.Concat(_driveFilterOptions))
            option.IsSelected = false;
        foreach (var option in _collectionOptions)
            option.IsFilterSelected = false;
        NotifyAdvancedFilterProjectionChanged();
    }

    public void AttachCollectionStore(IGameCollectionStore collectionStore)
    {
        ArgumentNullException.ThrowIfNull(collectionStore);
        _collectionStore = collectionStore;
    }

    public async Task RefreshCollectionsAsync(CancellationToken cancellationToken)
    {
        if (_collectionStore is null)
            return;

        var collections = await _collectionStore.GetCollectionsAsync(cancellationToken);
        _collectionMemberships = (await _collectionStore.GetMembershipsAsync(cancellationToken)).ToHashSet();
        var selectedFilters = _collectionOptions.Where(option => option.IsFilterSelected).Select(option => option.Id).ToHashSet();
        var selectedGame = SelectedGameId;
        var previous = _collectionOptions.ToDictionary(option => option.Id);
        _collectionOptions = collections.Select(collection =>
        {
            var option = previous.GetValueOrDefault(collection.Id) ?? new LibraryCollectionOption(collection);
            if (!previous.ContainsKey(collection.Id))
            {
                option.FilterChanged += CollectionFilterChanged;
                option.MembershipChanged += CollectionMembershipChanged;
            }
            option.Update(collection, selectedFilters.Contains(collection.Id), selectedGame is not null && _collectionMemberships.Contains(new GameCollectionMembership(collection.Id, selectedGame.Value)));
            return option;
        }).ToArray();
        OnPropertyChanged(nameof(CollectionOptions));
        OnPropertyChanged(nameof(CollectionNamesByGame));
        OnPropertyChanged(nameof(HasCollections));
        NotifyAdvancedFilterProjectionChanged();
    }

    public async Task<GameCollection> CreateCollectionAsync(string name, CancellationToken cancellationToken)
    {
        if (_collectionStore is null) throw new InvalidOperationException("A collection store is required.");
        var result = await _collectionStore.CreateCollectionAsync(name, cancellationToken);
        await RefreshCollectionsAsync(cancellationToken);
        return result;
    }

    public async Task<GameCollection> RenameCollectionAsync(Guid collectionId, string name, CancellationToken cancellationToken)
    {
        if (_collectionStore is null) throw new InvalidOperationException("A collection store is required.");
        var result = await _collectionStore.RenameCollectionAsync(collectionId, name, cancellationToken);
        await RefreshCollectionsAsync(cancellationToken);
        return result;
    }

    public async Task<bool> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        if (_collectionStore is null) throw new InvalidOperationException("A collection store is required.");
        var result = await _collectionStore.DeleteCollectionAsync(collectionId, cancellationToken);
        await RefreshCollectionsAsync(cancellationToken);
        return result;
    }

    public void AttachAttentionService(IAttentionService attentionService)
    {
        ArgumentNullException.ThrowIfNull(attentionService);
        if (ReferenceEquals(_attentionService, attentionService))
            return;

        if (_attentionService is not null)
            _attentionService.Changed -= AttentionServiceOnChanged;

        _attentionService = attentionService;
        _attentionService.Changed += AttentionServiceOnChanged;
        RefreshAttentionGameIds();
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

    public void Dispose()
    {
        if (_attentionService is not null)
            _attentionService.Changed -= AttentionServiceOnChanged;
        if (_sessionMonitor is not null)
            _sessionMonitor.SnapshotUpdated -= SessionMonitor_OnSnapshotUpdated;
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

        UpdateCollectionMemberships(item.GameId);

        _ = EnsureSelectedLogoAsync(
            item);
    }

    public void ClearSelection()
    {
        SetSelectedItem(
            null);

        SetSelectedGame(
            null);
        UpdateCollectionMemberships(null);
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
            VerticalOffset)
        {
            QuickFilter = QuickFilter,
            ProviderFilters = _providerFilterOptions.Where(option => option.IsSelected).Select(option => option.Key).ToArray(),
            DriveFilters = _driveFilterOptions.Where(option => option.IsSelected).Select(option => option.Key).ToArray(),
            CollectionFilters = _collectionOptions.Where(option => option.IsFilterSelected).Select(option => option.Id).ToArray(),
            SearchText = SearchQuery
        };
    }

    public void RestoreUiState(
        LibraryUiState state)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        SetViewMode(
            state.ViewMode);

        SetQuickFilter(state.QuickFilter);

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

        SetSearchQuery(state.SearchText ?? string.Empty);

        var providerFilters = state.ProviderFilters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var option in _providerFilterOptions)
            option.IsSelected = providerFilters.Contains(option.Key);

        var driveFilters = state.DriveFilters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var option in _driveFilterOptions)
            option.IsSelected = driveFilters.Contains(option.Key);

        var collectionFilters = state.CollectionFilters.ToHashSet();
        foreach (var option in _collectionOptions)
            option.IsFilterSelected = collectionFilters.Contains(option.Id);

        NotifyAdvancedFilterProjectionChanged();

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
    public Task EnsureLogoAsync(
        LibraryItemViewModel item,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(
                cancellationToken);
        }

        if (item.HasLogo ||
            item.Provider != ProviderKind.Steam)
        {
            return Task.CompletedTask;
        }

        TaskCompletionSource completion;

        lock (_logoLoadsGate)
        {
            if (_logoLoads.TryGetValue(
                    item.GameId,
                    out var existingLoad))
            {
                return existingLoad;
            }

            completion =
                new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            _logoLoads[item.GameId] =
                completion.Task;
        }

        _ = CompleteLogoLoadAsync(
            item,
            cancellationToken,
            completion);

        return completion.Task;
    }

    private async Task CompleteLogoLoadAsync(
        LibraryItemViewModel item,
        CancellationToken cancellationToken,
        TaskCompletionSource completion)
    {
        try
        {
            await EnsureLogoCoreAsync(
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
            lock (_logoLoadsGate)
            {
                if (_logoLoads.TryGetValue(
                        item.GameId,
                        out var currentLoad) &&
                    ReferenceEquals(
                        currentLoad,
                        completion.Task))
                {
                    _logoLoads.Remove(
                        item.GameId);
                }
            }
        }
    }

    private async Task EnsureLogoCoreAsync(
        LibraryItemViewModel item,
        CancellationToken cancellationToken)
    {
        if (item.HasLogo)
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
                    GameMediaAssetType.Logo,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (path is not null)
            {
                item.SetLogoPath(
                    path);
            }
        }
        finally
        {
            _mediaGate.Release();
        }
    }

    private async Task EnsureSelectedLogoAsync(
        LibraryItemViewModel item)
    {
        try
        {
            await EnsureLogoAsync(
                item,
                CancellationToken.None);
        }
        catch
        {
            // Logo media is cosmetic. Selection and launch behavior must remain available.
        }
    }

    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        var snapshot =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);

        await RefreshActivityProjectionAsync(snapshot.Installations, cancellationToken);

        _installations =
            snapshot.Installations.ToArray();

        Items =
            BuildItems(
                snapshot,
                checking: false);
        await RefreshCollectionsAsync(cancellationToken);
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
                                game.Id),
                            game.CanonicalContentId);

                    ApplyCachedCover(
                        item,
                        installation);

                    ApplyCachedLogo(
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

    private void ApplyCachedLogo(
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

        item.SetLogoPath(
            _gameMediaResolver.TryGetCachedPath(
                identity,
                GameMediaAssetType.Logo));
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
        if (_uiDispatcher is not null)
        {
            if (_uiDispatcher.HasShutdownStarted ||
                _uiDispatcher.HasShutdownFinished)
            {
                return;
            }

            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.BeginInvoke(
                    () => SessionMonitor_OnSnapshotUpdated(snapshot),
                    DispatcherPriority.Normal);

                return;
            }
        }

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
                ? VisibleItems.FirstOrDefault(
                    item =>
                        item.GameId == selectedGameId)
                : null;

        SetSelectedItem(
            selectedItem);
    }

    public async Task RefreshActivityProjectionAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        var providerValues = _providerActivityStore is null
            ? Array.Empty<ProviderActivityMetadata>()
            : await _providerActivityStore.GetAllAsync(cancellationToken);
        var providerByGame = providerValues.ToDictionary(value => value.GameId);
        var projection = new Dictionary<GameId, LibraryActivityProjection>();

        foreach (var installation in installations.Where(item => item.IsPresent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = providerByGame.TryGetValue(installation.GameId, out var value)
                ? value
                : null;
            TimeSpan? observedPlaytime = null;
            DateTimeOffset? observedLastPlayed = null;
            if (_sessionStore is not null)
            {
                var sessions = await _sessionStore.GetByGameAsync(installation.GameId.Value, cancellationToken);
                var completed = sessions
                    .Where(session => session.State is SessionState.Ended or SessionState.Recovered &&
                                      session.ObservedEndedAtUtc is not null &&
                                      session.ObservedEndedAtUtc.Value > session.ObservedStartedAtUtc)
                    .ToArray();
                observedPlaytime = completed.Length == 0
                    ? null
                    : completed.Aggregate(TimeSpan.Zero, (total, session) =>
                        total + (session.ObservedEndedAtUtc!.Value - session.ObservedStartedAtUtc));
                observedLastPlayed = sessions
                    .Select(session => session.ObservedEndedAtUtc ?? session.LastSeenAtUtc)
                    .OrderByDescending(value => value)
                    .FirstOrDefault();
            }

            projection[installation.GameId] = new LibraryActivityProjection(
                provider?.TotalPlaytime,
                provider?.LastPlayedAtUtc,
                observedPlaytime,
                observedLastPlayed);
        }

        _activityByGame = projection;
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(GridRows));
    }

    private IReadOnlyList<LibraryItemViewModel> ApplyProjection()
    {
        if (_quickFilter == LibraryQuickFilter.Installed &&
            string.IsNullOrWhiteSpace(SearchQuery) &&
            SortKey == "Title" &&
            !HasAdvancedFilters)
        {
            return Items;
        }

        var filtered = _quickFilter == LibraryQuickFilter.Attention
            ? Items.Where(item => _attentionGameIds.Contains(item.GameId))
            : Items.AsEnumerable();

        var selectedProviders = _providerFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Key)
            .ToHashSet(StringComparer.Ordinal);
        var selectedDrives = _driveFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedProviders.Count > 0)
            filtered = filtered.Where(item => selectedProviders.Contains(item.Provider.ToString()));
        if (selectedDrives.Count > 0)
            filtered = filtered.Where(item =>
            {
                var installation = _installations.FirstOrDefault(value => value.GameId == item.GameId);
                var drive = installation is null ? null : Path.GetPathRoot(installation.InstallPath)?.TrimEnd('\\');
                return drive is not null && selectedDrives.Contains(drive);
            });
        var selectedCollections = _collectionOptions
            .Where(option => option.IsFilterSelected)
            .Select(option => option.Id)
            .ToHashSet();
        if (selectedCollections.Count > 0)
            filtered = filtered.Where(item => _collectionMemberships.Any(membership => membership.GameId == item.GameId && selectedCollections.Contains(membership.CollectionId)));

        var searched = LibrarySearchService.Search(filtered.ToArray(), SearchQuery);
        return SortKey switch
        {
            "Recent" => searched
                .OrderByDescending(item => EffectiveActivity(item.GameId).LastPlayedAtUtc.HasValue)
                .ThenByDescending(item => EffectiveActivity(item.GameId).LastPlayedAtUtc)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            "Playtime" => searched
                .OrderByDescending(item => EffectiveActivity(item.GameId).Playtime.HasValue)
                .ThenByDescending(item => EffectiveActivity(item.GameId).Playtime)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            "Size" => searched
                .OrderByDescending(item => item.InstalledSizeBytes.HasValue)
                .ThenByDescending(item => item.InstalledSizeBytes)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            "Provider" => searched
                .OrderBy(item => item.ProviderLabel, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            _ => searched
                .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
        };
    }

    private LibraryActivityProjection EffectiveActivity(GameId gameId) =>
        _activityByGame.TryGetValue(gameId, out var activity)
            ? activity
            : LibraryActivityProjection.Empty;

    private sealed record LibraryActivityProjection(
        TimeSpan? ProviderPlaytime,
        DateTimeOffset? ProviderLastPlayedAtUtc,
        TimeSpan? ObservedPlaytime,
        DateTimeOffset? ObservedLastPlayedAtUtc)
    {
        public static LibraryActivityProjection Empty { get; } = new(null, null, null, null);

        public TimeSpan? Playtime => ProviderPlaytime ?? ObservedPlaytime;

        public DateTimeOffset? LastPlayedAtUtc =>
            GetLastPlayedAtUtc();

        private DateTimeOffset? GetLastPlayedAtUtc()
        {
            var values = new[] { ProviderLastPlayedAtUtc, ObservedLastPlayedAtUtc }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
            return values.Length == 0 ? null : values.Max();
        }
    }

    private void AttentionServiceOnChanged(object? sender, EventArgs e) =>
        RefreshAttentionGameIds();

    private void RefreshAdvancedFilterOptions()
    {
        var selectedProviders = _providerFilterOptions.Where(option => option.IsSelected).Select(option => option.Key).ToHashSet(StringComparer.Ordinal);
        var selectedDrives = _driveFilterOptions.Where(option => option.IsSelected).Select(option => option.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _providerFilterOptions = Items.Select(item => item.Provider).Distinct().OrderBy(value => value)
            .Select(value => CreateFilterOption(value.ToString(), value.ToString(), selectedProviders.Contains(value.ToString()))).ToArray();
        _driveFilterOptions = _installations.Select(value => Path.GetPathRoot(value.InstallPath)?.TrimEnd('\\'))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => CreateFilterOption(value!, value!, selectedDrives.Contains(value!))).ToArray();
        OnPropertyChanged(nameof(ProviderFilterOptions));
        OnPropertyChanged(nameof(DriveFilterOptions));
        OnPropertyChanged(nameof(HasDriveFilterOptions));
        NotifyAdvancedFilterProjectionChanged();
    }

    private LibraryFilterOption CreateFilterOption(string key, string label, bool selected)
    {
        var option = new LibraryFilterOption(key, label) { IsSelected = selected };
        option.Changed += AdvancedFilterOptionOnChanged;
        return option;
    }

    private void AdvancedFilterOptionOnChanged(object? sender, EventArgs e) =>
        NotifyAdvancedFilterProjectionChanged();

    private void CollectionFilterChanged(object? sender, EventArgs e) =>
        NotifyAdvancedFilterProjectionChanged();

    private async void CollectionMembershipChanged(object? sender, EventArgs e)
    {
        if (_updatingCollectionOption || _collectionStore is null || sender is not LibraryCollectionOption option || SelectedGameId is not GameId gameId)
            return;
        try
        {
            await _collectionStore.SetMembershipAsync(option.Id, gameId, option.IsMember, CancellationToken.None);
            _collectionMemberships = (await _collectionStore.GetMembershipsAsync(CancellationToken.None)).ToHashSet();
            OnPropertyChanged(nameof(CollectionNamesByGame));
            NotifyAdvancedFilterProjectionChanged();
        }
        catch
        {
            _updatingCollectionOption = true;
            option.IsMember = !option.IsMember;
            _updatingCollectionOption = false;
        }
    }

    private void NotifyAdvancedFilterProjectionChanged()
    {
        OnPropertyChanged(nameof(ActiveAdvancedFilterCategoryCount));
        OnPropertyChanged(nameof(AdvancedFilterButtonLabel));
        OnPropertyChanged(nameof(HasAdvancedFilters));
        OnPropertyChanged(nameof(IsAdvancedFilterEmpty));
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(GridRows));
        ReconcileSelectedItem();
    }

    private void RefreshAttentionGameIds()
    {
        _attentionGameIds = _attentionService?.Items
            .Where(item => item.GameId is not null)
            .Select(item => new GameId(item.GameId!.Value))
            .ToHashSet() ?? [];
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(GridRows));
        ReconcileSelectedItem();
    }

    private void UpdateCollectionMemberships(GameId? gameId)
    {
        _updatingCollectionOption = true;
        foreach (var option in _collectionOptions)
            option.IsMember = gameId is not null && _collectionMemberships.Contains(new GameCollectionMembership(option.Id, gameId.Value));
        _updatingCollectionOption = false;
        OnPropertyChanged(nameof(CollectionOptions));
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
