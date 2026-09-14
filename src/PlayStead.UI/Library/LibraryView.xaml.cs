using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PlayStead.UI.Library;

public partial class LibraryView :
    UserControl
{
    private const double HorizontalContentMargin =
        56d;

    private const double GridCardWidth =
        260d;

    private const double GridColumnGap =
        12d;

    private readonly LibraryViewStateAdapter
        _viewStateAdapter =
            new();

    public LibraryView()
    {
        InitializeComponent();

        Loaded +=
            LibraryView_OnLoaded;

        DataContextChanged +=
            LibraryView_OnDataContextChanged;
    }

    public event EventHandler?
        RescanRequested;

    private void RescanButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        RescanRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private async void VerifySteamButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        await viewModel.VerifySteamAsync(
            CancellationToken.None);
    }

    private async void GridModeButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        CaptureCurrentVerticalOffset(
            viewModel);

        viewModel.SetViewMode(
            LibraryViewMode.Grid);

        await viewModel.SaveUiPreferencesAsync(
            CancellationToken.None);

        RestoreVerticalOffset();
    }

    private async void ListModeButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        CaptureCurrentVerticalOffset(
            viewModel);

        viewModel.SetViewMode(
            LibraryViewMode.List);

        await viewModel.SaveUiPreferencesAsync(
            CancellationToken.None);

        RestoreVerticalOffset();
    }

    private void LibraryView_OnSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        var availableWidth =
            Math.Max(
                0d,
                e.NewSize.Width -
                HorizontalContentMargin);

        var columnCount =
            Math.Max(
                1,
                (int)Math.Floor(
                    (availableWidth +
                     GridColumnGap) /
                    (GridCardWidth +
                     GridColumnGap)));

        viewModel.SetGridColumnCount(
            columnCount);
    }

    private void LibraryView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        AttachScrollHandlers();
        RestoreVerticalOffset();
    }

    private void LibraryView_OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        AttachScrollHandlers();
        RestoreVerticalOffset();
    }

    private void AttachScrollHandlers()
    {
        var gridScrollViewer =
            GetGridScrollViewer();

        if (gridScrollViewer is not null)
        {
            gridScrollViewer.ScrollChanged -=
                LibraryScrollViewer_OnScrollChanged;

            gridScrollViewer.ScrollChanged +=
                LibraryScrollViewer_OnScrollChanged;
        }

        var listScrollViewer =
            GetListScrollViewer();

        if (listScrollViewer is not null)
        {
            listScrollViewer.ScrollChanged -=
                LibraryScrollViewer_OnScrollChanged;

            listScrollViewer.ScrollChanged +=
                LibraryScrollViewer_OnScrollChanged;
        }
    }

    private void LibraryScrollViewer_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel ||
            sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var isActiveViewer =
            (viewModel.IsGridMode &&
             ReferenceEquals(
                 scrollViewer,
                 GetGridScrollViewer())) ||
            (viewModel.IsListMode &&
             ReferenceEquals(
                 scrollViewer,
                 GetListScrollViewer()));

        if (!isActiveViewer)
        {
            return;
        }

        _viewStateAdapter.CaptureVerticalOffset(
            viewModel,
            scrollViewer.VerticalOffset);
    }

    private void CaptureCurrentVerticalOffset(
        LibraryViewModel viewModel)
    {
        var activeScrollViewer =
            viewModel.IsGridMode
                ? GetGridScrollViewer()
                : GetListScrollViewer();

        if (activeScrollViewer is null)
        {
            return;
        }

        _viewStateAdapter.CaptureVerticalOffset(
            viewModel,
            activeScrollViewer.VerticalOffset);
    }

    private void RestoreVerticalOffset()
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                var verticalOffset =
                    _viewStateAdapter
                        .GetRestoreVerticalOffset(
                            viewModel);

                var activeScrollViewer =
                    viewModel.IsGridMode
                        ? GetGridScrollViewer()
                        : GetListScrollViewer();

                activeScrollViewer
                    ?.ScrollToVerticalOffset(
                        verticalOffset);
            },
            DispatcherPriority.Loaded);
    }

    private ScrollViewer?
        GetGridScrollViewer()
    {
        GameGridRows.ApplyTemplate();

        return GameGridRows.Template.FindName(
                   "GridScrollViewer",
                   GameGridRows)
               as ScrollViewer;
    }

    private ScrollViewer?
        GetListScrollViewer()
    {
        GameList.ApplyTemplate();

        return GameList.Template.FindName(
                   "ListScrollViewer",
                   GameList)
               as ScrollViewer;
    }
}
