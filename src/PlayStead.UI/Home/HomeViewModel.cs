using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.UI.Launching;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;

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
    private CancellationTokenSource? _heroCancellation;
    private IReadOnlyDictionary<Guid, string> _recentLandscapeMediaPaths =
        new Dictionary<Guid, string>();
    private (Guid? GameId, Guid? SessionId, DateTimeOffset? StartedAtUtc,
        ProviderKind? Provider, string? Id, string? Title) _mediaKey;
    private long _refreshVersion;
    private long _heroVersion;
    private bool _mediaStarted;
    private bool _disposed;
    private readonly Dispatcher? _uiDispatcher;
    private HashSet<Guid> _activeSessionIds = [];

    public HomeViewModel(
        LibraryViewModel libraryViewModel,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ILibraryStore libraryStore,
        ISessionStore sessionStore,
        SessionMonitor sessionMonitor,
        IGameMediaResolver mediaResolver,
        ILogger<HomeViewModel> logger,
        GameLaunchService? gameLaunchService = null)
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
        _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.FromThread(Thread.CurrentThread);
        _activeSessionIds = sessionMonitor.LatestSnapshot?.ActiveSessions
            .Select(session => session.SessionId).ToHashSet() ?? [];
        sessionMonitor.SnapshotUpdated += SessionMonitor_OnSnapshotUpdated;
    }

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
        PlayRecentlyPlayedCommand = new RelayCommand<Guid>(
            PlayRecentlyPlayed,
            CanPlayRecentlyPlayed);

        _libraryViewModel.PropertyChanged += LibraryViewModel_OnPropertyChanged;
        _sessionViewModel.PropertyChanged += SessionViewModel_OnPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

            foreach (var session in RecentSessions)
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

    public bool HasRecentlyPlayedGames =>
        RecentSessions.Count > 0;

    public bool HasRecentActivity =>
        _sessionViewModel.HasRecentSessions;

    public ICommand NavigateLibraryCommand { get; }

    public ICommand NavigateSessionsCommand { get; }

    public ICommand PlayRecentlyPlayedCommand { get; }

    public ICommand OpenRecentlyPlayedDetailsCommand { get; }

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
    public bool HasActiveSessionHero { get; private set; }
    public string? ActiveSessionStartedAtLabel { get; private set; }
    public string HeroEyebrow => HasActiveSessionHero
        ? "L’aventure continue"
        : "Aucune aventure en cours";
    public string HeroTitle => HasActiveSessionHero
        ? FeaturedGameTitle ?? string.Empty
        : "Prêt à replonger ?";
    public string HeroSupportingText => HasActiveSessionHero
        ? $"En cours depuis {ActiveSessionStartedAtLabel}"
        : "Lance un jeu, PlayStead s’occupe du reste.";
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
            var selected = SelectActiveSession(active);
            var library = await _libraryStore!.LoadSnapshotAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (_disposed || refreshVersion != _refreshVersion)
            {
                return;
            }

            RefreshRecentlyPlayedLandscapeMediaPaths(library);

            var game = library.Games.FirstOrDefault(game => game.Id.Value == selected?.GameId);
            var identity = CreateMediaIdentity(game, library.Installations);
            UpdateHeroSession(selected, game?.Title);
            var key = (selected?.GameId, selected?.SessionId, selected?.ObservedStartedAtUtc,
                identity?.Provider, identity?.ProviderGameId, game?.Title);
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

    private static GameSession? SelectActiveSession(
        IReadOnlyList<GameSession> active) =>
        active.Where(session => session.State == SessionState.Active)
            .OrderByDescending(session => session.ObservedStartedAtUtc)
            .ThenBy(session => session.SessionId)
            .FirstOrDefault();

    private void UpdateHeroSession(GameSession? session, string? title)
    {
        var isActive = session is not null;
        if (HasActiveSessionHero != isActive)
        {
            HasActiveSessionHero = isActive;
            OnPropertyChanged(nameof(HasActiveSessionHero));
            OnPropertyChanged(nameof(HeroEyebrow));
            OnPropertyChanged(nameof(HeroTitle));
            OnPropertyChanged(nameof(HeroSupportingText));
        }

        if (FeaturedGameId != session?.GameId)
        {
            FeaturedGameId = session?.GameId;
            OnPropertyChanged(nameof(FeaturedGameId));
        }

        if (FeaturedGameTitle != title)
        {
            FeaturedGameTitle = title;
            OnPropertyChanged(nameof(FeaturedGameTitle));
            OnPropertyChanged(nameof(HeroTitle));
        }

        var startedAtLabel = session?.ObservedStartedAtUtc
            .ToLocalTime()
            .ToString("HH:mm", CultureInfo.CurrentCulture);
        if (ActiveSessionStartedAtLabel != startedAtLabel)
        {
            ActiveSessionStartedAtLabel = startedAtLabel;
            OnPropertyChanged(nameof(ActiveSessionStartedAtLabel));
            OnPropertyChanged(nameof(HeroSupportingText));
        }
    }

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

        try
        {
            return new GameMediaIdentity(installation.Provider, installation.ExternalId, game.Title);
        }
        catch (ArgumentException)
        {
            return null;
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

    public void Dispose()
    {
        _disposed = true;
        _refreshVersion++;
        _heroVersion++;
        _heroCancellation?.Cancel();
        _heroCancellation?.Dispose();
        _libraryViewModel.PropertyChanged -= LibraryViewModel_OnPropertyChanged;
        _sessionViewModel.PropertyChanged -= SessionViewModel_OnPropertyChanged;
        if (_sessionMonitor is not null)
        {
            _sessionMonitor.SnapshotUpdated -= SessionMonitor_OnSnapshotUpdated;
        }
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
