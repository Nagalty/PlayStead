using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;
using PlayStead.UI.Library;

namespace PlayStead.UI.Controls;

public partial class GameCard :
    UserControl
{
    private bool _mediaRequested;

    public GameCard()
    {
        InitializeComponent();
        Loaded += GameCard_OnLoaded;
        Unloaded += GameCard_OnUnloaded;
    }

    public event EventHandler<LibraryItemViewModel>?
        MediaRequested;

    public event EventHandler?
        SelectionRequested;

    public event EventHandler<LibraryItemViewModel>?
        DetailsRequested;

    private void GameCard_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_mediaRequested ||
            DataContext is not LibraryItemViewModel item ||
            item.HasCover)
        {
            return;
        }

        _mediaRequested = true;
        MediaRequested?.Invoke(this, item);
    }

    private void GameCard_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        _mediaRequested = false;
    }

    private void GameCard_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        SelectionRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void GameArtwork_OnMouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (DataContext is LibraryItemViewModel item)
        {
            DetailsRequested?.Invoke(this, item);
            e.Handled = true;
        }
    }
}
