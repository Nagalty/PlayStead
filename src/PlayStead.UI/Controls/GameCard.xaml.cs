using System.Windows.Input;
using System.Windows.Controls;

namespace PlayStead.UI.Controls;

public partial class GameCard :
    UserControl
{
    public GameCard()
    {
        InitializeComponent();
    }

    public event EventHandler?
        SelectionRequested;

    private void GameCard_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        SelectionRequested?.Invoke(
            this,
            EventArgs.Empty);
    }
}
