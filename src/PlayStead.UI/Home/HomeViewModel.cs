using System.ComponentModel;
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
    private readonly LibraryViewModel _libraryViewModel;
    private readonly SessionViewModel _sessionViewModel;
    private readonly NavigationService _navigationService;
    private readonly ILibraryStore? _libraryStore;
    private readonly ISessionStore? _sessionStore;
    private readonly SessionMonitor? _sessionMonitor;
    private readonly IGameMediaResolver? _mediaResolver;
    private readonly ILogger<HomeViewModel>? _logger;
    private CancellationTokenSource? _heroCancellation;
    private (Guid? GameId, ProviderKind? Provider, string? Id, string? Title) _mediaKey;
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
        ILogger<HomeViewModel> logger)
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

        NavigateLibraryCommand =
            new RelayCommand(
                () => _navigationService.Navigate(
                    new NavigationRequest(AppRoute.Library)));

        NavigateSessionsCommand =
            new RelayCommand(
                () => _navigationService.Navigate(
                    new NavigationRequest(AppRoute.Sessions)));

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

    public bool HasRecentActivity =>
        _sessionViewModel.HasRecentSessions;

    public ICommand NavigateLibraryCommand { get; }

    public ICommand NavigateSessionsCommand { get; }

    public Guid? FeaturedGameId { get; private set; }
    public string? FeaturedGameTitle { get; private set; }
    public string? HeroPath { get; private set; }
    public bool HasHero => !string.IsNullOrWhiteSpace(HeroPath);

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
            var recent = active.Count == 0
                ? await _sessionStore!.GetRecentAsync(int.MaxValue, cancellationToken)
                : Array.Empty<GameSession>();
            var selected = SelectFeaturedSession(active, recent);
            var library = await _libraryStore!.LoadSnapshotAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (_disposed || refreshVersion != _refreshVersion)
            {
                return;
            }

            var game = library.Games.FirstOrDefault(game => game.Id.Value == selected?.GameId);
            var identity = CreateMediaIdentity(game, library.Installations);
            var key = (selected?.GameId, identity?.Provider, identity?.ProviderGameId, game?.Title);
            if (_mediaKey == key)
            {
                return;
            }

            _mediaKey = key;
            var heroVersion = ++_heroVersion;
            _heroCancellation?.Cancel();
            _heroCancellation?.Dispose();
            _heroCancellation = null;
            FeaturedGameId = selected?.GameId;
            FeaturedGameTitle = game?.Title;
            SetHeroPath(null);
            OnPropertyChanged(nameof(FeaturedGameId));
            OnPropertyChanged(nameof(FeaturedGameTitle));

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

    private static GameSession? SelectFeaturedSession(
        IReadOnlyList<GameSession> active,
        IReadOnlyList<GameSession> recent) =>
        active.Where(session => session.State == SessionState.Active)
            .OrderByDescending(session => session.ObservedStartedAtUtc)
            .ThenBy(session => session.SessionId)
            .FirstOrDefault()
        ?? recent.Where(session => session.State != SessionState.Active && session.ObservedEndedAtUtc.HasValue)
            .OrderByDescending(session => session.ObservedEndedAtUtc)
            .ThenBy(session => session.SessionId)
            .FirstOrDefault();

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
