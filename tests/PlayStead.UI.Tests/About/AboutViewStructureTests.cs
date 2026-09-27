using Xunit;

namespace PlayStead.UI.Tests.About;

public sealed class AboutViewStructureTests
{
    private static string Read(string path) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "src", "PlayStead.UI", path));

    [Fact]
    public void About_view_binds_runtime_identity_and_channel()
    {
        var xaml = Read("About/AboutView.xaml");

        Assert.Contains("Text=\"{Binding ProductName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Version, StringFormat=Version {0}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ChannelDisplay, StringFormat=Canal : {0}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("0.4.3-alpha1", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_exposes_an_about_route_and_navigation_entry()
    {
        var shell = Read("Shell/ShellViewModel.cs");
        var window = Read("MainWindow.xaml");
        var route = Read("Navigation/AppRoute.cs");

        Assert.Contains("About", route, StringComparison.Ordinal);
        Assert.Contains("NavigateAboutCommand", shell, StringComparison.Ordinal);
        Assert.Contains("AboutNavButton", window, StringComparison.Ordinal);
        Assert.Contains("À propos", window, StringComparison.Ordinal);
    }
}
