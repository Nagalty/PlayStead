using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroContractTests
{
    [Fact]
    public void Hero_reuses_GameArtwork_with_real_detail_bindings()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.Contains(
            "controls:GameArtwork",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "SourcePath=\"{Binding CoverPath}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "HasArtwork=\"{Binding HasCover}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Title=\"{Binding Title}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "ProviderLabel=\"{Binding ProviderLabel}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_has_one_primary_play_action_bound_to_existing_launch_model()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        var playButtons =
            document
                .Descendants()
                .Where(element =>
                    element.Name.LocalName == "PlaySplitButton")
                .ToArray();

        Assert.Single(playButtons);
        Assert.Equal(
            "{Binding Launch}",
            (string?)playButtons[0].Attribute("DataContext"));
    }

    [Fact]
    public void Hero_does_not_introduce_remote_artwork_or_fake_metadata()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.DoesNotContain(
            "ImageSource=\"http://",
            xaml,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "ImageSource=\"https://",
            xaml,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "SteamGridDB",
            xaml,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(directory.FullName, "src", "PlayStead.UI");

            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }
}
