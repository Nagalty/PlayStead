using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PlayStead.UI.Home;
using PlayStead.UI.Library;
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

        _libraryView =
            MainContent.Content as LibraryView
            ?? throw new InvalidOperationException(
                "The main window must declare its LibraryView shell content.");

        _navigationService =
            navigationService;

        _shellViewModel =
            shellViewModel
            ?? new ShellViewModel(
                _navigationService);

        PrimaryNavigation.DataContext =
            _shellViewModel;

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

            case AppRoute.Attention:
            case AppRoute.GameDetail:
            case AppRoute.SessionDetail:
                MainContent.Content =
                    null;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported route: {_navigationService.CurrentRoute}.");
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

    private void LibraryView_OnRescanRequested(
        object? sender,
        EventArgs e)
    {
        RescanRequested?.Invoke(
            this,
            EventArgs.Empty);
    }
}
