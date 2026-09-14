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
    }
}
