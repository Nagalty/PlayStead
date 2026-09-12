using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Library;

public partial class LibraryView :
    UserControl
{
    public LibraryView()
    {
        InitializeComponent();
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
}
