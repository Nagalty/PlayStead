using System.Windows;
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

    private void GameDetailView_OnSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        var wide =
            e.NewSize.Width >= 1100;

        if (wide)
        {
            DetailColumns.ColumnDefinitions[0].Width =
                new GridLength(1, GridUnitType.Star);
            DetailColumns.ColumnDefinitions[1].Width =
                new GridLength(1, GridUnitType.Star);
            DetailColumns.RowDefinitions[0].Height =
                GridLength.Auto;
            DetailColumns.RowDefinitions[1].Height =
                new GridLength(0);

            Grid.SetColumn(
                GameActivity,
                0);
            Grid.SetRow(
                GameActivity,
                0);
            Grid.SetColumn(
                InstallationInformation,
                1);
            Grid.SetRow(
                InstallationInformation,
                0);

            GameActivity.Margin =
                new Thickness(0, 0, 9, 0);
            InstallationInformation.Margin =
                new Thickness(9, 0, 0, 0);
        }
        else
        {
            DetailColumns.ColumnDefinitions[0].Width =
                new GridLength(1, GridUnitType.Star);
            DetailColumns.ColumnDefinitions[1].Width =
                new GridLength(0);
            DetailColumns.RowDefinitions[0].Height =
                GridLength.Auto;
            DetailColumns.RowDefinitions[1].Height =
                GridLength.Auto;

            Grid.SetColumn(
                GameActivity,
                0);
            Grid.SetRow(
                GameActivity,
                0);
            Grid.SetColumn(
                InstallationInformation,
                0);
            Grid.SetRow(
                InstallationInformation,
                1);

            GameActivity.Margin =
                new Thickness(0, 0, 0, 9);
            InstallationInformation.Margin =
                new Thickness(0, 9, 0, 0);
        }
    }
}
