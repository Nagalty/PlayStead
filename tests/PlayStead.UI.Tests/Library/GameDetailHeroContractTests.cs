using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroContractTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

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

        Assert.Contains(
            "ImageSource=\"{Binding HeroPath}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Stretch=\"UniformToFill\"",
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

    [Fact]
    public void Hero_content_is_integrated_without_nested_opaque_card()
    {
        var hero = FindHero();
        var nestedBorders = hero.Descendants()
            .Where(element => element.Name.LocalName == "Border")
            .ToArray();

        Assert.DoesNotContain(
            nestedBorders,
            element => string.Equals(
                (string?)element.Attribute("Background"),
                "{DynamicResource PlayStead.Brush.Surface}",
                StringComparison.Ordinal));

        var title = hero.Descendants()
            .First(element =>
                element.Name.LocalName == "TextBlock" &&
                ((string?)element.Attribute("Text"))?.Contains(
                    "{Binding DisplayTitle}",
                    StringComparison.Ordinal) == true);

        Assert.DoesNotContain(
            title.Ancestors().Where(element => element.Name.LocalName == "Border"),
            element => element != hero);
    }

    [Fact]
    public void Hero_hides_technical_installation_details_and_shows_conditional_activity()
    {
        var hero = FindHero();
        var heroText = hero.ToString();

        Assert.DoesNotContain("{Binding InstallPath}", heroText, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding InstalledSizeLabel}", heroText, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding SteamStatusLabel}", heroText, StringComparison.Ordinal);
        Assert.Contains("{Binding Activity.HasSessionHistory", heroText, StringComparison.Ordinal);
        Assert.Contains("{Binding Activity.TotalPlayTimeLabel}", heroText, StringComparison.Ordinal);
        Assert.Contains("{Binding Activity.LastActivityLabel}", heroText, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_platform_is_compact_and_primary_cta_is_prominent()
    {
        var hero = FindHero().ToString();
        var document = XDocument.Load(FindUiFile("Library/GameDetailView.xaml"));

        Assert.Contains("x:Name=\"HeroPlatformRow\"", hero, StringComparison.Ordinal);
        Assert.Contains("Orientation=\"Horizontal\"", hero, StringComparison.Ordinal);
        Assert.Contains("controls:PlaySplitButton", hero, StringComparison.Ordinal);

        Assert.Contains("PrimaryButtonMinWidth=\"150\"", hero, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonPadding=\"20,14\"", hero, StringComparison.Ordinal);

        var playSplitButton = File.ReadAllText(
            FindUiFile(Path.Combine("Controls", "PlaySplitButton.xaml")));
        Assert.Contains("PrimaryButtonMinWidth, ElementName=Root", playSplitButton, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonPadding, ElementName=Root", playSplitButton, StringComparison.Ordinal);
        Assert.Contains("new Thickness(14, 10, 14, 10)", File.ReadAllText(
            FindUiFile(Path.Combine("Controls", "PlaySplitButton.xaml.cs"))), StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_cta_reflects_the_current_game_session_state()
    {
        var hero = FindHero().ToString();
        var document = XDocument.Load(FindUiFile(Path.Combine("Library", "GameDetailView.xaml")));
        var viewSource = document.ToString();
        var splitButton = File.ReadAllText(
            FindUiFile(Path.Combine("Controls", "PlaySplitButton.xaml")));

        Assert.Contains("x:Name=\"GameDetailRoot\"", document.ToString(), StringComparison.Ordinal);
        Assert.Contains("IsSessionActive=\"{Binding DataContext.Game.IsSessionActive, ElementName=GameDetailRoot}\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("PlayLabel=\"{Binding DataContext.SessionStatusLabel", viewSource, StringComparison.Ordinal);
        Assert.Contains("ElementName=GameDetailRoot", viewSource, StringComparison.Ordinal);
        Assert.Contains("Binding PlayLabel, RelativeSource={RelativeSource AncestorType=UserControl}", splitButton, StringComparison.Ordinal);
        Assert.Contains("Binding IsSessionActive, ElementName=Root", splitButton, StringComparison.Ordinal);
        Assert.Contains("Setter Property=\"IsEnabled\"", splitButton, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_activity_meta_uses_readable_primary_text()
    {
        var hero = FindHero().ToString();

        Assert.Contains("Foreground=\"{DynamicResource PlayStead.Brush.TextPrimary}\"", hero, StringComparison.Ordinal);
        Assert.Contains("FontWeight=\"SemiBold\"", hero, StringComparison.Ordinal);
    }

    private static XElement FindHero()
    {
        var document = XDocument.Load(
            FindUiFile(Path.Combine("Library", "GameDetailView.xaml")));

        return document.Descendants()
            .First(element =>
                string.Equals(
                    (string?)element.Attribute(X + "Name"),
                    "GameDetailHero",
                    StringComparison.Ordinal));
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
