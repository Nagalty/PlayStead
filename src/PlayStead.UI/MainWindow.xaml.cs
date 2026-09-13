using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;
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

    public event EventHandler? RescanRequested;

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

            case AppRoute.Home:
            case AppRoute.Attention:
            case AppRoute.Settings:
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
