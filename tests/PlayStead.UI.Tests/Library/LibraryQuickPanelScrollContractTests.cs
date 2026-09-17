using System.IO;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryQuickPanelScrollContractTests
{
    [Fact]
    public void Quick_panel_body_is_vertically_scrollable_without_horizontal_scroll()
    {
        var xamlPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "LibraryView.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        var panelStart = xaml.IndexOf("x:Name=\"GameQuickPanel\"", StringComparison.Ordinal);
        var panelEnd = xaml.IndexOf("x:Name=\"EmptyStatePanel\"", panelStart, StringComparison.Ordinal);
        Assert.True(panelStart >= 0 && panelEnd > panelStart);
        var panel = xaml[panelStart..panelEnd];

        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", panel);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", panel);
        Assert.Contains("x:Name=\"OpenGameDetailButton\"", panel);
        Assert.Contains("Content=\"Voir la fiche\"", panel);
    }
}
