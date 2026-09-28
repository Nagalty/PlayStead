using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualBasic;

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

    public event EventHandler?
        GameDetailsRequested;

    public static readonly DependencyProperty QuickPanelViewModelProperty =
        DependencyProperty.Register(
            nameof(QuickPanelViewModel),
            typeof(GameQuickPanelViewModel),
            typeof(LibraryView));

    public GameQuickPanelViewModel? QuickPanelViewModel
    {
        get => (GameQuickPanelViewModel?)GetValue(QuickPanelViewModelProperty);
        set => SetValue(QuickPanelViewModelProperty, value);
    }

    public void FocusSearch()
    {
        // Search is owned by the Shell global search field.
    }

    public void RestoreSavedScrollPosition() => RestoreVerticalOffset();

    private void GameCard_OnSelectionRequested(
        object? sender,
        EventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel ||
            sender is not
            PlayStead.UI.Controls.GameCard gameCard ||
            gameCard.DataContext is not
            LibraryItemViewModel item)
        {
            return;
        }

        viewModel.SelectGame(
            item);
    }

    private void GameCard_OnDetailsRequested(
        object? sender,
        LibraryItemViewModel item)
    {
        if (DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        viewModel.SelectGame(item);
        GameDetailsRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void GameCard_OnMediaRequested(
        object? sender,
        LibraryItemViewModel item)
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.EnsureCoverAsync(
                item,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Artwork loading is opportunistic. The fallback remains visible.
        }
        catch
        {
            // Artwork failures are non-fatal. The fallback remains visible.
        }
    }

    private void CloseQuickPanelButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel)
        {
            return;
        }

        viewModel.ClearSelection();
    }

    private void OpenGameDetailButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not
            LibraryViewModel viewModel ||
            viewModel.SelectedItem is null)
        {
            return;
        }

        GameDetailsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

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

    private void InstalledFilterButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
            viewModel.SetQuickFilter(LibraryQuickFilter.Installed);
    }

    private void AttentionFilterButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
            viewModel.SetQuickFilter(LibraryQuickFilter.Attention);
    }

    private void ModdedFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
            viewModel.SetQuickFilter(LibraryQuickFilter.Modded);
    }

    private void AdvancedFiltersButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
            viewModel.ToggleAdvancedFilters();
    }

    private void ResetAdvancedFiltersButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
            viewModel.ResetAdvancedFilters();
    }

    private void AdvancedFiltersPopup_OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
            viewModel.CloseAdvancedFilters();
    }

    private async void CreateCollectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel)
            return;
        var name = Interaction.InputBox("Nom de la collection", "Nouvelle collection", "");
        if (string.IsNullOrWhiteSpace(name))
            return;
        try { await viewModel.CreateCollectionAsync(name, CancellationToken.None); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Collection", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void RenameCollectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel || sender is not Button { Tag: Guid id })
            return;
        var current = viewModel.CollectionOptions.FirstOrDefault(option => option.Id == id)?.Name ?? string.Empty;
        var name = Interaction.InputBox("Nouveau nom", "Renommer la collection", current);
        if (string.IsNullOrWhiteSpace(name))
            return;
        try { await viewModel.RenameCollectionAsync(id, name, CancellationToken.None); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Collection", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void DeleteCollectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel || sender is not Button { Tag: Guid id })
            return;
        try { await viewModel.DeleteCollectionAsync(id, CancellationToken.None); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Collection", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void LibrarySortComboBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 ||
            DataContext is not LibraryViewModel viewModel ||
            e.AddedItems[0] is not ComboBoxItem { Tag: string sortKey })
        {
            return;
        }

        viewModel.SetSortKey(sortKey);
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
