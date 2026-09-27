using System.Windows.Controls;

namespace PlayStead.UI.About;

public partial class AboutView : UserControl
{
    public AboutView(AboutViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}
