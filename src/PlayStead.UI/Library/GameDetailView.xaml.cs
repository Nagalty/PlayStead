using System.Windows.Controls;

namespace PlayStead.UI.Library;

public partial class GameDetailView :
    UserControl
{
    public GameDetailView(
        GameDetailViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        InitializeComponent();

        DataContext =
            viewModel;

        Loaded +=
            GameDetailView_OnLoaded;
    }

    private async void GameDetailView_OnLoaded(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        Loaded -=
            GameDetailView_OnLoaded;

        if (DataContext is GameDetailViewModel viewModel)
        {
            await viewModel.LoadAsync(
                CancellationToken.None);
        }
    }
}
