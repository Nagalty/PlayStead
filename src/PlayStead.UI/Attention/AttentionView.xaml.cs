using System.Windows.Controls;

namespace PlayStead.UI.Attention;

public partial class AttentionView : UserControl
{
    public AttentionView(AttentionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}
