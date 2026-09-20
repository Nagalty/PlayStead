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

}
