using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Home;

public partial class HomeView : UserControl
{
    public HomeView(HomeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
        Loaded += async (_, _) =>
            await viewModel.RefreshFeaturedGameAsync(CancellationToken.None);
    }

    private void HomeView_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 1100;
        if (wide)
        {
            HomeEditorialRegion.ColumnDefinitions[0].Width =
                new GridLength(3, GridUnitType.Star);
            HomeEditorialRegion.ColumnDefinitions[1].Width =
                new GridLength(16);
            HomeEditorialRegion.ColumnDefinitions[2].Width =
                new GridLength(2, GridUnitType.Star);
            HomeEditorialRegion.RowDefinitions[1].Height =
                new GridLength(0);

            Grid.SetColumn(EditorialPlaceholder, 0);
            Grid.SetRow(EditorialPlaceholder, 0);
            Grid.SetColumn(DormantGameCard, 2);
            Grid.SetRow(DormantGameCard, 0);
            Grid.SetColumn(DormantGamePlaceholder, 2);
            Grid.SetRow(DormantGamePlaceholder, 0);
            return;
        }

        HomeEditorialRegion.ColumnDefinitions[0].Width =
            new GridLength(1, GridUnitType.Star);
        HomeEditorialRegion.ColumnDefinitions[1].Width =
            new GridLength(0);
        HomeEditorialRegion.ColumnDefinitions[2].Width =
            new GridLength(0);
        HomeEditorialRegion.RowDefinitions[1].Height =
            GridLength.Auto;

        Grid.SetColumn(EditorialPlaceholder, 0);
        Grid.SetRow(EditorialPlaceholder, 0);
        Grid.SetColumn(DormantGameCard, 0);
        Grid.SetRow(DormantGameCard, 1);
        Grid.SetColumn(DormantGamePlaceholder, 0);
        Grid.SetRow(DormantGamePlaceholder, 1);
    }

}
