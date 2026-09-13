using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Controls;

public partial class SectionHeader :
    UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(SectionHeader),
            new PropertyMetadata(string.Empty));

    public SectionHeader()
    {
        InitializeComponent();
    }

    public string Title
    {
        get =>
            (string)GetValue(
                TitleProperty);

        set =>
            SetValue(
                TitleProperty,
                value);
    }
}
