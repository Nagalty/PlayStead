using System.ComponentModel;
using System.Windows;
using PlayStead.UI.Library;
using PlayStead.UI.State;

namespace PlayStead.UI;

public partial class MainWindow : Window
{
    private readonly WindowPlacementService? _windowPlacementService;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(LibraryViewModel viewModel)
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

    public event EventHandler? RescanRequested;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!e.Cancel &&
            _windowPlacementService is not null)
        {
            var state = CaptureWindowPlacement();

            Task.Run(
                    () => _windowPlacementService.SaveAsync(
                        state,
                        CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        base.OnClosing(e);
    }

    private void RestoreWindowPlacement()
    {
        if (_windowPlacementService is null)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;

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

        var placement = Task.Run(
                () => _windowPlacementService.LoadAsync(
                    currentWorkAreas,
                    CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        if (placement is null)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;

        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;

        WindowState = placement.IsMaximized
            ? WindowState.Maximized
            : WindowState.Normal;
    }

    private WindowPlacementState CaptureWindowPlacement()
    {
        var normalBounds = WindowState == WindowState.Normal
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
            IsMaximized: WindowState == WindowState.Maximized);
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
