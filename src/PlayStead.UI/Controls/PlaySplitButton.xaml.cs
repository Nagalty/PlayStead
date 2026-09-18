using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Controls;

public partial class PlaySplitButton : UserControl
{
    public static readonly DependencyProperty IsSessionActiveProperty =
        DependencyProperty.Register(
            nameof(IsSessionActive),
            typeof(bool),
            typeof(PlaySplitButton),
            new PropertyMetadata(false));

    public static readonly DependencyProperty PlayLabelProperty =
        DependencyProperty.Register(
            nameof(PlayLabel),
            typeof(string),
            typeof(PlaySplitButton),
            new PropertyMetadata("Jouer"));

    public static readonly DependencyProperty PrimaryButtonMinWidthProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonMinWidth),
            typeof(double),
            typeof(PlaySplitButton),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty PrimaryButtonPaddingProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonPadding),
            typeof(Thickness),
            typeof(PlaySplitButton),
            new PropertyMetadata(new Thickness(14, 10, 14, 10)));

    public PlaySplitButton()
    {
        InitializeComponent();
    }

    public bool IsSessionActive
    {
        get => (bool)GetValue(IsSessionActiveProperty);
        set => SetValue(IsSessionActiveProperty, value);
    }

    public string PlayLabel
    {
        get => (string)GetValue(PlayLabelProperty);
        set => SetValue(PlayLabelProperty, value);
    }

    public double PrimaryButtonMinWidth
    {
        get => (double)GetValue(PrimaryButtonMinWidthProperty);
        set => SetValue(PrimaryButtonMinWidthProperty, value);
    }

    public Thickness PrimaryButtonPadding
    {
        get => (Thickness)GetValue(PrimaryButtonPaddingProperty);
        set => SetValue(PrimaryButtonPaddingProperty, value);
    }

    private void Play_OnClick(object sender, RoutedEventArgs e)
    {
        Launch(null);
    }

    private void Options_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void Installation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: GameInstallation installation })
        {
            Launch(installation);
            e.Handled = true;
        }
    }

    private void Launch(GameInstallation? installation)
    {
        if (DataContext is not GameLaunchViewModel launch)
        {
            return;
        }

        try
        {
            if (installation is null)
            {
                launch.TryPlayDefault();
            }
            else
            {
                launch.TryPlay(installation);
            }
        }
        catch (Win32Exception)
        {
            MessageBox.Show("Impossible de lancer le jeu. Vérifiez l’installation de Steam.",
                "Jouer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
