using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchTypingContractTests
{
    [Fact]
    public void Library_view_does_not_declare_a_local_search_box()
    {
        var xamlPath = FindUiFile(Path.Combine("Library", "LibraryView.xaml"));
        var xaml = XDocument.Load(xamlPath);

        Assert.DoesNotContain(
            xaml.Descendants("TextBox"),
            element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" &&
                attribute.Value == "LibrarySearchBox"));
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
