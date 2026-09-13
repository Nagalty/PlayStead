using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;
using PlayStead.UI.State;
using PlayStead.UI.Tray;

namespace PlayStead.UI;

public partial class MainWindow : Window
{
    private static readonly Brush ActiveNavBrush =
        CreateFrozenBrush(
            0xC9,
            0x82,
            0x4B);

    private static readonly Brush InactiveNavBrush =
        CreateFrozenBrush(
            0xA8,
            0xAD,
            0xB5);

    private readonly WindowPlacementService? _windowPlacementService;
    private readonly WindowClosePolicy? _windowClosePolicy;
    private readonly LibraryView _libraryView;

    private SessionViewModel? _sessionViewModel;
    private SessionsView? _sessionsView;

    public MainWindow()
    {
        InitializeComponent();

        _libraryView =
            MainContent.Content as LibraryView
            ?? throw new InvalidOperationException(
                "The main window must start with LibraryView.");

        UpdateNavigationState(
            sessionsSelected: false);
    }

    public MainWindow(
        LibraryViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    public MainWindow(
        LibraryViewModel viewModel,
        WindowPlacementService windowPlacementService)
        : this(viewModel)
    {
        ArgumentNullException.ThrowIfNull(windowPlacementService);

        _windowPlacementService = windowPlacementService;

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
        ArgumentNullException.ThrowIfNull(windowClosePolicy);

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
        ArgumentNullException.ThrowIfNull(sessionViewModel);

        _sessionViewModel =
            sessionViewModel;
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

        base.OnClosing(e);
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

        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;

        WindowState =
            placement.IsMaximized
                ? WindowState.Maximized
                : WindowState.Normal;
    }

    private WindowPlacementState
        CaptureWindowPlacement()
    {
        var normalBounds =
            WindowState == WindowState.Normal
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

    private void LibraryNavButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        MainContent.Content =
            _libraryView;

        UpdateNavigationState(
            sessionsSelected: false);
    }

    private void SessionsNavButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        _sessionsView ??=
            new SessionsView();

        _sessionsView.DataContext =
            _sessionViewModel;

        MainContent.Content =
            _sessionsView;

        UpdateNavigationState(
            sessionsSelected: true);
    }

    private void UpdateNavigationState(
        bool sessionsSelected)
    {
        LibraryNavIndicator.Visibility =
            sessionsSelected
                ? Visibility.Collapsed
                : Visibility.Visible;

        SessionsNavIndicator.Visibility =
            sessionsSelected
                ? Visibility.Visible
                : Visibility.Collapsed;

        LibraryNavText.Foreground =
            sessionsSelected
                ? InactiveNavBrush
                : ActiveNavBrush;

        SessionsNavText.Foreground =
            sessionsSelected
                ? ActiveNavBrush
                : InactiveNavBrush;
    }

    private static Brush CreateFrozenBrush(
        byte red,
        byte green,
        byte blue)
    {
        var brush =
            new SolidColorBrush(
                Color.FromRgb(
                    red,
                    green,
                    blue));

        brush.Freeze();

        return brush;
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
