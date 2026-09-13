using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Controls;

public partial class EmptyState :
    UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public EmptyState()
    {
        InitializeComponent();
    }

    public string Message
    {
        get =>
            (string)GetValue(
                MessageProperty);

        set =>
            SetValue(
                MessageProperty,
                value);
    }
}
