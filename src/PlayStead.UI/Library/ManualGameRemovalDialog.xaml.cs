using System.Windows;
using System.Windows.Input;

namespace PlayStead.UI.Library;

public partial class ManualGameRemovalDialog : Window
{
    public ManualGameRemovalDialog(string title)
    {
        InitializeComponent();
        MessageTextBlock.Text = $"Retirer « {title} » de PlayStead ?";
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void RemoveButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Dialog_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
