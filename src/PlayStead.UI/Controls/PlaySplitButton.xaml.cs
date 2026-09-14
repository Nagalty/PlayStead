using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Controls;

public partial class PlaySplitButton : UserControl
{
    public PlaySplitButton()
    {
        InitializeComponent();
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
