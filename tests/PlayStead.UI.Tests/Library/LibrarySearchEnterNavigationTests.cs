using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchTypingContractTests
{
    [Fact]
    public void Search_box_filters_on_each_text_change_without_submit_action()
    {
        var xamlPath = FindUiFile(Path.Combine("Library", "LibraryView.xaml"));
        var xaml = XDocument.Load(xamlPath);

        var searchBox =
            xaml.Descendants().Single(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" &&
                    attribute.Value == "LibrarySearchBox"));

        Assert.Equal(
            "LibrarySearchBox_OnTextChanged",
            searchBox.Attribute("TextChanged")?.Value);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory = Path.Combine(directory.FullName, "src", "PlayStead.UI");
            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("PlayStead.UI source directory was not found.");
    }
}
