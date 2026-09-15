using System.Xml.Linq;

namespace PlayStead.UI.Tests.Controls;

public sealed class GameArtworkFallbackContractTests
{
    [Fact]
    public void Fallback_is_a_dark_copper_composition_not_a_symbol_placeholder()
    {
        var path =
            FindUiFile(
                Path.Combine(
                    "Controls",
                    "GameArtwork.xaml"));

        var source =
            File.ReadAllText(
                path);

        foreach (var symbol in new[]
                 {
                     "◇",
                     "◆",
                     "◈",
                     "♦",
                     "⬥"
                 })
        {
            Assert.DoesNotContain(
                symbol,
                source,
                StringComparison.Ordinal);
        }

        var document =
            XDocument.Load(
                path);

        Assert.Contains(
            document.Descendants(),
            element =>
                element.Name.LocalName == "SolidColorBrush" &&
                element.Attributes().Any(
                    attribute =>
                        attribute.Name.LocalName == "Key" &&
                        attribute.Value == "CopperAccentBrush"));

        Assert.Contains(
            document.Descendants(),
            element =>
                element.Name.LocalName == "Border" &&
                element.Attributes().Any(
                    attribute =>
                        attribute.Name.LocalName == "Background" &&
                        attribute.Value.Contains(
                            "CopperAccentBrush",
                            StringComparison.Ordinal)));
    }

    [Fact]
    public void Fallback_keeps_title_and_provider_bindings()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameArtwork.xaml")));

        var textBlocks =
            document
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName == "TextBlock")
                .ToArray();

        Assert.Contains(
            textBlocks,
            element =>
                element.Attributes().Any(
                    attribute =>
                        attribute.Name.LocalName == "Text" &&
                        attribute.Value.Contains(
                            "Title",
                            StringComparison.Ordinal)));

        Assert.Contains(
            textBlocks,
            element =>
                element.Attributes().Any(
                    attribute =>
                        attribute.Name.LocalName == "Text" &&
                        attribute.Value.Contains(
                            "ProviderLabel",
                            StringComparison.Ordinal)));
    }

    [Fact]
    public void Fallback_does_not_reference_remote_artwork()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameArtwork.xaml")));

        var nonNamespaceAttributeValues =
            document
                .Descendants()
                .SelectMany(
                    element =>
                        element.Attributes())
                .Where(
                    attribute =>
                        !attribute.IsNamespaceDeclaration)
                .Select(
                    attribute =>
                        attribute.Value)
                .ToArray();

        Assert.DoesNotContain(
            nonNamespaceAttributeValues,
            value =>
                value.Contains(
                    "http://",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "https://",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string FindUiFile(
        string relativePath)
    {
        for (var directory =
                 new DirectoryInfo(
                     AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI",
                    relativePath);

            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Required UI file missing: {relativePath}");
    }
}
