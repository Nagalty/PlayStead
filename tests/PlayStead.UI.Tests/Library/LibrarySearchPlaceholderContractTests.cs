using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchPlaceholderContractTests
{
    [Fact]
    public void Search_box_declares_Rechercher_un_jeu_placeholder_and_focus_wiring()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var searchBox =
            FindRequiredByName(
                xaml,
                "LibrarySearchBox");

        Assert.Equal(
            "LibrarySearchBox_OnGotKeyboardFocus",
            searchBox.Attribute(
                "GotKeyboardFocus")?.Value);

        Assert.Equal(
            "LibrarySearchBox_OnLostKeyboardFocus",
            searchBox.Attribute(
                "LostKeyboardFocus")?.Value);

        var placeholder =
            FindRequiredByName(
                xaml,
                "LibrarySearchPlaceholder");

        Assert.Equal(
            "Rechercher un jeu",
            placeholder.Attribute(
                "Text")?.Value);

        Assert.Equal(
            "False",
            placeholder.Attribute(
                "IsHitTestVisible")?.Value);
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
