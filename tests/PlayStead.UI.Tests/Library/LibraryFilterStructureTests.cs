using System.Xml.Linq;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryFilterStructureTests
{
    [Fact]
    public void Library_view_exposes_installed_attention_filters_and_available_sorts()
    {
        var xaml = XDocument.Load(FindUiFile("Library/LibraryView.xaml"));
        var text = xaml.ToString(SaveOptions.DisableFormatting);
        var buttons = xaml.Descendants().Where(element => element.Name.LocalName == "Button").ToList();
        var comboBox = xaml.Descendants().Single(element => element.Name.LocalName == "ComboBox");

        Assert.Contains(buttons, button =>
            button.Attributes().Any(attribute => attribute.Value == "InstalledFilterButton") &&
            (string?)button.Attribute("Content") == "Installés");
        Assert.Contains(buttons, button =>
            button.Attributes().Any(attribute => attribute.Value == "AttentionFilterButton") &&
            (string?)button.Attribute("Content") == "À signaler");

        var sortTags = comboBox.Descendants()
            .Where(element => element.Name.LocalName == "ComboBoxItem")
            .Select(element => (string?)element.Attribute("Tag") ?? string.Empty)
            .ToArray();

        Assert.Equal(["Recent", "Playtime", "Title", "Size"], sortTags);
        Assert.DoesNotContain("Content=\"Moddés\"", text);
        Assert.DoesNotContain("Content=\"Dernière mise à jour\"", text);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "PlayStead.UI", relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
