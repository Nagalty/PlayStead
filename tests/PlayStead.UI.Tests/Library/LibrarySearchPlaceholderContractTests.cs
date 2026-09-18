using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchPlaceholderContractTests
{
    [Fact]
    public void Library_view_does_not_declare_a_duplicate_search_placeholder()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        Assert.DoesNotContain("LibrarySearchBox", xaml.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("LibrarySearchPlaceholder", xaml.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Rechercher un jeu", xaml.ToString(), StringComparison.Ordinal);
    }

    private static XElement FindRequiredByName(
        XDocument document,
        string name) =>
        document
            .Descendants()
            .Single(
                element =>
                    element
                        .Attributes()
                        .Any(
                            attribute =>
                                attribute.Name.LocalName == "Name" &&
                                attribute.Value == name));

    private static string FindUiFile(
        string relativePath)
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI");

            if (Directory.Exists(
                    uiDirectory))
            {
                var path =
                    Path.Combine(
                        uiDirectory,
                        relativePath);

                Assert.True(
                    File.Exists(
                        path),
                    $"Required UI file missing: {relativePath}");

                return path;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }
}
