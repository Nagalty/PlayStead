using System.Xml.Linq;

namespace PlayStead.UI.Tests.Notifications;

public sealed class NotificationPanelContractTests
{
    [Fact]
    public void Main_window_declares_bell_badge_right_panel_and_filters()
    {
        var xaml = XDocument.Load(FindUiFile("MainWindow.xaml"));
        Assert.NotNull(FindByName(xaml, "NotificationBellButton"));
        Assert.NotNull(FindByName(xaml, "NotificationBadge"));
        Assert.NotNull(FindByName(xaml, "NotificationPanelHost"));
    }

    [Fact]
    public void Notification_panel_contains_items_and_no_generic_resolve_or_ignore_action()
    {
        var xaml = XDocument.Load(FindUiFile(Path.Combine("Notifications", "NotificationPanel.xaml")));
        Assert.Contains(xaml.Descendants(), x => x.Name.LocalName == "ItemsControl");
        Assert.NotNull(FindByName(xaml, "ActiveNotificationsFilterButton"));
        Assert.NotNull(FindByName(xaml, "ResolvedNotificationsFilterButton"));
        Assert.DoesNotContain(xaml.Descendants(), x => (x.Attribute("Name")?.Value ?? x.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value) is "ResolveButton" or "IgnoreButton" or "DismissButton");
    }

    [Fact]
    public void Existing_attention_route_remains_bound_and_panel_is_right_aligned()
    {
        var xaml = XDocument.Load(FindUiFile("MainWindow.xaml"));
        var attention = FindByName(xaml, "AttentionNavButton");
        Assert.Equal("{Binding NavigateAttentionCommand}", attention?.Attribute("Command")?.Value);
        var panel = FindByName(xaml, "NotificationPanelHost");
        Assert.Equal("Right", panel?.Attribute("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void Main_window_binds_bell_command_and_badge_visibility()
    {
        var xaml = XDocument.Load(FindUiFile("MainWindow.xaml"));
        var bell = FindByName(xaml, "NotificationBellButton");
        var badge = FindByName(xaml, "NotificationBadge");
        Assert.Equal("{Binding TogglePanelCommand}", bell?.Attribute("Command")?.Value);
        Assert.Contains("IsBadgeVisible", badge?.Attribute("Visibility")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    private static System.Xml.Linq.XElement? FindByName(XDocument document, string name) =>
        document.Descendants().FirstOrDefault(x => x.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == name));

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "PlayStead.UI", relativePath);
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }
}
