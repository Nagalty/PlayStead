using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Controls;

public partial class KpiCard :
    UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(KpiCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(KpiCard),
            new PropertyMetadata(string.Empty));

    public KpiCard()
    {
        InitializeComponent();
    }

    public string Label
    {
        get =>
            (string)GetValue(
                LabelProperty);

        set =>
            SetValue(
                LabelProperty,
                value);
    }

    public string Value
    {
        get =>
            (string)GetValue(
                ValueProperty);

        set =>
            SetValue(
                ValueProperty,
                value);
    }
}
