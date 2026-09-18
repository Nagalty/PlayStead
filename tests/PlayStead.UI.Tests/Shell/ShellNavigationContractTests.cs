using System.IO;

namespace PlayStead.UI.Tests.Shell;

public sealed class ShellNavigationContractTests
{
    [Fact]
    public void Main_window_uses_compact_navigation_and_global_game_search()
    {
        var root = FindRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "MainWindow.xaml"));
        Assert.Contains("PlayStead.Button.Navigation", xaml);
        Assert.Contains("PlayStead.Icon.Home", xaml);
        Assert.Contains("PlayStead.Icon.Library", xaml);
        Assert.Contains("PlayStead.Icon.Report", xaml);
        Assert.Contains("PlayStead.Icon.Search", File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Themes", "PlaySteadControls.xaml")));
        Assert.Contains("Rechercher un jeu...", xaml);
        Assert.Contains("GotKeyboardFocus=\"ShellSearchBox_OnKeyboardFocusChanged\"", xaml);
        Assert.Contains("LostKeyboardFocus=\"ShellSearchBox_OnKeyboardFocusChanged\"", xaml);
        Assert.Contains("ShellSearchBox_OnKeyDown", xaml);
        Assert.Contains("NavigateAttentionCommand", xaml);
        Assert.Contains("NavigateSettingsCommand", xaml);
        Assert.Contains("TogglePanelCommand", xaml);
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PlayStead.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
