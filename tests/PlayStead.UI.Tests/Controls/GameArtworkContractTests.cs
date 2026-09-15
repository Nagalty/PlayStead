using System.Xml.Linq;

namespace PlayStead.UI.Tests.Controls;

public sealed class GameArtworkContractTests
{
    [Fact]
    public void GameArtwork_control_files_exist_and_expose_expected_dependency_properties()
    {
        var xamlPath = FindUiFile(
            Path.Combine(
                "Controls",
                "GameArtwork.xaml"));

        var codeBehindPath = FindUiFile(
            Path.Combine(
                "Controls",
                "GameArtwork.xaml.cs"));

        var document = XDocument.Load(
            xamlPath);

        var root = Assert.IsType<XElement>(
            document.Root);

        Assert.Equal(
            "UserControl",
            root.Name.LocalName);

        Assert.Equal(
            "PlayStead.UI.Controls.GameArtwork",
            AttributeValue(
                root,
                "Class"));

        var codeBehind = File.ReadAllText(
            codeBehindPath);

        Assert.Contains(
            "partial class GameArtwork",
            codeBehind,
            StringComparison.Ordinal);

        foreach (var propertyName in new[]
                 {
                     "SourcePath",
                     "Title",
                     "ProviderLabel",
                     "HasArtwork"
                 })
        {
            Assert.Contains(
                $"{propertyName}Property",
                codeBehind,
                StringComparison.Ordinal);

            Assert.Contains(
                $"nameof({propertyName})",
                codeBehind,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GameArtwork_uses_local_image_with_uniform_stretch()
    {
        var document = LoadGameArtwork();

        var image = Assert.Single(
            document.Descendants(),
            element =>
                element.Name.LocalName ==
                "Image");

        Assert.Equal(
            "Uniform",
            AttributeValue(
                image,
                "Stretch"));

        var source =
            AttributeValue(
                image,
                "Source");

        Assert.False(
            string.IsNullOrWhiteSpace(
                source));

        Assert.Contains(
            "SourcePath",
            source!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GameArtwork_fallback_contains_title_provider_and_copper_resource()
    {
        var document = LoadGameArtwork();

        Assert.Contains(
            document.Descendants(),
            element =>
                BindingAttributeContains(
                    element,
                    "Text",
                    "Title"));

        Assert.Contains(
            document.Descendants(),
            element =>
                BindingAttributeContains(
                    element,
                    "Text",
                    "ProviderLabel"));

        Assert.Contains(
            document
                .Descendants()
                .SelectMany(
                    element =>
                        element.Attributes()),
            attribute =>
                attribute.Value.Contains(
                    "Copper",
                    StringComparison.OrdinalIgnoreCase) &&
                (attribute.Value.Contains(
                     "StaticResource",
                     StringComparison.Ordinal) ||
                 attribute.Value.Contains(
                     "DynamicResource",
                     StringComparison.Ordinal)));
    }

    [Fact]
    public void GameArtwork_switches_image_and_fallback_from_HasArtwork()
    {
        var document = LoadGameArtwork();

        var triggers =
            document
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName ==
                        "DataTrigger")
                .ToArray();

        Assert.NotEmpty(
            triggers);

        Assert.Contains(
            triggers,
            trigger =>
            {
                var binding =
                    AttributeValue(
                        trigger,
                        "Binding");

                return binding is not null &&
                       binding.Contains(
                           "HasArtwork",
                           StringComparison.Ordinal);
            });

        Assert.Contains(
            document.Descendants(),
            element =>
                element.Name.LocalName ==
                    "Setter" &&
                AttributeValue(
                    element,
                    "Property") ==
                    "Visibility");
    }

    [Fact]
    public void GameArtwork_contains_no_remote_media_uri()
    {
        var document = LoadGameArtwork();

        var applicationAttributeValues =
            document
                .Root!
                .DescendantsAndSelf()
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
            applicationAttributeValues,
            value =>
                value.Contains(
                    "http://",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "https://",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "steamstatic",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GameCard_consumes_GameArtwork_from_library_media_state()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameCard.xaml")));

        var artwork =
            Assert.Single(
                document.Descendants(),
                element =>
                    element.Name.LocalName ==
                    "GameArtwork");

        Assert.Equal(
            "{Binding CoverPath}",
            AttributeValue(
                artwork,
                "SourcePath"));

        Assert.Equal(
            "{Binding Title}",
            AttributeValue(
                artwork,
                "Title"));

        Assert.Equal(
            "{Binding ProviderLabel}",
            AttributeValue(
                artwork,
                "ProviderLabel"));

        Assert.Equal(
            "{Binding HasCover}",
            AttributeValue(
                artwork,
                "HasArtwork"));
    }

    private static XDocument LoadGameArtwork() =>
        XDocument.Load(
            FindUiFile(
                Path.Combine(
                    "Controls",
                    "GameArtwork.xaml")));

    private static bool BindingAttributeContains(
        XElement element,
        string attributeName,
        string bindingMember)
    {
        var value =
            AttributeValue(
                element,
                attributeName);

        return value is not null &&
               value.Contains(
                   bindingMember,
                   StringComparison.Ordinal) &&
               value.Contains(
                   "Binding",
                   StringComparison.Ordinal);
    }

    private static string? AttributeValue(
        XElement element,
        string localName) =>
        element
            .Attributes()
            .SingleOrDefault(
                attribute =>
                    attribute.Name.LocalName ==
                    localName)
            ?.Value;

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

            if (File.Exists(
                    path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Required UI file missing: {relativePath}");
    }
}
