using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Home;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.Core.Shortlist;
using PlayStead.Core.Notifications;
using PlayStead.Core.Steam;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Core.GameBuildHistory;
using PlayStead.UI.Launching;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;

namespace PlayStead.UI.Home;

public sealed class HomeViewModel :
    INotifyPropertyChanged,
    IDisposable
{
    private const int RecentlyPlayedGameLimit = 5;

    private readonly LibraryViewModel _libraryViewModel;
    private readonly SessionViewModel _sessionViewModel;
    private readonly NavigationService _navigationService;
    private readonly ILibraryStore? _libraryStore;
    private readonly ISessionStore? _sessionStore;
    private readonly SessionMonitor? _sessionMonitor;
    private readonly IGameMediaResolver? _mediaResolver;
    private readonly ILogger<HomeViewModel>? _logger;
    private readonly GameLaunchService? _gameLaunchService;
    private readonly IGamesDuMomentService? _gamesDuMomentService;
    private readonly IWeeklyActivitySummaryService? _weeklyActivitySummaryService;
    private readonly IAttentionService? _attentionService;
    private readonly IProviderActivityMetadataStore? _providerActivityStore;
    private IEffectiveActivityService? _effectiveActivityService;
    private readonly ProviderActivityReconciliationService? _providerActivityReconciliation;
    private readonly IProviderGameMetadataStore? _providerGameMetadataStore;
    private readonly HomeSuggestionSelector? _homeSuggestionSelector;
    private readonly ProviderInstallUpdateStateReconciliationService? _providerInstallUpdates;
    private readonly IProviderGameMetadataProgress? _providerGameMetadataProgress;
    private readonly GameBuildHistoryService? _gameBuildHistoryService;
    private readonly UiPreferencesStore? _uiPreferencesStore;
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private CancellationTokenSource? _heroCancellation;
    private CancellationTokenSource? _activeHeroCancellation;
    private CancellationTokenSource? _dormantMediaCancellation;
    private CancellationTokenSource? _suggestionMediaCancellation;
    private CancellationTokenSource? _attentionMediaCancellation;
    private long _attentionMediaVersion;
    private IReadOnlyDictionary<Guid, string> _recentLandscapeMediaPaths =
        new Dictionary<Guid, string>();
    private LibrarySnapshot? _latestLibrarySnapshot;
    private (HomeEditorialPrimarySourceKind SourceKind, Guid? GameId, DateTimeOffset? StartedAtUtc,
        ProviderKind? Provider, string? Id, string? Title) _mediaKey;
    private (Guid? GameId, DateTimeOffset? StartedAtUtc, ProviderKind? Provider, string? Id, string? Title)
        _activeMediaKey;
    private (Guid? GameId, ProviderKind? Provider, string? Id, string? Title) _dormantMediaKey;
    private (Guid? GameId, ProviderKind? Provider, string? Id, string? Title) _suggestionMediaKey;
    private long _refreshVersion;
    private long _heroVersion;
    private long _activeHeroVersion;
    private long _dormantMediaVersion;
    private long _suggestionMediaVersion;
    private bool _mediaStarted;
    private bool _disposed;
    private CancellationTokenSource? _metadataProgressDelayCancellation;
    private ProviderGameMetadataProgress _metadataProgress = new(false, 0, 0, 0, 0);
    private bool _isMetadataProgressVisible;
    private bool _dormantSelectionInitialized;
    private Guid? _selectedDormantGameId;
    private UiPreferences? _loadedUiPreferences;
    private readonly Dispatcher? _uiDispatcher;
    private readonly SemaphoreSlim _dormantSelectionGate = new(1, 1);
    private HashSet<Guid> _activeSessionIds = [];
    private IReadOnlyList<ProviderGameMetadata> _providerMetadata = [];
    private IReadOnlyDictionary<Guid, EffectiveActivitySnapshot> _effectiveActivityByGame =
        new Dictionary<Guid, EffectiveActivitySnapshot>();
    private HomeEditorialPrimary _editorialPrimary = new(
        HomeEditorialPrimarySourceKind.Placeholder,
        null);
    private HomeEditorialActiveSession? _activeSession;
    private readonly RelayCommand _primaryActionCommand;
    private readonly RelayCommand _openPrimaryDetailsCommand;
    private readonly RelayCommand _openActiveSessionDetailsCommand;
    private readonly RelayCommand _openDormantGameDetailsCommand;
    private readonly RelayCommand _playSuggestionCommand;
    private readonly RelayCommand _openSuggestionDetailsCommand;
    private readonly RelayCommand _openSuggestionChangesCommand;
    private readonly RelayCommand _openDormantChangesCommand;

    public HomeViewModel(
        LibraryViewModel libraryViewModel,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ILibraryStore libraryStore,
        ISessionStore sessionStore,
        SessionMonitor sessionMonitor,
        IGameMediaResolver mediaResolver,
        ILogger<HomeViewModel> logger,
        GameLaunchService? gameLaunchService = null,
        IGamesDuMomentService? gamesDuMomentService = null,
        IWeeklyActivitySummaryService? weeklyActivitySummaryService = null,
        IAttentionService? attentionService = null,
        TimeProvider? timeProvider = null)
        : this(libraryViewModel, sessionViewModel, navigationService)
    {
        ArgumentNullException.ThrowIfNull(libraryStore);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionMonitor);
        ArgumentNullException.ThrowIfNull(mediaResolver);
        ArgumentNullException.ThrowIfNull(logger);
        _libraryStore = libraryStore;
        _sessionStore = sessionStore;
        _sessionMonitor = sessionMonitor;
        _mediaResolver = mediaResolver;
        _logger = logger;
        _gameLaunchService = gameLaunchService;
        _gamesDuMomentService = gamesDuMomentService;
        _weeklyActivitySummaryService = weeklyActivitySummaryService;
        _attentionService = attentionService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.FromThread(Thread.CurrentThread);
        _activeSessionIds = sessionMonitor.LatestSnapshot?.ActiveSessions
            .Select(session => session.SessionId).ToHashSet() ?? [];
        sessionMonitor.SnapshotUpdated += SessionMonitor_OnSnapshotUpdated;
        if (_attentionService is not null) _attentionService.Changed += AttentionService_OnChanged;
    }

    public HomeViewModel(
        LibraryViewModel libraryViewModel, SessionViewModel sessionViewModel, NavigationService navigationService,
        ILibraryStore libraryStore, ISessionStore sessionStore, SessionMonitor sessionMonitor,
        IGameMediaResolver mediaResolver, ILogger<HomeViewModel> logger, GameLaunchService? gameLaunchService,
        IGamesDuMomentService? gamesDuMomentService, IWeeklyActivitySummaryService? weeklyActivitySummaryService,
        IAttentionService? attentionService, IProviderActivityMetadataStore providerActivityStore,
        ProviderActivityReconciliationService providerActivityReconciliation, TimeProvider? timeProvider = null,
        UiPreferencesStore? uiPreferencesStore = null,
        IProviderGameMetadataStore? providerGameMetadataStore = null,
        HomeSuggestionSelector? homeSuggestionSelector = null,
        ProviderInstallUpdateStateReconciliationService? providerInstallUpdates = null,
        IProviderGameMetadataProgress? providerGameMetadataProgress = null,
        GameBuildHistoryService? gameBuildHistoryService = null)
        : this(libraryViewModel, sessionViewModel, navigationService, libraryStore, sessionStore, sessionMonitor,
            mediaResolver, logger, gameLaunchService, gamesDuMomentService, weeklyActivitySummaryService,
            attentionService, timeProvider)
    {
        _providerActivityStore = providerActivityStore;
        _providerActivityReconciliation = providerActivityReconciliation;
        _providerGameMetadataStore = providerGameMetadataStore;
        _homeSuggestionSelector = homeSuggestionSelector;
        _providerInstallUpdates = providerInstallUpdates;
        _providerGameMetadataProgress = providerGameMetadataProgress;
        _gameBuildHistoryService = gameBuildHistoryService;
        _uiPreferencesStore = uiPreferencesStore;
        _providerActivityReconciliation.Changed += ProviderActivity_OnChanged;
        if (_providerInstallUpdates is not null) _providerInstallUpdates.Changed += ProviderInstallUpdates_OnChanged;
        if (_providerGameMetadataProgress is not null)
        {
            _metadataProgress = _providerGameMetadataProgress.Current;
            _providerGameMetadataProgress.ProgressChanged += ProviderGameMetadataProgress_OnChanged;
        }
    }

    public bool IsMetadataProgressVisible => _isMetadataProgressVisible;
    public int MetadataProgressTotal => _metadataProgress.Total;
    public int MetadataProgressCompleted => _metadataProgress.Completed;
    public int MetadataProgressSucceeded => _metadataProgress.Succeeded;
    public int MetadataProgressFailed => _metadataProgress.Failed;
    public string MetadataProgressLabel => $"{_metadataProgress.Completed} / {_metadataProgress.Total}";

    public HomeViewModel(
        LibraryViewModel libraryViewModel,
        SessionViewModel sessionViewModel,
        NavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(libraryViewModel);
        ArgumentNullException.ThrowIfNull(sessionViewModel);
        ArgumentNullException.ThrowIfNull(navigationService);

        _libraryViewModel = libraryViewModel;
        _sessionViewModel = sessionViewModel;
        _navigationService = navigationService;
        EditorialPlaceholderAssetPath = HomeHeroIdlePlaceholderPool.EditorialPlaceholderResourcePath;
        EditorialPlaceholderImageSource = HomeHeroIdlePlaceholderPool.TryLoad(EditorialPlaceholderAssetPath);
        DormantPlaceholderAssetPath = HomeHeroIdlePlaceholderPool.DormantPlaceholderResourcePath;
        DormantPlaceholderImageSource = HomeHeroIdlePlaceholderPool.TryLoad(DormantPlaceholderAssetPath);
        IdleHeroAssetPath = HomeHeroIdlePlaceholderPool.SelectPath(Random.Shared);
        IdleHeroImageSource = HomeHeroIdlePlaceholderPool.TryLoad(IdleHeroAssetPath);

        NavigateLibraryCommand =
            new RelayCommand(
                () => _navigationService.Navigate(
                    new NavigationRequest(AppRoute.Library)));

        NavigateSessionsCommand =
            new RelayCommand(
                () => _navigationService.Navigate(
                    new NavigationRequest(AppRoute.Sessions)));

        OpenRecentlyPlayedDetailsCommand = new RelayCommand<Guid>(
            gameId => _navigationService.Navigate(
                new NavigationRequest(AppRoute.GameDetail, new GameId(gameId))));
        OpenGamesDuMomentDetailsCommand = new RelayCommand<Guid>(gameId => _navigationService.Navigate(new NavigationRequest(AppRoute.GameDetail, new GameId(gameId))));
        RemoveGamesDuMomentCommand = new AsyncRelayCommand<Guid>(RemoveGamesDuMomentAsync);
        MoveGamesDuMomentLeftCommand = new AsyncRelayCommand<Guid>(MoveGamesDuMomentLeftAsync);
        MoveGamesDuMomentRightCommand = new AsyncRelayCommand<Guid>(MoveGamesDuMomentRightAsync);
        NavigateAttentionCommand = new RelayCommand(() => _navigationService.Navigate(new NavigationRequest(AppRoute.Attention)));
        OpenHomeAttentionItemCommand = new RelayCommand<HomeAttentionItemViewModel>(item =>
        {
            if (item is null) return;
            _navigationService.Navigate(item.Source.GameId is Guid gameId
                ? new NavigationRequest(AppRoute.GameDetail, new GameId(gameId))
                : new NavigationRequest(AppRoute.Attention));
        });
        PlayRecentlyPlayedCommand = new RelayCommand<Guid>(
            PlayRecentlyPlayed,
            CanPlayRecentlyPlayed);
        _primaryActionCommand = new RelayCommand(
            ExecutePrimaryAction,
            () => HasPrimaryGame);
        _openPrimaryDetailsCommand = new RelayCommand(
            OpenPrimaryDetails,
            () => HasPrimarySecondaryAction);
        _openActiveSessionDetailsCommand = new RelayCommand(
            OpenActiveSessionDetails,
            () => HasActiveSessionHero);
        _openDormantGameDetailsCommand = new RelayCommand(
            OpenDormantGameDetails,
            () => HasDormantGame);
        _playSuggestionCommand = new RelayCommand(
            PlaySuggestion,
            () => CanLaunchSuggestion);
        _openSuggestionDetailsCommand = new RelayCommand(
            OpenSuggestionDetails,
            () => HasSuggestion);
        _openSuggestionChangesCommand = new RelayCommand(
            OpenSuggestionChanges,
            () => HasSuggestionBuildChanges);
        _openDormantChangesCommand = new RelayCommand(
            OpenDormantChanges,
            () => HasDormantBuildChanges);
        PrimaryActionCommand = _primaryActionCommand;
        OpenPrimaryDetailsCommand = _openPrimaryDetailsCommand;
        OpenActiveSessionDetailsCommand = _openActiveSessionDetailsCommand;
        OpenDormantGameDetailsCommand = _openDormantGameDetailsCommand;
        PlaySuggestionCommand = _playSuggestionCommand;
        OpenSuggestionDetailsCommand = _openSuggestionDetailsCommand;
        OpenSuggestionChangesCommand = _openSuggestionChangesCommand;
        OpenDormantChangesCommand = _openDormantChangesCommand;

        _libraryViewModel.PropertyChanged += LibraryViewModel_OnPropertyChanged;
        _sessionViewModel.PropertyChanged += SessionViewModel_OnPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AttachEffectiveActivityService(IEffectiveActivityService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _effectiveActivityService = service;
    }

    public int LibraryGameCount =>
        _libraryViewModel.Items.Count;

    public IReadOnlyList<ActiveSessionItemViewModel> ActiveSessions =>
        _sessionViewModel.ActiveSessions;

    public IReadOnlyList<RecentSessionItemViewModel> RecentSessions =>
        _sessionViewModel.RecentSessions;

    public IReadOnlyList<HomeRecentlyPlayedGameViewModel> RecentlyPlayedGames
    {
        get
        {
            var libraryItems = _libraryViewModel.Items
                .ToDictionary(item => item.GameId.Value);
            var seenGameIds = new HashSet<Guid>();
            var games = new List<HomeRecentlyPlayedGameViewModel>(RecentlyPlayedGameLimit);

            var sessions = RecentSessions
                .Select((session, index) => (session, index))
                .OrderByDescending(item =>
                    _effectiveActivityByGame.TryGetValue(item.session.GameId, out var effective)
                        ? effective.EffectiveLastPlayedAtUtc ?? DateTimeOffset.MinValue
                        : DateTimeOffset.MinValue)
                .ThenBy(item => item.index)
                .Select(item => item.session);

            foreach (var session in sessions)
            {
                if (!seenGameIds.Add(session.GameId))
                {
                    continue;
                }

                libraryItems.TryGetValue(session.GameId, out var libraryItem);
                games.Add(new HomeRecentlyPlayedGameViewModel(
                    session.GameId,
                    session.Title,
                    session.StartedAtLabel,
                    session.DurationLabel,
                    libraryItem,
                    _recentLandscapeMediaPaths.TryGetValue(
                        session.GameId,
                        out var landscapeMediaPath)
                        ? landscapeMediaPath
                        : null));

                if (games.Count == RecentlyPlayedGameLimit)
                {
                    break;
                }
            }

            return games;
        }
    }

    public IReadOnlyList<HomeGamesDuMomentViewModel> GamesDuMoment { get; private set; } = [];

    public bool HasGamesDuMoment => GamesDuMoment.Count > 0;
    public bool HasNoGamesDuMoment => !HasGamesDuMoment;

    public IReadOnlyList<HomeAttentionItemViewModel> HomeAttentionItems { get; private set; } = [];
    public bool HasHomeAttention => HomeAttentionItems.Count > 0;

    public WeeklyActivitySummary WeeklySummary { get; private set; } = WeeklyActivitySummary.Empty;
    public HomeEditorialDormantGame? DormantGame { get; private set; }
    public bool HasDormantGame => DormantGame is not null;
    public string? DormantGameTitle => DormantGame?.Game.Title;
    public string? DormantGameContext => DormantGame is null
        ? null
        : $"Ça fait {DormantGame.DaysSinceLastPlayed} jours que celui-là n’a pas tourné.";
    public string? DormantGameDescription
    {
        get
        {
            var identity = DormantGame?.Game.MediaIdentity;
            if (identity is null) return null;
            return _providerMetadata.FirstOrDefault(metadata =>
                metadata.GameId == DormantGame!.Game.GameId &&
                metadata.Provider == identity.Provider)?.ShortDescription;
        }
    }
    public bool HasDormantGameDescription => !string.IsNullOrWhiteSpace(DormantGameDescription);
    public string? DormantGameMediaPath { get; private set; }
    public HomeSuggestion? Suggestion { get; private set; }
    public bool HasSuggestion => Suggestion is not null;
    public string? SuggestionTitle => Suggestion?.Title;
    public string? SuggestionEditorialTitle => Suggestion is null
        ? null
        : Suggestion.SelectionDay switch
        {
            DayOfWeek.Friday => "Vendredi coop ?",
            DayOfWeek.Saturday or DayOfWeek.Sunday => "Week-end coop ?",
            _ => Suggestion.Title
        };
    public string? SuggestionEditorialLine => Suggestion is null
        ? null
        : Suggestion.SelectionDay switch
        {
            DayOfWeek.Friday => "Vendredi coop — On joue ensemble ?",
            DayOfWeek.Saturday or DayOfWeek.Sunday => "Week-end coop — On joue à plusieurs ?",
            _ => SuggestionEditorialTitle
        };
    public string? SuggestionSupportingText => Suggestion is null
        ? null
        : Suggestion.SelectionDay is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday
            ? Suggestion.SelectionDay == DayOfWeek.Friday ? "Ce soir, on joue ensemble ?" : "On joue à plusieurs ?"
            : "Je te garde une idée pour ta prochaine session.";
    public bool HasSuggestionWeekdayContext => Suggestion is not null && !HasSuggestionWeekendIdentity;
    public bool HasSuggestionWeekendIdentity => Suggestion is not null &&
        Suggestion.SelectionDay is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;
    public string? SuggestionGameIdentity => HasSuggestionWeekendIdentity ? Suggestion?.Title : null;
    public string? SuggestionDescription => Suggestion?.Metadata?.ShortDescription;
    public bool HasSuggestionDescription => !string.IsNullOrWhiteSpace(SuggestionDescription);
    public string? SuggestionCapabilityText
    {
        get
        {
            var metadata = Suggestion?.Metadata;
            if (metadata is null) return "Coop en ligne";
            if (metadata.OnlineCoop is true && metadata.OnlineCoopMaxPlayers is int onlineMax)
                return $"Coop en ligne · jusqu’à {onlineMax} joueurs";
            var capability = HasSuggestionWeekendIdentity
                ? metadata.OnlineCoop is true ? "Coop en ligne"
                : metadata.LocalCoop is true ? "Coop locale"
                : metadata.MultiPlayer is true ? "Multijoueur"
                : metadata.SinglePlayer is true ? "Solo" : null
                : metadata.SinglePlayer is true ? "Solo"
                : metadata.MultiPlayer is true ? "Multijoueur"
                : metadata.OnlineCoop is true ? "Coop en ligne"
                : metadata.LocalCoop is true ? "Coop locale" : null;
            return capability ?? "Coop en ligne";
        }
    }
    public bool HasSuggestionUpdate => Suggestion?.UpdateState?.Status is
        ProviderInstallUpdateStatus.UpdateAvailable or
        ProviderInstallUpdateStatus.Downloading or
        ProviderInstallUpdateStatus.Staging;
    public string? SuggestionUpdateStatusText => HasSuggestionUpdate
        ? "Mise à jour disponible"
        : null;
    public string? SuggestionMediaPath { get; private set; }
    public bool HasSuggestionMedia => !string.IsNullOrWhiteSpace(SuggestionMediaPath);
    public int SuggestionBuildChangeCount => Suggestion is null
        ? 0
        : _buildChangeCounts.GetValueOrDefault((Suggestion.GameId.Value, Suggestion.Provider));
    public bool HasSuggestionBuildChanges => SuggestionBuildChangeCount > 0;
    public string? SuggestionBuildChangeText => FormatBuildChangeText(SuggestionBuildChangeCount);
    public bool CanLaunchSuggestion =>
        HasSuggestion &&
        _gameLaunchService is not null &&
        CreateSuggestionLaunchViewModel()?.CanPlay == true;
    public bool HasWeeklyActivity => WeeklySummary.SessionCount > 0;
    public string WeeklyPlayTimeLabel => WeeklySummary.TotalPlayTime.TotalHours >= 1
        ? $"{(int)WeeklySummary.TotalPlayTime.TotalHours} h {WeeklySummary.TotalPlayTime.Minutes:00}"
        : $"{WeeklySummary.TotalPlayTime.Minutes} min";
    public string WeeklyPlayTimeTitle => WeeklySummary.Coverage switch
    {
        WeeklyActivityCoverage.Complete => "Temps joué cette semaine",
        WeeklyActivityCoverage.Partial => "Temps joué cette semaine · partiel",
        _ => "Temps observé cette semaine"
    };
    public string WeeklySessionsTitle => WeeklySummary.Coverage switch
    {
        WeeklyActivityCoverage.Complete => "Sessions cette semaine",
        WeeklyActivityCoverage.Partial => "Sessions cette semaine · partiel",
        _ => "Sessions observées cette semaine"
    };
    public int DormantBuildChangeCount => DormantGame is null
        ? 0
        : DormantGame.Game.MediaIdentity is null
            ? 0
            : _buildChangeCounts.GetValueOrDefault((DormantGame.Game.GameId.Value, DormantGame.Game.MediaIdentity.Provider));
    public bool HasDormantBuildChanges => DormantBuildChangeCount > 0;
    public string? DormantBuildChangeText => FormatBuildChangeText(DormantBuildChangeCount);
    public int GamesChangedSinceLastPlayCount => _buildChangeCounts
        .Where(pair => pair.Value > 0)
        .Select(pair => pair.Key.GameId)
        .Distinct()
        .Count();
    public string GamesChangedSinceLastPlayLabel => "jeux ont changé";
    public string GamesChangedSinceLastPlaySubtitle => "depuis ta dernière partie";

    public bool HasRecentlyPlayedGames =>
        RecentSessions.Count > 0;
    public bool HasNoRecentlyPlayedGames => !HasRecentlyPlayedGames;

    public bool HasRecentActivity =>
        _sessionViewModel.HasRecentSessions;

    public ICommand NavigateLibraryCommand { get; }

    public ICommand NavigateSessionsCommand { get; }

    public ICommand PlayRecentlyPlayedCommand { get; }

    public ICommand OpenRecentlyPlayedDetailsCommand { get; }

    public ICommand OpenGamesDuMomentDetailsCommand { get; }
    public IAsyncRelayCommand<Guid> RemoveGamesDuMomentCommand { get; }
    public IAsyncRelayCommand<Guid> MoveGamesDuMomentLeftCommand { get; }
    public IAsyncRelayCommand<Guid> MoveGamesDuMomentRightCommand { get; }
    public ICommand NavigateAttentionCommand { get; }
    public ICommand OpenHomeAttentionItemCommand { get; }
    public ICommand PrimaryActionCommand { get; }
    public ICommand OpenPrimaryDetailsCommand { get; }
    public ICommand OpenActiveSessionDetailsCommand { get; }
    public ICommand OpenDormantGameDetailsCommand { get; }
    public ICommand PlaySuggestionCommand { get; }
    public ICommand OpenSuggestionDetailsCommand { get; }
    public ICommand OpenSuggestionChangesCommand { get; }
    public ICommand OpenDormantChangesCommand { get; }

    public async Task RefreshGamesDuMomentAsync(CancellationToken cancellationToken)
    {
        await RefreshFeaturedGameAsync(cancellationToken);
    }

    public async Task RefreshWeeklySummaryAsync(CancellationToken cancellationToken)
    {
        if (_weeklyActivitySummaryService is null) return;
        WeeklySummary = await _weeklyActivitySummaryService.GetAsync(DateTimeOffset.UtcNow, cancellationToken);
        OnPropertyChanged(nameof(WeeklySummary));
        OnPropertyChanged(nameof(HasWeeklyActivity));
        OnPropertyChanged(nameof(WeeklyPlayTimeLabel));
        OnPropertyChanged(nameof(WeeklyPlayTimeTitle));
        OnPropertyChanged(nameof(WeeklySessionsTitle));
    }

    public async Task RefreshHomeAttentionAsync(CancellationToken cancellationToken)
    {
        if (_attentionService is null) return;
        if (_latestLibrarySnapshot is null && _libraryStore is not null)
            _latestLibrarySnapshot = await _libraryStore.LoadSnapshotAsync(cancellationToken);
        var items = (await _attentionService.GetActiveAsync(cancellationToken)).Take(3).ToArray();
        HomeAttentionItems = CreateHomeAttentionItems(items);
        OnPropertyChanged(nameof(HomeAttentionItems));
        OnPropertyChanged(nameof(HasHomeAttention));
        StartHomeAttentionMediaResolution(_refreshVersion, cancellationToken);
    }

    private void AttentionService_OnChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        var dispatcher = _uiDispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                _ = dispatcher.BeginInvoke(() => _ = RefreshHomeAttentionAsync(CancellationToken.None));
            return;
        }
        _ = RefreshHomeAttentionAsync(CancellationToken.None);
    }

    private void ProviderActivity_OnChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        var dispatcher = _uiDispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                _ = dispatcher.BeginInvoke(() => _ = RefreshFeaturedGameAsync(CancellationToken.None));
            return;
        }
        _ = RefreshFeaturedGameAsync(CancellationToken.None);
    }

    private async Task RemoveGamesDuMomentAsync(Guid gameId)
    {
        if (_gamesDuMomentService is null || _disposed)
        {
            return;
        }

        await _gamesDuMomentService.RemoveAsync(
            new GameId(gameId),
            CancellationToken.None);

        await RefreshFeaturedGameAsync(CancellationToken.None);
    }

    private Task MoveGamesDuMomentLeftAsync(Guid gameId) => MoveGamesDuMomentAsync(gameId, -1);

    private Task MoveGamesDuMomentRightAsync(Guid gameId) => MoveGamesDuMomentAsync(gameId, 1);

    private async Task MoveGamesDuMomentAsync(Guid gameId, int delta)
    {
        if (_gamesDuMomentService is null || _disposed) return;
        var ids = GamesDuMoment.Select(game => new GameId(game.GameId)).ToList();
        var index = ids.FindIndex(id => id.Value == gameId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= ids.Count) return;
        (ids[index], ids[target]) = (ids[target], ids[index]);
        await _gamesDuMomentService.ReorderAsync(ids, CancellationToken.None);
        await RefreshFeaturedGameAsync(CancellationToken.None);
    }

    private void ProviderInstallUpdates_OnChanged(object? sender, EventArgs e)
    {
        if (_disposed || !_mediaStarted) return;
        var dispatcher = _uiDispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                _ = dispatcher.BeginInvoke(() => _ = RefreshFeaturedGameAsync(CancellationToken.None));
            return;
        }
        _ = RefreshFeaturedGameAsync(CancellationToken.None);
    }

    private void ProviderGameMetadataProgress_OnChanged(
        object? sender,
        ProviderGameMetadataProgress progress)
    {
        if (_disposed) return;
        var dispatcher = _uiDispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                _ = dispatcher.BeginInvoke(() => ApplyMetadataProgress(progress));
            return;
        }
        ApplyMetadataProgress(progress);
    }

    private void ApplyMetadataProgress(ProviderGameMetadataProgress progress)
    {
        if (_disposed) return;
        var wasRunning = _metadataProgress.IsRunning;
        _metadataProgress = progress;
        OnPropertyChanged(nameof(MetadataProgressTotal));
        OnPropertyChanged(nameof(MetadataProgressCompleted));
        OnPropertyChanged(nameof(MetadataProgressSucceeded));
        OnPropertyChanged(nameof(MetadataProgressFailed));
        OnPropertyChanged(nameof(MetadataProgressLabel));

        if (!progress.IsRunning || progress.Total <= 0)
        {
            _metadataProgressDelayCancellation?.Cancel();
            _metadataProgressDelayCancellation?.Dispose();
            _metadataProgressDelayCancellation = null;
            SetMetadataProgressVisible(false);
            return;
        }

        if (wasRunning)
            return;

        var cancellation = new CancellationTokenSource();
        _metadataProgressDelayCancellation = cancellation;
        _ = ShowMetadataProgressAfterDelayAsync(cancellation.Token);
    }

    private async Task ShowMetadataProgressAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
            if (!_disposed && _metadataProgress.IsRunning && _metadataProgress.Total > 0)
                SetMetadataProgressVisible(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void SetMetadataProgressVisible(bool value)
    {
        if (_isMetadataProgressVisible == value) return;
        _isMetadataProgressVisible = value;
        OnPropertyChanged(nameof(IsMetadataProgressVisible));
    }

    private bool CanPlayRecentlyPlayed(Guid gameId)
    {
        if (_gameLaunchService is null)
        {
            return false;
        }

        var game = new GameId(gameId);
        return new GameLaunchViewModel(
            game,
            _libraryViewModel.GetLaunchInstallations(game),
            _gameLaunchService).CanPlay;
    }

    private void PlayRecentlyPlayed(Guid gameId)
    {
        if (_gameLaunchService is null)
        {
            return;
        }

        var game = new GameId(gameId);
        var launch = new GameLaunchViewModel(
            game,
            _libraryViewModel.GetLaunchInstallations(game),
            _gameLaunchService);
        launch.TryPlayDefault();
    }

    public Guid? FeaturedGameId { get; private set; }
    public string? FeaturedGameTitle { get; private set; }
    public string? HeroPath { get; private set; }
    public bool HasHero => !string.IsNullOrWhiteSpace(HeroPath);
    public string? ActiveSessionHeroPath { get; private set; }
    public bool HasActiveSessionHero => _activeSession is not null;
    public bool HasActiveSessionHeroMedia => !string.IsNullOrWhiteSpace(ActiveSessionHeroPath);
    public Guid? ActiveSessionGameId => _activeSession?.NavigationGameId.Value;
    public string? ActiveSessionStartedAtLabel => _activeSession?.StartedAtUtc
        .ToLocalTime()
        .ToString("HH'h'mm", CultureInfo.CurrentCulture);
    public string ActiveSessionEyebrow => "L’aventure continue";
    public string ActiveSessionTitle => _activeSession?.Game.Title ?? string.Empty;
    public string ActiveSessionSupportingText => HasActiveSessionHero
        ? $"Démarré à {ActiveSessionStartedAtLabel}"
        : string.Empty;
    public HomeEditorialPrimarySourceKind PrimarySourceKind => _editorialPrimary.SourceKind;
    public bool HasPrimaryGame => _editorialPrimary.Game is not null;
    public bool CanLaunchPrimary =>
        HasPrimaryGame &&
        CreatePrimaryLaunchViewModel()?.CanPlay == true;
    public bool HasPrimarySecondaryAction => CanLaunchPrimary;
    public string PrimaryActionLabel => CanLaunchPrimary ? "Jouer" : "Voir le jeu";
    public string EditorialEyebrow => PrimarySourceKind switch
    {
        HomeEditorialPrimarySourceKind.GamesDuMoment => "Jeu du moment",
        HomeEditorialPrimarySourceKind.RecentCompletedSession => "Reprendre l’aventure",
        _ => "Rien en cours pour l’instant."
    };
    public string EditorialTitle => _editorialPrimary.Game?.Title ?? "Prêt à replonger ?";
    public string EditorialSupportingText => PrimarySourceKind switch
    {
        HomeEditorialPrimarySourceKind.GamesDuMoment => "Dans tes jeux du moment",
        HomeEditorialPrimarySourceKind.RecentCompletedSession =>
            $"Dernière session le {FormatEditorialTimestamp(_editorialPrimary.SessionEndedAtUtc)}",
        _ => "Lance un jeu, PlayStead s’occupe du reste."
    };
    public string HeroEyebrow => EditorialEyebrow;
    public string HeroTitle => EditorialTitle;
    public string HeroSupportingText => EditorialSupportingText;
    public string EditorialPlaceholderAssetPath { get; }
    public ImageSource? EditorialPlaceholderImageSource { get; }
    public bool HasEditorialPlaceholderImage => EditorialPlaceholderImageSource is not null;
    public string DormantPlaceholderAssetPath { get; }
    public ImageSource? DormantPlaceholderImageSource { get; }
    public bool HasDormantPlaceholderImage => DormantPlaceholderImageSource is not null;
    public string IdleHeroAssetPath { get; }
    public ImageSource? IdleHeroImageSource { get; }
    public bool HasIdleHeroImage => IdleHeroImageSource is not null;

    public async Task RefreshFeaturedGameAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _mediaResolver is null)
        {
            return;
        }

        _mediaStarted = true;
        var refreshVersion = ++_refreshVersion;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = _sessionMonitor!.LatestSnapshot?.ActiveSessions
                ?? await _sessionStore!.GetActiveAsync(cancellationToken);
            var library = await _libraryStore!.LoadSnapshotAsync(cancellationToken);
            var shortlist = _gamesDuMomentService is null
                ? []
                : await _gamesDuMomentService.GetAsync(cancellationToken);
            var recent = await _sessionStore!.GetRecentAsync(50, cancellationToken);
            var weekly = _weeklyActivitySummaryService is null
                ? WeeklySummary
                : await _weeklyActivitySummaryService.GetAsync(DateTimeOffset.UtcNow, cancellationToken);
            var attention = _attentionService is null
                ? HomeAttentionItems.Select(x => x.Source).ToArray()
                : await _attentionService.GetActiveAsync(cancellationToken);
            var providerActivity = _providerActivityStore is null
                ? []
                : await _providerActivityStore.GetAllAsync(cancellationToken);
            var providerMetadata = _providerGameMetadataStore is null
                ? []
                : await _providerGameMetadataStore.GetAllAsync(cancellationToken);
            _providerMetadata = providerMetadata;
            _effectiveActivityByGame = await LoadEffectiveActivityAsync(library, cancellationToken);
            var suggestion = _homeSuggestionSelector is null
                ? null
                : await _homeSuggestionSelector.SelectAsync(
                    library,
                    providerMetadata,
                    active.Select(session => new GameId(session.GameId)).ToArray(),
                    cancellationToken,
                    _providerInstallUpdates?.GetAll());
            cancellationToken.ThrowIfCancellationRequested();

            if (_disposed || refreshVersion != _refreshVersion)
            {
                return;
            }

            RefreshRecentlyPlayedLandscapeMediaPaths(library);
            _latestLibrarySnapshot = library;
        var snapshot = HomeEditorialProjector.Project(new HomeEditorialProjectionInput(
                library,
                active,
                shortlist,
                recent,
                weekly,
                attention,
                _timeProvider.GetUtcNow(),
                providerActivity,
                _effectiveActivityByGame.ToDictionary(
                    pair => new GameId(pair.Key),
                    pair => pair.Value)));
            await EnsureDormantSelectionAsync(snapshot.EligibleDormantGames, cancellationToken);
            _buildChangeCounts = await LoadBuildChangeCountsAsync(library, cancellationToken);
            ApplyEditorialSnapshot(snapshot, suggestion);
            StartHomeAttentionMediaResolution(_refreshVersion, cancellationToken);
            StartGamesDuMomentMediaResolution(snapshot.GamesDuMoment, refreshVersion, cancellationToken);
            RefreshActiveSessionMedia(snapshot.ActiveSession, cancellationToken);

            var primary = snapshot.Primary;
            var identity = primary.Game?.MediaIdentity;
            var key = (primary.SourceKind, primary.NavigationGameId?.Value, primary.SessionStartedAtUtc,
                identity?.Provider, identity?.ProviderGameId, primary.Game?.Title);
            if (_mediaKey == key)
            {
                return;
            }

            _mediaKey = key;
            var heroVersion = ++_heroVersion;
            _heroCancellation?.Cancel();
            _heroCancellation?.Dispose();
            _heroCancellation = null;
            SetHeroPath(null);

            if (identity is null)
            {
                return;
            }

            var cached = _mediaResolver.TryGetCachedPath(identity, GameMediaAssetType.Hero);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                SetHeroPath(cached);
                return;
            }

            _heroCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // This method observes and logs failures; refresh does not await remote media.
            _ = LoadHeroObservedAsync(identity, heroVersion, _heroCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal lifecycle outcome; keep the current fallback/artwork.
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Home featured game refresh failed.");
        }
    }

    private async Task<IReadOnlyDictionary<Guid, EffectiveActivitySnapshot>> LoadEffectiveActivityAsync(
        LibrarySnapshot library,
        CancellationToken cancellationToken)
    {
        if (_effectiveActivityService is null)
        {
            return new Dictionary<Guid, EffectiveActivitySnapshot>();
        }

        var snapshots = new Dictionary<Guid, EffectiveActivitySnapshot>();
        foreach (var installation in library.Installations.Where(item => item.IsPresent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await _effectiveActivityService.GetAsync(
                installation.GameId,
                installation.Provider,
                cancellationToken);
            if (!snapshots.TryGetValue(installation.GameId.Value, out var current) ||
                (snapshot.EffectiveLastPlayedAtUtc ?? DateTimeOffset.MinValue) >
                (current.EffectiveLastPlayedAtUtc ?? DateTimeOffset.MinValue))
            {
                snapshots[installation.GameId.Value] = snapshot;
            }
        }

        return snapshots;
    }

    private Dictionary<(Guid GameId, ProviderKind Provider), int> _buildChangeCounts = [];

    private async Task<Dictionary<(Guid GameId, ProviderKind Provider), int>> LoadBuildChangeCountsAsync(
        LibrarySnapshot library,
        CancellationToken cancellationToken)
    {
        var counts = library.Installations
            .Where(x => x.IsPresent)
            .Select(x => (GameId: x.GameId.Value, Provider: x.Provider))
            .Distinct()
            .ToDictionary(key => key, _ => 0);
        if (_gameBuildHistoryService is null) return counts;

        foreach (var providerGroup in library.Installations
                     .Where(x => x.IsPresent)
                     .GroupBy(x => x.Provider))
        {
            var providerCounts = await _gameBuildHistoryService
                .CountGamesChangedSinceLastPlayAsync(
                    providerGroup.Select(x => x.GameId),
                    providerGroup.Key,
                    cancellationToken);
            foreach (var (gameId, count) in providerCounts)
            {
                var key = (gameId, providerGroup.Key);
                if (counts.ContainsKey(key)) counts[key] = count;
            }
        }

        return counts;
    }

    public static string? FormatBuildChangeText(int count) => count switch
    {
        <= 0 => null,
        1 => "1 mise à jour depuis ta dernière partie",
        _ => $"{count} mises à jour depuis ta dernière partie"
    };

    private void ApplyEditorialSnapshot(HomeEditorialSnapshot snapshot, HomeSuggestion? suggestion)
    {
        _activeSession = snapshot.ActiveSession;
        _editorialPrimary = snapshot.Primary;
        FeaturedGameId = snapshot.Primary.NavigationGameId?.Value;
        FeaturedGameTitle = snapshot.Primary.Game?.Title;

        GamesDuMoment = CreateGamesDuMomentViewModels(snapshot.GamesDuMoment);
        DormantGame = SelectDormantGame(snapshot);
        RefreshDormantGameMedia(DormantGame);
        Suggestion = suggestion;
        RefreshSuggestionMedia(Suggestion);
        WeeklySummary = snapshot.WeeklySummary;
        HomeAttentionItems = CreateHomeAttentionItems(snapshot.AttentionItems.Take(3));

        foreach (var propertyName in new[]
        {
            nameof(PrimarySourceKind), nameof(HasPrimaryGame), nameof(CanLaunchPrimary),
            nameof(HasPrimarySecondaryAction), nameof(PrimaryActionLabel),
            nameof(EditorialEyebrow), nameof(EditorialTitle), nameof(EditorialSupportingText),
            nameof(FeaturedGameId), nameof(FeaturedGameTitle), nameof(HasActiveSessionHero),
            nameof(ActiveSessionGameId), nameof(ActiveSessionStartedAtLabel), nameof(ActiveSessionEyebrow),
            nameof(ActiveSessionTitle), nameof(ActiveSessionSupportingText),
            nameof(HeroEyebrow), nameof(HeroTitle),
            nameof(HeroSupportingText), nameof(GamesDuMoment), nameof(HasGamesDuMoment),
            nameof(HasNoGamesDuMoment),
            nameof(DormantGame), nameof(HasDormantGame), nameof(DormantGameTitle),
            nameof(DormantGameContext), nameof(DormantGameMediaPath),
            nameof(DormantGameDescription), nameof(HasDormantGameDescription),
            nameof(DormantBuildChangeCount), nameof(HasDormantBuildChanges), nameof(DormantBuildChangeText),
            nameof(Suggestion), nameof(HasSuggestion), nameof(SuggestionTitle),
            nameof(SuggestionEditorialTitle), nameof(SuggestionEditorialLine), nameof(SuggestionSupportingText), nameof(HasSuggestionWeekdayContext), nameof(HasSuggestionWeekendIdentity),
            nameof(SuggestionGameIdentity), nameof(SuggestionDescription), nameof(HasSuggestionDescription),
            nameof(SuggestionCapabilityText), nameof(HasSuggestionUpdate), nameof(SuggestionUpdateStatusText), nameof(SuggestionMediaPath), nameof(HasSuggestionMedia),
            nameof(SuggestionBuildChangeCount), nameof(HasSuggestionBuildChanges), nameof(SuggestionBuildChangeText),
            nameof(CanLaunchSuggestion),
            nameof(WeeklySummary), nameof(HasWeeklyActivity), nameof(WeeklyPlayTimeLabel), nameof(WeeklyPlayTimeTitle), nameof(WeeklySessionsTitle), nameof(GamesChangedSinceLastPlayCount), nameof(GamesChangedSinceLastPlayLabel), nameof(GamesChangedSinceLastPlaySubtitle),
            nameof(HomeAttentionItems), nameof(HasHomeAttention)
        })
        {
            OnPropertyChanged(propertyName);
        }

        _primaryActionCommand.NotifyCanExecuteChanged();
        _openPrimaryDetailsCommand.NotifyCanExecuteChanged();
        _openActiveSessionDetailsCommand.NotifyCanExecuteChanged();
        _openDormantGameDetailsCommand.NotifyCanExecuteChanged();
        _playSuggestionCommand.NotifyCanExecuteChanged();
        _openSuggestionDetailsCommand.NotifyCanExecuteChanged();
        _openSuggestionChangesCommand.NotifyCanExecuteChanged();
        _openDormantChangesCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<HomeGamesDuMomentViewModel> CreateGamesDuMomentViewModels(
        IReadOnlyList<HomeEditorialGame> games)
    {
        var cardWidth = games.Count switch
        {
            1 => 420d,
            2 => 340d,
            _ => 275d
        };
        return games.Select((game, index) => new HomeGamesDuMomentViewModel(
                game.GameId.Value,
                game.Title,
                ResolveCachedLandscapePath(game.MediaIdentity))
            { CardWidth = cardWidth, CanMoveLeft = index > 0, CanMoveRight = index < games.Count - 1 })
            .ToArray();
    }

    private string? ResolveCachedLandscapePath(GameMediaIdentity? identity)
    {
        if (identity is null || _mediaResolver is null) return null;
        var path = _mediaResolver.TryGetCachedPath(identity, GameMediaAssetType.Hero);
        return string.IsNullOrWhiteSpace(path)
            ? _mediaResolver.TryGetCachedPath(identity, GameMediaAssetType.Header)
            : path;
    }

    private HomeEditorialDormantGame? SelectDormantGame(HomeEditorialSnapshot snapshot)
    {
        if (_selectedDormantGameId is not Guid selectedId)
        {
            return null;
        }

        return snapshot.EligibleDormantGames
            .FirstOrDefault(candidate => candidate.Game.GameId.Value == selectedId);
    }

    private async Task EnsureDormantSelectionAsync(
        IReadOnlyList<HomeEditorialDormantGame> candidates,
        CancellationToken cancellationToken)
    {
        await _dormantSelectionGate.WaitAsync(cancellationToken);
        try
        {
            if (candidates.Count == 0 ||
                (_dormantSelectionInitialized && _selectedDormantGameId is Guid selectedId &&
                 candidates.Any(candidate => candidate.Game.GameId.Value == selectedId)))
            {
                return;
            }

            _loadedUiPreferences ??= _uiPreferencesStore is null
                ? new UiPreferences()
                : await _uiPreferencesStore.LoadAsync(cancellationToken);

            var previousId = _dormantSelectionInitialized
                ? _selectedDormantGameId
                : _loadedUiPreferences.LastDormantGameId;
            var selected = HomeDormantGameSelector.Select(candidates, previousId, Random.Shared)!;
            _selectedDormantGameId = selected.Game.GameId.Value;
            _dormantSelectionInitialized = true;

            if (_uiPreferencesStore is not null && _loadedUiPreferences.LastDormantGameId != _selectedDormantGameId)
            {
                _loadedUiPreferences = _loadedUiPreferences with { LastDormantGameId = _selectedDormantGameId };
                try
                {
                    await _uiPreferencesStore.SaveAsync(_loadedUiPreferences, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger?.LogDebug(exception, "Could not persist dormant game selection.");
                }
            }
        }
        finally
        {
            _dormantSelectionGate.Release();
        }
    }

    private void RefreshDormantGameMedia(HomeEditorialDormantGame? dormantGame)
    {
        var identity = dormantGame?.Game.MediaIdentity;
        var key = (dormantGame?.NavigationGameId.Value, identity?.Provider,
            identity?.ProviderGameId, dormantGame?.Game.Title);
        if (_dormantMediaKey == key)
            return;

        _dormantMediaKey = key;
        var version = ++_dormantMediaVersion;
        _dormantMediaCancellation?.Cancel();
        _dormantMediaCancellation?.Dispose();
        _dormantMediaCancellation = null;
        DormantGameMediaPath = ResolveCachedDormantPath(identity);
        OnPropertyChanged(nameof(DormantGameMediaPath));

        if (identity is null || !string.IsNullOrWhiteSpace(DormantGameMediaPath) || _mediaResolver is null)
            return;

        _dormantMediaCancellation = new CancellationTokenSource();
        _ = LoadDormantGameMediaObservedAsync(identity, version, _dormantMediaCancellation.Token);
    }

    private void RefreshSuggestionMedia(HomeSuggestion? suggestion)
    {
        var identity = suggestion?.MediaIdentity;
        var key = (suggestion?.GameId.Value, identity?.Provider,
            identity?.ProviderGameId, suggestion?.Title);
        if (_suggestionMediaKey == key)
            return;

        _suggestionMediaKey = key;
        var version = ++_suggestionMediaVersion;
        _suggestionMediaCancellation?.Cancel();
        _suggestionMediaCancellation?.Dispose();
        _suggestionMediaCancellation = null;
        SuggestionMediaPath = ResolveCachedLandscapePath(identity);
        OnPropertyChanged(nameof(SuggestionMediaPath));
        OnPropertyChanged(nameof(HasSuggestionMedia));

        if (identity is null || !string.IsNullOrWhiteSpace(SuggestionMediaPath) || _mediaResolver is null)
            return;

        _suggestionMediaCancellation = new CancellationTokenSource();
        _ = LoadSuggestionMediaObservedAsync(identity, version, _suggestionMediaCancellation.Token);
    }

    private string? ResolveCachedDormantPath(GameMediaIdentity? identity)
    {
        if (identity is null || _mediaResolver is null) return null;
        var path = _mediaResolver.TryGetCachedPath(identity, GameMediaAssetType.Header);
        return string.IsNullOrWhiteSpace(path)
            ? _mediaResolver.TryGetCachedPath(identity, GameMediaAssetType.Hero)
            : path;
    }

    private async Task LoadSuggestionMediaObservedAsync(
        GameMediaIdentity identity,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = await ResolveLandscapeMediaAsync(identity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_disposed && version == _suggestionMediaVersion)
            {
                SuggestionMediaPath = path;
                OnPropertyChanged(nameof(SuggestionMediaPath));
                OnPropertyChanged(nameof(HasSuggestionMedia));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception,
                "Home suggestion media resolution failed for {GameId}.",
                identity.ProviderGameId);
        }
    }

    private async Task LoadDormantGameMediaObservedAsync(
        GameMediaIdentity identity,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = await ResolveDormantMediaAsync(identity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_disposed && version == _dormantMediaVersion)
            {
                DormantGameMediaPath = path;
                OnPropertyChanged(nameof(DormantGameMediaPath));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception,
                "Home dormant game media resolution failed for {GameId}.",
                identity.ProviderGameId);
        }
    }

    private void StartGamesDuMomentMediaResolution(
        IReadOnlyList<HomeEditorialGame> games,
        long refreshVersion,
        CancellationToken cancellationToken)
    {
        foreach (var game in games)
        {
            if (game.MediaIdentity is null ||
                !string.IsNullOrWhiteSpace(ResolveCachedLandscapePath(game.MediaIdentity)))
            {
                continue;
            }

            _ = LoadGamesDuMomentMediaObservedAsync(
                game.GameId.Value,
                game.MediaIdentity,
                refreshVersion,
                cancellationToken);
        }
    }

    private async Task LoadGamesDuMomentMediaObservedAsync(
        Guid gameId,
        GameMediaIdentity identity,
        long refreshVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = await ResolveLandscapeMediaAsync(identity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || refreshVersion != _refreshVersion || string.IsNullOrWhiteSpace(path))
                return;

            GamesDuMoment = GamesDuMoment
                .Select(game => game.GameId == gameId
                    ? game with { LandscapeMediaPath = path }
                    : game)
                .ToArray();
            OnPropertyChanged(nameof(GamesDuMoment));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception,
                "Home current-rotation media resolution failed for {GameId}.",
                identity.ProviderGameId);
        }
    }

    private async Task<string?> ResolveLandscapeMediaAsync(
        GameMediaIdentity identity,
        CancellationToken cancellationToken)
    {
        var path = ResolveCachedLandscapePath(identity);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        path = await _mediaResolver!.ResolveAndCacheAsync(
            identity, GameMediaAssetType.Hero, cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        return await _mediaResolver.ResolveAndCacheAsync(
            identity, GameMediaAssetType.Header, cancellationToken);
    }

    private async Task<string?> ResolveDormantMediaAsync(
        GameMediaIdentity identity,
        CancellationToken cancellationToken)
    {
        var path = ResolveCachedDormantPath(identity);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        path = await _mediaResolver!.ResolveAndCacheAsync(
            identity, GameMediaAssetType.Header, cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        return await _mediaResolver.ResolveAndCacheAsync(
            identity, GameMediaAssetType.Hero, cancellationToken);
    }

    private async Task<string?> ResolveAttentionThumbnailMediaAsync(
        GameMediaIdentity identity,
        CancellationToken cancellationToken)
    {
        var path = _mediaResolver!.TryGetCachedPath(identity, GameMediaAssetType.Cover);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        path = await _mediaResolver.ResolveAndCacheAsync(
            identity, GameMediaAssetType.Cover, cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        return await ResolveLandscapeMediaAsync(identity, cancellationToken);
    }

    private string? ResolveCachedAttentionThumbnailPath(GameMediaIdentity? identity)
    {
        if (identity is null)
            return null;
        var path = _mediaResolver!.TryGetCachedPath(identity, GameMediaAssetType.Cover);
        return !string.IsNullOrWhiteSpace(path)
            ? path
            : ResolveCachedLandscapePath(identity);
    }

    private static string FormatEditorialTimestamp(DateTimeOffset? timestamp) =>
        timestamp?.ToLocalTime().ToString("d MMMM à HH'h'mm", CultureInfo.CurrentCulture)
        ?? string.Empty;

    private GameLaunchViewModel? CreatePrimaryLaunchViewModel()
    {
        if (_gameLaunchService is null || _editorialPrimary.NavigationGameId is not GameId gameId)
            return null;
        return new GameLaunchViewModel(
            gameId,
            _libraryViewModel.GetLaunchInstallations(gameId),
            _gameLaunchService);
    }

    private void ExecutePrimaryAction()
    {
        if (_editorialPrimary.NavigationGameId is not GameId gameId) return;
        if (CanLaunchPrimary)
        {
            CreatePrimaryLaunchViewModel()?.TryPlayDefault();
            return;
        }
        _navigationService.Navigate(new NavigationRequest(AppRoute.GameDetail, gameId));
    }

    private void OpenPrimaryDetails()
    {
        if (_editorialPrimary.NavigationGameId is GameId gameId)
            _navigationService.Navigate(new NavigationRequest(AppRoute.GameDetail, gameId));
    }

    private void OpenActiveSessionDetails()
    {
        if (_activeSession?.NavigationGameId is GameId gameId)
            _navigationService.Navigate(new NavigationRequest(AppRoute.GameDetail, gameId));
    }

    private void OpenDormantGameDetails()
    {
        if (DormantGame?.NavigationGameId is GameId gameId)
            _navigationService.Navigate(new NavigationRequest(AppRoute.GameDetail, gameId));
    }

    private GameLaunchViewModel? CreateSuggestionLaunchViewModel()
    {
        if (_gameLaunchService is null || Suggestion?.GameId is not GameId gameId)
            return null;
        return new GameLaunchViewModel(
            gameId,
            _libraryViewModel.GetLaunchInstallations(gameId),
            _gameLaunchService);
    }

    private void PlaySuggestion() => CreateSuggestionLaunchViewModel()?.TryPlayDefault();

    private void OpenSuggestionDetails()
    {
        if (Suggestion?.GameId is GameId gameId)
            _navigationService.Navigate(new NavigationRequest(AppRoute.GameDetail, gameId));
    }

    private void OpenSuggestionChanges() => OpenSuggestionDetails();

    private void OpenDormantChanges() => OpenDormantGameDetails();

    private static GameMediaIdentity? CreateMediaIdentity(
        LogicalGame? game,
        IReadOnlyList<GameInstallation> installations)
    {
        if (game is null)
        {
            return null;
        }

        var installation = GameLaunchInstallationSelector.SelectDefault(game.Id, installations);
        if (installation is null)
        {
            return null;
        }

        if (!installation.IsPresent || string.IsNullOrWhiteSpace(installation.InstallPath))
        {
            return null;
        }

        try
        {
            return GameMediaIdentityFactory.Create(game.Id, installation, game.Title);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private IReadOnlyList<HomeAttentionItemViewModel> CreateHomeAttentionItems(
        IEnumerable<AttentionItem> items)
    {
        return items.Select(item =>
        {
            var game = item.GameId is Guid gameId
                ? _latestLibrarySnapshot?.Games.FirstOrDefault(x => x.Id.Value == gameId)
                : null;
            var identity = CreateMediaIdentity(game, _latestLibrarySnapshot?.Installations ?? []);
            var mediaPath = ResolveCachedAttentionThumbnailPath(identity);
            var separator = item.Title.IndexOf(" — ", StringComparison.Ordinal);
            var title = separator > 0 ? item.Title[..separator] : item.Title;
            var status = separator > 0 ? item.Title[(separator + 3)..] : item.Message;
            return new HomeAttentionItemViewModel(item, title, status, mediaPath, identity);
        }).ToArray();
    }

    private void StartHomeAttentionMediaResolution(long refreshVersion, CancellationToken cancellationToken)
    {
        if (_mediaResolver is null) return;
        var identities = HomeAttentionItems
            .Where(x => x.Identity is not null && string.IsNullOrWhiteSpace(x.MediaPath))
            .Select(x => x.Identity!)
            .Distinct()
            .ToArray();
        if (identities.Length == 0) return;

        _attentionMediaCancellation?.Cancel();
        _attentionMediaCancellation?.Dispose();
        _metadataProgressDelayCancellation?.Cancel();
        _metadataProgressDelayCancellation?.Dispose();
        _attentionMediaCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var version = ++_attentionMediaVersion;
        _ = LoadHomeAttentionMediaObservedAsync(identities, version, _attentionMediaCancellation.Token);
    }

    private async Task LoadHomeAttentionMediaObservedAsync(
        IReadOnlyList<GameMediaIdentity> identities,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var identity in identities)
            {
                var path = await ResolveAttentionThumbnailMediaAsync(identity, cancellationToken);
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (!_disposed && version == _attentionMediaVersion)
                {
                    HomeAttentionItems = HomeAttentionItems
                        .Select(item => item.Identity == identity ? item with { MediaPath = path } : item)
                        .ToArray();
                    OnPropertyChanged(nameof(HomeAttentionItems));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Home attention media resolution failed.");
        }
    }

    private void RefreshRecentlyPlayedLandscapeMediaPaths(
        LibrarySnapshot library)
    {
        var paths = new Dictionary<Guid, string>();
        foreach (var recentGame in RecentlyPlayedGames)
        {
            var game = library.Games.FirstOrDefault(candidate =>
                candidate.Id.Value == recentGame.GameId);
            var identity = CreateMediaIdentity(game, library.Installations);
            if (identity is null)
            {
                continue;
            }

            var path = _mediaResolver!.TryGetCachedPath(
                identity,
                GameMediaAssetType.Hero);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = _mediaResolver.TryGetCachedPath(
                    identity,
                    GameMediaAssetType.Header);
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                paths[recentGame.GameId] = path;
            }
        }

        _recentLandscapeMediaPaths = paths;
        OnPropertyChanged(nameof(RecentlyPlayedGames));
    }

    private void RefreshActiveSessionMedia(
        HomeEditorialActiveSession? activeSession,
        CancellationToken cancellationToken)
    {
        var identity = activeSession?.Game.MediaIdentity;
        var key = (activeSession?.NavigationGameId.Value, activeSession?.StartedAtUtc,
            identity?.Provider, identity?.ProviderGameId, activeSession?.Game.Title);
        if (_activeMediaKey == key)
        {
            return;
        }

        _activeMediaKey = key;
        var version = ++_activeHeroVersion;
        _activeHeroCancellation?.Cancel();
        _activeHeroCancellation?.Dispose();
        _activeHeroCancellation = null;
        SetActiveSessionHeroPath(null);

        if (identity is null)
        {
            return;
        }

        var cached = _mediaResolver!.TryGetCachedPath(identity, GameMediaAssetType.Hero);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            SetActiveSessionHeroPath(cached);
            return;
        }

        _activeHeroCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = LoadActiveSessionHeroObservedAsync(identity, version, _activeHeroCancellation.Token);
    }

    private async Task LoadActiveSessionHeroObservedAsync(
        GameMediaIdentity identity,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = await _mediaResolver!.ResolveAndCacheAsync(
                identity, GameMediaAssetType.Hero, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_disposed && version == _activeHeroVersion)
            {
                SetActiveSessionHeroPath(path);
            }
        }
        catch (OperationCanceledException)
        {
            // A superseded request must not update Home.
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception,
                "Home active-session Hero resolution failed for {GameId}.",
                identity.ProviderGameId);
        }
    }

    private async Task LoadHeroObservedAsync(
        GameMediaIdentity identity,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = await _mediaResolver!.ResolveAndCacheAsync(
                identity, GameMediaAssetType.Hero, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_disposed && version == _heroVersion)
            {
                SetHeroPath(path);
                if (FeaturedGameId is Guid featuredGameId &&
                    !string.IsNullOrWhiteSpace(path))
                {
                    var paths = new Dictionary<Guid, string>(_recentLandscapeMediaPaths)
                    {
                        [featuredGameId] = path
                    };
                    _recentLandscapeMediaPaths = paths;
                    OnPropertyChanged(nameof(RecentlyPlayedGames));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A superseded request must not update Home.
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Home Hero resolution failed for {GameId}.", identity.ProviderGameId);
        }
    }

    private void SetHeroPath(string? path)
    {
        if (HeroPath == path)
        {
            return;
        }

        HeroPath = path;
        OnPropertyChanged(nameof(HeroPath));
        OnPropertyChanged(nameof(HasHero));
    }

    private void SetActiveSessionHeroPath(string? path)
    {
        if (ActiveSessionHeroPath == path)
        {
            return;
        }

        ActiveSessionHeroPath = path;
        OnPropertyChanged(nameof(ActiveSessionHeroPath));
        OnPropertyChanged(nameof(HasActiveSessionHeroMedia));
    }

    public void Dispose()
    {
        _disposed = true;
        _refreshVersion++;
        _heroVersion++;
        _activeHeroVersion++;
        _dormantMediaVersion++;
        _suggestionMediaVersion++;
        _heroCancellation?.Cancel();
        _heroCancellation?.Dispose();
        _activeHeroCancellation?.Cancel();
        _activeHeroCancellation?.Dispose();
        _dormantMediaCancellation?.Cancel();
        _dormantMediaCancellation?.Dispose();
        _suggestionMediaCancellation?.Cancel();
        _suggestionMediaCancellation?.Dispose();
        _attentionMediaCancellation?.Cancel();
        _attentionMediaCancellation?.Dispose();
        _libraryViewModel.PropertyChanged -= LibraryViewModel_OnPropertyChanged;
        _sessionViewModel.PropertyChanged -= SessionViewModel_OnPropertyChanged;
        if (_attentionService is not null) _attentionService.Changed -= AttentionService_OnChanged;
        if (_providerActivityReconciliation is not null) _providerActivityReconciliation.Changed -= ProviderActivity_OnChanged;
        if (_providerInstallUpdates is not null) _providerInstallUpdates.Changed -= ProviderInstallUpdates_OnChanged;
        if (_providerGameMetadataProgress is not null) _providerGameMetadataProgress.ProgressChanged -= ProviderGameMetadataProgress_OnChanged;
        if (_sessionMonitor is not null)
        {
            _sessionMonitor.SnapshotUpdated -= SessionMonitor_OnSnapshotUpdated;
        }
        _dormantSelectionGate.Dispose();
    }

    private async void LibraryViewModel_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.Items))
        {
            OnPropertyChanged(nameof(LibraryGameCount));
            OnPropertyChanged(nameof(RecentlyPlayedGames));
            OnPropertyChanged(nameof(HasRecentlyPlayedGames));
            OnPropertyChanged(nameof(HasNoRecentlyPlayedGames));
            if (_mediaStarted)
            {
                await RefreshFeaturedGameAsync(CancellationToken.None);
            }
        }
    }

    private async void SessionViewModel_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessionViewModel.ActiveSessions):
                OnPropertyChanged(nameof(ActiveSessions));
                break;
            case nameof(SessionViewModel.RecentSessions):
                OnPropertyChanged(nameof(RecentSessions));
                OnPropertyChanged(nameof(RecentlyPlayedGames));
                OnPropertyChanged(nameof(HasRecentlyPlayedGames));
                OnPropertyChanged(nameof(HasNoRecentlyPlayedGames));
                break;
            case nameof(SessionViewModel.HasRecentSessions):
                OnPropertyChanged(nameof(HasRecentActivity));
                break;
        }

        if (_mediaStarted && e.PropertyName is nameof(SessionViewModel.ActiveSessions)
            or nameof(SessionViewModel.RecentSessions))
        {
            await RefreshFeaturedGameAsync(CancellationToken.None);
        }
    }

    private void SessionMonitor_OnSnapshotUpdated(SessionRuntimeSnapshot snapshot)
    {
        if (_disposed) return;
        var dispatcher = _uiDispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                _ = dispatcher.BeginInvoke(() => SessionMonitor_OnSnapshotUpdated(snapshot));
            return;
        }

        var currentIds = snapshot.ActiveSessions.Select(session => session.SessionId).ToHashSet();
        var completedSession = _activeSessionIds.Except(currentIds).Any();
        _activeSessionIds = currentIds;
        if (completedSession)
        {
            _ = RefreshRecentActivityObservedAsync();
            _ = RefreshWeeklySummaryAsync(CancellationToken.None);
        }
    }

    private async Task RefreshRecentActivityObservedAsync()
    {
        try
        {
            await _sessionViewModel.RefreshRecentSessionsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Home recent activity refresh failed.");
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record HomeGamesDuMomentViewModel(Guid GameId, string GameTitle, string? LandscapeMediaPath)
{
    public double CardWidth { get; init; } = 275;
    public bool HasLandscapeMedia => !string.IsNullOrWhiteSpace(LandscapeMediaPath);
    public bool CanMoveLeft { get; init; }
    public bool CanMoveRight { get; init; }
}

public sealed record HomeAttentionItemViewModel(
    AttentionItem Source,
    string DisplayTitle,
    string StatusText,
    string? MediaPath,
    GameMediaIdentity? Identity)
{
    public bool HasMedia => !string.IsNullOrWhiteSpace(MediaPath);
}

public sealed record HomeRecentlyPlayedGameViewModel(
    Guid GameId,
    string GameTitle,
    string StartedAtLabel,
    string DurationLabel,
    LibraryItemViewModel? LibraryItem,
    string? LandscapeMediaPath)
{
    public bool HasLandscapeMedia =>
        !string.IsNullOrWhiteSpace(LandscapeMediaPath);

    public bool HasProvider =>
        !string.IsNullOrWhiteSpace(LibraryItem?.ProviderLabel);

    public string? ProviderLabel =>
        HasProvider ? LibraryItem!.ProviderLabel : null;

    public bool HasInstalledSize =>
        LibraryItem?.InstalledSizeBytes is > 0;

    public string? InstalledSizeLabel =>
        HasInstalledSize ? LibraryItem!.InstalledSizeLabel : null;

    public bool HasMeaningfulStatus =>
        LibraryItem?.SteamState is SteamUpdateState.UpdateAvailable or SteamUpdateState.NewVersionDetected;

    public string? StatusLabel =>
        HasMeaningfulStatus ? LibraryItem!.SteamStatusLabel : null;

    public string DisplayStartedAtLabel =>
        DateTimeOffset.TryParse(
            StartedAtLabel,
            CultureInfo.CurrentCulture,
            DateTimeStyles.AssumeLocal,
            out var timestamp)
            ? GameQuickPanelViewModel.FormatTimestampForDisplay(
                timestamp,
                DateTimeOffset.Now)
            : StartedAtLabel;

    public string DisplayDurationLabel =>
        TryParseSessionDuration(
            DurationLabel,
            out var duration)
            ? GameQuickPanelViewModel.FormatDurationForDisplay(duration)
            : DurationLabel;

    private static bool TryParseSessionDuration(
        string value,
        out TimeSpan duration)
    {
        var parts = value.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
            minutes is >= 0 and < 60 &&
            seconds is >= 0 and < 60)
        {
            duration = new TimeSpan(0, minutes, seconds);
            return true;
        }

        if (parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes) &&
            int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out seconds) &&
            hours >= 0 &&
            minutes is >= 0 and < 60 &&
            seconds is >= 0 and < 60)
        {
            duration = new TimeSpan(hours, minutes, seconds);
            return true;
        }

        duration = default;
        return false;
    }
}
