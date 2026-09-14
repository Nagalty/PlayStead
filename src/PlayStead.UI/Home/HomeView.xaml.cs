using System.Windows.Controls;

namespace PlayStead.UI.Home;

public partial class HomeView : UserControl
{
    public HomeView(HomeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
