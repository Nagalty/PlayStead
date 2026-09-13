using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Sessions;

public partial class SessionDetailView :
    UserControl
{
    public SessionDetailView()
    {
        InitializeComponent();
    }

    private void CorrectSessionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CorrectionPanel.Visibility =
            Visibility.Visible;
    }

    private async void SaveCorrectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is
            SessionDetailViewModel viewModel)
        {
            await viewModel.Correction.SaveAsync(
                CancellationToken.None);
        }
    }
}
