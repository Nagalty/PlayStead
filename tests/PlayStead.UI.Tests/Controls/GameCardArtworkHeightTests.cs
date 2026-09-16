using System.Globalization;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Controls;

public sealed class GameCardArtworkHeightTests
{
    [Fact]
    public void GameCard_reserves_exactly_300_pixels_for_artwork()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameCard.xaml")));

        var rows =
            document
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName ==
                        "RowDefinition")
                .ToArray();

        Assert.NotEmpty(rows);

        var height =
            rows[0]
                .Attributes()
                .Single(
                    attribute =>
                        attribute.Name.LocalName ==
                        "Height")
                .Value;

        Assert.True(
            double.TryParse(
                height,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var pixels),
            $"Expected a numeric artwork row height, got '{height}'.");

        Assert.Equal(
            300d,
            pixels);
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
