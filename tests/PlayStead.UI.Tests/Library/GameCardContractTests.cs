using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class GameCardContractTests
{
    [Fact]
    public void GameCard_control_files_exist_and_root_matches_codebehind()
    {
        var xamlPath =
            FindUiFile(
                Path.Combine(
                    "Controls",
                    "GameCard.xaml"));

        var codeBehindPath =
            FindUiFile(
                Path.Combine(
                    "Controls",
                    "GameCard.xaml.cs"));

        var document =
            XDocument.Load(
                xamlPath);

        var root =
            Assert.IsType<XElement>(
                document.Root);

        Assert.Equal(
            "UserControl",
            root.Name.LocalName);

        Assert.Equal(
            "PlayStead.UI.Controls.GameCard",
            AttributeValue(
                root,
                "Class"));

        var codeBehind =
            File.ReadAllText(
                codeBehindPath);

        Assert.Contains(
            "partial class GameCard",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GameCard_presents_only_real_library_metadata_and_status()
    {
        var document =
            LoadGameCard();

        Assert.Contains(
            document.Descendants(),
            element =>
                AttributeValue(
                    element,
                    "Text") ==
                "{Binding Title}");

        Assert.Contains(
            document.Descendants(),
            element =>
                AttributeValue(
                    element,
                    "Text") ==
                "{Binding ProviderLabel}");

        Assert.Contains(
            document.Descendants(),
            element =>
                AttributeValue(
                    element,
                    "Text") ==
                "{Binding InstalledSizeLabel, Mode=OneWay}");

        Assert.Contains(
            document.Descendants(),
            element =>
                AttributeValue(
                    element,
                    "Text") ==
                "{Binding SessionStatusLabel}");

        Assert.Contains(
            document.Descendants(),
            element =>
                AttributeValue(
                    element,
                    "Text") ==
                "{Binding SteamStatusLabel}");
    }

    [Fact]
    public void GameCard_uses_local_media_fallback_without_eager_image_loading()
    {
        var document =
            LoadGameCard();

        Assert.NotNull(
            FindByName(
                document,
                "MediaFallback"));

        Assert.DoesNotContain(
            document.Descendants(),
            element =>
                element.Name.LocalName ==
                "Image");
    }

    [Fact]
    public void Library_grid_uses_reusable_GameCard_control()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var gridHost =
            FindRequiredByName(
                document,
                "GameGridRows");

        Assert.Contains(
            gridHost.Descendants(),
            element =>
                element.Name.LocalName ==
                "GameCard");
    }

    private static XDocument LoadGameCard()
    {
        return XDocument.Load(
            FindUiFile(
                Path.Combine(
                    "Controls",
                    "GameCard.xaml")));
    }


    private static XElement FindRequiredByName(
        XDocument document,
        string name)
    {
        return FindByName(
                   document,
                   name)
               ?? throw new Xunit.Sdk.XunitException(
                   $"Element with x:Name '{name}' was not found.");
    }

    private static XElement? FindByName(
        XDocument document,
        string name)
    {
        return document
            .Descendants()
            .SingleOrDefault(
                element =>
                    element
                        .Attributes()
                        .Any(
                            attribute =>
                                attribute.Name.LocalName ==
                                    "Name" &&
                                attribute.Value ==
                                    name));
    }

    private static string? AttributeValue(
        XElement element,
        string localName)
    {
        return element
            .Attributes()
            .SingleOrDefault(
                attribute =>
                    attribute.Name.LocalName ==
                    localName)
            ?.Value;
    }

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
