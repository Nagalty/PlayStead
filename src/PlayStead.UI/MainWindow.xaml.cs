using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Sessions;
using PlayStead.UI.Home;
using PlayStead.UI.Attention;
using PlayStead.UI.Library;
using PlayStead.UI.Notifications;
using PlayStead.UI.Launching;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Shell;
using PlayStead.UI.State;
using PlayStead.UI.Tray;

namespace PlayStead.UI;

public partial class MainWindow : Window
{
    private readonly WindowPlacementService? _windowPlacementService;
    private readonly WindowClosePolicy? _windowClosePolicy;
    private readonly LibraryView _libraryView;
    private readonly NavigationService _navigationService;
    private readonly ShellViewModel _shellViewModel;

    private SessionViewModel? _sessionViewModel;
    private SessionsView? _sessionsView;
    private readonly SettingsViewModel? _settingsViewModel;
    private SettingsView? _settingsView;
    private readonly UiMotionPreferenceCoordinator? _uiMotionPreferenceCoordinator;
    private readonly HomeViewModel? _homeViewModel;
    private HomeView? _homeView;
    private readonly GameLaunchService? _gameLaunchService;
    private readonly IGameMediaResolver? _gameMediaResolver;
    private readonly ILocalGameMediaResolver? _localGameMediaResolver;
    private readonly AttentionViewModel? _attentionViewModel;
    private AttentionView? _attentionView;
    private readonly ISessionStore? _sessionStore;
    private readonly ISessionCorrectionStore? _sessionCorrectionStore;
    private readonly SessionCorrectionPolicy? _sessionCorrectionPolicy;
    private readonly NotificationCenterViewModel? _notificationCenterViewModel;
    private readonly SessionMonitor? _sessionMonitor;

    public MainWindow()
        : this(
            new NavigationService(),
            shellViewModel: null)
    {
    }

    private MainWindow(
        NavigationService navigationService,
        ShellViewModel? shellViewModel)
    {
        ArgumentNullException.ThrowIfNull(
            navigationService);

        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeHelper.ApplyDarkTitleBar(this);

        _libraryView =
            MainContent.Content as LibraryView
            ?? throw new InvalidOperationException(
                "The main window must declare its LibraryView shell content.");

        _libraryView.GameDetailsRequested +=
            LibraryView_OnGameDetailsRequested;

        _navigationService =
            navigationService;

        _shellViewModel =
            shellViewModel
            ?? new ShellViewModel(
                _navigationService);

        PrimaryNavigation.DataContext =
            _shellViewModel;

        _shellViewModel.SearchRequested +=
            ShellViewModel_OnSearchRequested;

        _navigationService.Changed +=
            NavigationService_OnChanged;

        PreviewKeyDown +=
            MainWindow_OnPreviewKeyDown;

        ApplyCurrentRoute();
    }

    public MainWindow(
        LibraryViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        DataContext =
            viewModel;
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService)
        : this(
            viewModel)
    {
        ArgumentNullException.ThrowIfNull(
            windowPlacementService);

        _windowPlacementService =
            windowPlacementService;

        RestoreWindowPlacement();
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy)
        : this(
            viewModel,
            windowPlacementService)
    {
        ArgumentNullException.ThrowIfNull(
            windowClosePolicy);

        _windowClosePolicy =
            windowClosePolicy;
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel)
        : this(
            viewModel,
            windowPlacementService,
            windowClosePolicy)
    {
        ArgumentNullException.ThrowIfNull(
            sessionViewModel);

        _sessionViewModel =
            sessionViewModel;
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel)
        : this(
            navigationService,
            shellViewModel)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);
        ArgumentNullException.ThrowIfNull(
            windowPlacementService);
        ArgumentNullException.ThrowIfNull(
            windowClosePolicy);
        ArgumentNullException.ThrowIfNull(
            sessionViewModel);

        DataContext =
            viewModel;

        _windowPlacementService =
            windowPlacementService;

        _windowClosePolicy =
            windowClosePolicy;

        _sessionViewModel =
            sessionViewModel;

        RestoreWindowPlacement();
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel)
        : this(
            viewModel,
            windowPlacementService,
            windowClosePolicy,
            sessionViewModel,
            navigationService,
            shellViewModel)
    {
        ArgumentNullException.ThrowIfNull(settingsViewModel);

        _settingsViewModel = settingsViewModel;
        ApplyCurrentRoute();
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel,
        UiMotionController uiMotionController)
        : this(
            viewModel,
            windowPlacementService,
            windowClosePolicy,
            sessionViewModel,
            navigationService,
            shellViewModel,
            settingsViewModel)
    {
        ArgumentNullException.ThrowIfNull(uiMotionController);

        if (Application.Current is not null)
        {
            _uiMotionPreferenceCoordinator =
                new UiMotionPreferenceCoordinator(
                    settingsViewModel,
                    uiMotionController,
                    Application.Current.Resources);

            IsEnabled = false;
            Loaded += MainWindow_OnMotionFirstLoaded;
        }
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel,
        UiMotionController uiMotionController,
        HomeViewModel homeViewModel)
        : this(
            viewModel,
            windowPlacementService,
            windowClosePolicy,
            sessionViewModel,
            navigationService,
            shellViewModel,
            settingsViewModel,
            uiMotionController)
    {
        ArgumentNullException.ThrowIfNull(homeViewModel);

        _homeViewModel = homeViewModel;
        ApplyCurrentRoute();
    }

    public event EventHandler? RescanRequested;

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel,
        UiMotionController uiMotionController,
        HomeViewModel homeViewModel,
        GameLaunchService gameLaunchService)
        : this(viewModel, windowPlacementService, windowClosePolicy,
            sessionViewModel, navigationService, shellViewModel,
            settingsViewModel, uiMotionController, homeViewModel)
    {
        ArgumentNullException.ThrowIfNull(gameLaunchService);
        _gameLaunchService = gameLaunchService;
        viewModel.PropertyChanged += LibraryViewModel_OnPropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= LibraryViewModel_OnPropertyChanged;
        UpdateQuickPanel();
        ApplyCurrentRoute();
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel,
        UiMotionController uiMotionController,
        HomeViewModel homeViewModel,
        GameLaunchService gameLaunchService,
        AttentionViewModel attentionViewModel,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        SessionCorrectionPolicy sessionCorrectionPolicy,
        NotificationCenterViewModel notificationCenterViewModel)
        : this(viewModel, windowPlacementService, windowClosePolicy,
            sessionViewModel, navigationService, shellViewModel,
            settingsViewModel, uiMotionController, homeViewModel, gameLaunchService)
    {
        ArgumentNullException.ThrowIfNull(attentionViewModel);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionPolicy);
        ArgumentNullException.ThrowIfNull(notificationCenterViewModel);

        _attentionViewModel = attentionViewModel;
        _sessionStore = sessionStore;
        _sessionCorrectionStore = sessionCorrectionStore;
        _sessionCorrectionPolicy = sessionCorrectionPolicy;
        _notificationCenterViewModel = notificationCenterViewModel;
        NotificationBellButton.DataContext = notificationCenterViewModel;
        NotificationPanelHost.DataContext = notificationCenterViewModel;

        UpdateQuickPanel();
        ApplyCurrentRoute();
    }

    private void ShellSearchBox_OnKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            PrimaryNavigation.DataContext is ShellViewModel shell)
        {
            shell.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ShellSearchBox_OnKeyboardFocusChanged(
        object sender,
        RoutedEventArgs e)
    {
        ShellSearchPlaceholder.Visibility =
            string.IsNullOrEmpty(ShellSearchBox.Text) &&
            !ShellSearchBox.IsKeyboardFocusWithin
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ShellSearchBox_OnTextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        ShellSearchPlaceholder.Visibility =
            string.IsNullOrEmpty(ShellSearchBox.Text) &&
            !ShellSearchBox.IsKeyboardFocusWithin
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ShellViewModel_OnSearchRequested(
        object? sender,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        _navigationService.Navigate(
            new NavigationRequest(AppRoute.Library));

        if (DataContext is LibraryViewModel library)
        {
            library.SetSearchQuery(query);
        }
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel,
        UiMotionController uiMotionController,
        HomeViewModel homeViewModel,
        GameLaunchService gameLaunchService,
        AttentionViewModel attentionViewModel,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        SessionCorrectionPolicy sessionCorrectionPolicy,
        NotificationCenterViewModel notificationCenterViewModel,
        IGameMediaResolver gameMediaResolver,
        ILocalGameMediaResolver localGameMediaResolver,
        SessionMonitor sessionMonitor)
        : this(viewModel, windowPlacementService, windowClosePolicy,
            sessionViewModel, navigationService, shellViewModel,
            settingsViewModel, uiMotionController, homeViewModel,
            gameLaunchService, attentionViewModel, sessionStore,
            sessionCorrectionStore, sessionCorrectionPolicy,
            notificationCenterViewModel, gameMediaResolver,
            localGameMediaResolver)
    {
        ArgumentNullException.ThrowIfNull(sessionMonitor);
        _sessionMonitor = sessionMonitor;
        ApplyCurrentRoute();
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService,
        WindowClosePolicy windowClosePolicy,
        SessionViewModel sessionViewModel,
        NavigationService navigationService,
        ShellViewModel shellViewModel,
        SettingsViewModel settingsViewModel,
        UiMotionController uiMotionController,
        HomeViewModel homeViewModel,
        GameLaunchService gameLaunchService,
        AttentionViewModel attentionViewModel,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        SessionCorrectionPolicy sessionCorrectionPolicy,
        NotificationCenterViewModel notificationCenterViewModel,
        IGameMediaResolver gameMediaResolver,
        ILocalGameMediaResolver localGameMediaResolver)
        : this(viewModel, windowPlacementService, windowClosePolicy,
            sessionViewModel, navigationService, shellViewModel,
            settingsViewModel, uiMotionController, homeViewModel,
            gameLaunchService, attentionViewModel, sessionStore,
            sessionCorrectionStore, sessionCorrectionPolicy,
            notificationCenterViewModel)
    {
        ArgumentNullException.ThrowIfNull(gameMediaResolver);
        ArgumentNullException.ThrowIfNull(localGameMediaResolver);
        _gameMediaResolver = gameMediaResolver;
        _localGameMediaResolver = localGameMediaResolver;
        ApplyCurrentRoute();
    }

    private GameLaunchViewModel? CreateLaunchModel(
        LibraryViewModel libraryViewModel, GameId gameId) =>
        _gameLaunchService is null
            ? null
            : new GameLaunchViewModel(gameId,
                libraryViewModel.GetLaunchInstallations(gameId), _gameLaunchService);

    private GameQuickPanelViewModel? CreateActivityModel(
        LibraryViewModel libraryViewModel,
        GameId gameId)
    {
        var game =
            libraryViewModel.Items.FirstOrDefault(
                item => item.GameId == gameId);

        if (game is null)
        {
            return null;
        }

        var launch =
            CreateLaunchModel(
                libraryViewModel,
                gameId);

        return _sessionStore is not null &&
            _sessionCorrectionStore is not null &&
            _sessionCorrectionPolicy is not null
                ? new GameQuickPanelViewModel(
                    game,
                    _navigationService,
                    launch,
                    _sessionStore,
                    _sessionCorrectionStore,
                    _sessionCorrectionPolicy)
                : new GameQuickPanelViewModel(
                    game,
                    _navigationService,
                    launch);
    }

    private void LibraryViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.SelectedItem))
        {
            UpdateQuickPanel();
        }
    }

    private void UpdateQuickPanel()
    {
        if (DataContext is not LibraryViewModel viewModel ||
            viewModel.SelectedItem is not { } game)
        {
            _libraryView.QuickPanelViewModel =
                null;

            return;
        }

        var quickPanel =
            CreateActivityModel(
                viewModel,
                game.GameId)!;

        _libraryView.QuickPanelViewModel =
            quickPanel;

        _ =
            quickPanel.LoadSessionSummaryAsync(
                CancellationToken.None);
    }

    private async void MainWindow_OnMotionFirstLoaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= MainWindow_OnMotionFirstLoaded;

        try
        {
            if (_uiMotionPreferenceCoordinator is not null)
            {
                await _uiMotionPreferenceCoordinator.InitializeAsync(CancellationToken.None);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                "Impossible de charger les préférences d’animation. Vérifiez l’accès au dossier de données locales.",
                "Paramètres",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (!e.Cancel &&
            _windowPlacementService is not null)
        {
            var state =
                CaptureWindowPlacement();

            Task.Run(
                    () => _windowPlacementService.SaveAsync(
                        state,
                        CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        if (!e.Cancel &&
            _windowClosePolicy is not null &&
            !_windowClosePolicy.IsExitRequested)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(
            e);
    }

    private void RestoreWindowPlacement()
    {
        if (_windowPlacementService is null)
        {
            return;
        }

        var workArea =
            SystemParameters.WorkArea;

        var currentWorkAreas =
            new[]
            {
                new WindowWorkArea(
                    workArea.Left,
                    workArea.Top,
                    workArea.Width,
                    workArea.Height,
                    IsPrimary: true)
            };

        var placement =
            Task.Run(
                    () => _windowPlacementService.LoadAsync(
                        currentWorkAreas,
                        CancellationToken.None))
                .GetAwaiter()
                .GetResult();

        if (placement is null)
        {
            return;
        }

        WindowStartupLocation =
            WindowStartupLocation.Manual;

        Left =
            placement.Left;
        Top =
            placement.Top;
        Width =
            placement.Width;
        Height =
            placement.Height;

        WindowState =
            placement.IsMaximized
                ? WindowState.Maximized
                : WindowState.Normal;
    }

    private WindowPlacementState
        CaptureWindowPlacement()
    {
        var normalBounds =
            WindowState ==
            WindowState.Normal
                ? new Rect(
                    Left,
                    Top,
                    Width,
                    Height)
                : RestoreBounds;

        return new WindowPlacementState(
            normalBounds.Left,
            normalBounds.Top,
            normalBounds.Width,
            normalBounds.Height,
            IsMaximized:
                WindowState ==
                WindowState.Maximized);
    }

    private void NavigationService_OnChanged(
        object? sender,
        EventArgs e)
    {
        ApplyCurrentRoute();
    }

    private void ApplyCurrentRoute()
    {
        switch (_navigationService.CurrentRoute)
        {
            case AppRoute.Library:
                _libraryView.DataContext =
                    DataContext;

                MainContent.Content =
                    _libraryView;
                break;

            case AppRoute.Sessions:
                _sessionsView ??=
                    new SessionsView();

                _sessionsView.DataContext =
                    _sessionViewModel;

                MainContent.Content =
                    _sessionsView;
                break;

            case AppRoute.Settings:
                if (_settingsViewModel is null)
                {
                    break;
                }

                if (_settingsView is null)
                {
                    _settingsView =
                        new SettingsView(
                            _settingsViewModel);
                    _settingsView.Loaded += SettingsView_OnFirstLoaded;
                }

                MainContent.Content = _settingsView;
                break;

            case AppRoute.Home:
                if (_homeViewModel is null)
                {
                    break;
                }

                _homeView ??=
                    new HomeView(
                        _homeViewModel);

                MainContent.Content =
                    _homeView;
                break;

            case AppRoute.GameDetail:
                if (DataContext is not
                        LibraryViewModel libraryViewModel ||
                    _navigationService.CurrentParameter is not
                        GameId gameId)
                {
                    MainContent.Content =
                        null;
                    break;
                }

                var game =
                    libraryViewModel.Items.FirstOrDefault(
                        item =>
                            item.GameId == gameId);

                if (game is null)
                {
                    MainContent.Content =
                        null;
                    break;
                }

                var activity = CreateActivityModel(libraryViewModel, gameId);
                var gameDetailViewModel = _sessionMonitor is null
                    ? new GameDetailViewModel(
                        game,
                        CreateLaunchModel(libraryViewModel, gameId),
                        activity,
                        CreateHeroPath(libraryViewModel, game))
                    : new GameDetailViewModel(
                        game,
                        CreateLaunchModel(libraryViewModel, gameId),
                        activity,
                        CreateHeroPath(libraryViewModel, game),
                        _sessionMonitor,
                        () => activity?.LoadSessionSummaryAsync(CancellationToken.None)
                            ?? Task.CompletedTask);

                MainContent.Content =
                    new GameDetailView(
                        gameDetailViewModel);
                break;

            case AppRoute.Attention:
                if (_attentionViewModel is null)
                {
                    break;
                }

                _attentionView ??= new AttentionView(_attentionViewModel);
                MainContent.Content = _attentionView;
                break;

            case AppRoute.SessionDetail:
                MainContent.Content =
                    null;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported route: {_navigationService.CurrentRoute}.");
        }
    }

    private string? CreateHeroPath(
        LibraryViewModel libraryViewModel,
        LibraryItemViewModel game)
    {
        if (_gameMediaResolver is null)
        {
            return null;
        }

        var installation =
            libraryViewModel.GetDefaultLaunchInstallation(game.GameId);

        if (installation is null)
        {
            return null;
        }

        try
        {
            var identity = new GameMediaIdentity(
                installation.Provider,
                installation.ExternalId,
                game.Title);

            return _gameMediaResolver.TryGetCachedPath(
                       identity,
                       GameMediaAssetType.Hero)
                ?? _localGameMediaResolver?.TryGetPath(
                    identity,
                    GameMediaAssetType.Hero);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async void SettingsView_OnFirstLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_settingsView is null || _settingsViewModel is null)
        {
            return;
        }

        _settingsView.Loaded -= SettingsView_OnFirstLoaded;

        if (_uiMotionPreferenceCoordinator is not null)
        {
            return;
        }

        _settingsView.IsEnabled = false;

        try
        {
            await _settingsViewModel.LoadAsync(CancellationToken.None);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                "Impossible de lire les préférences. Vérifiez l’accès au dossier de données locales.",
                "Paramètres",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _settingsView.IsEnabled = true;
        }
    }

    private void MainWindow_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.K &&
            (Keyboard.Modifiers &
             ModifierKeys.Control) ==
            ModifierKeys.Control)
        {
            _navigationService.Navigate(
                new NavigationRequest(
                    AppRoute.Library));

            ShellSearchBox.Focus();
            ShellSearchBox.SelectAll();

            e.Handled =
                true;

            return;
        }

        if (e.Key != Key.Left ||
            (Keyboard.Modifiers &
             ModifierKeys.Alt) !=
            ModifierKeys.Alt)
        {
            return;
        }

        if (!_shellViewModel
                .GoBackCommand
                .CanExecute(
                    null))
        {
            return;
        }

        _shellViewModel
            .GoBackCommand
            .Execute(
                null);

        e.Handled =
            true;
    }

    private void LibraryView_OnGameDetailsRequested(
        object? sender,
        EventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel ||
            viewModel.SelectedItem is null)
        {
            return;
        }

        var quickPanelViewModel =
            new GameQuickPanelViewModel(
                viewModel.SelectedItem,
                _navigationService,
                CreateLaunchModel(viewModel, viewModel.SelectedItem.GameId));

        quickPanelViewModel.OpenDetails();
    }

    private void LibraryView_OnRescanRequested(
        object? sender,
        EventArgs e)
    {
        RescanRequested?.Invoke(
            this,
            EventArgs.Empty);
    }
}
