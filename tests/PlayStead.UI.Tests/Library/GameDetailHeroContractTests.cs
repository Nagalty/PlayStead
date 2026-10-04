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
            "HasArtwork=\"False\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Binding HasCover",
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
        Assert.Contains("Command=\"{Binding PlayCommand}\"", splitButton, StringComparison.Ordinal);
        Assert.DoesNotContain("Setter Property=\"IsEnabled\"", splitButton, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_activity_meta_uses_readable_primary_text()
    {
        var hero = FindHero().ToString();

        Assert.Contains("Foreground=\"{DynamicResource PlayStead.Brush.TextPrimary}\"", hero, StringComparison.Ordinal);
        Assert.Contains("FontWeight=\"SemiBold\"", hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_cover_reuses_the_library_mask_pattern_locally_with_a_subtle_shadow()
    {
        var document = XDocument.Load(FindUiFile("Library/GameDetailView.xaml"));
        var image = document.Descendants().Single(element =>
            element.Name.LocalName == "Image" &&
            ((string?)element.Attribute("Source")) == "{Binding CoverPath}");
        var grid = image.Parent;
        var inner = grid?.Parent;
        var wrapper = inner?.Parent;
        Assert.NotNull(grid);
        Assert.NotNull(inner);
        Assert.NotNull(wrapper);
        Assert.Equal("Border", wrapper!.Name.LocalName);
        Assert.Equal("176", (string?)inner!.Attribute("Width"));
        Assert.Equal("264", (string?)inner.Attribute("Height"));
        Assert.Equal("UniformToFill", (string?)image.Attribute("Stretch"));
        Assert.Equal("Stretch", (string?)image.Attribute("HorizontalAlignment"));
        Assert.Equal("Stretch", (string?)image.Attribute("VerticalAlignment"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", (string?)wrapper.Attribute("CornerRadius"));
        Assert.Equal("Transparent", (string?)wrapper.Attribute("Background"));
        Assert.Null(wrapper.Attribute("BorderBrush"));
        Assert.Null(wrapper.Attribute("BorderThickness"));

        var maskHost = inner!;
        Assert.Equal("Border", maskHost.Name.LocalName);
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", (string?)maskHost.Attribute("CornerRadius"));
        Assert.Equal("True", (string?)maskHost.Attribute("ClipToBounds"));
        var visual = maskHost.Descendants().Single(element => element.Name.LocalName == "VisualBrush");
        Assert.Equal("Fill", (string?)visual.Attribute("Stretch"));
        var mask = visual.Descendants().Single(element => element.Name.LocalName == "Border");
        Assert.Equal("176", (string?)mask.Attribute("Width"));
        Assert.Equal("264", (string?)mask.Attribute("Height"));
        Assert.Equal("Black", (string?)mask.Attribute("Background"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", (string?)mask.Attribute("CornerRadius"));

        var fallback = grid!.Descendants().Single(element => element.Name.LocalName == "GameArtwork");
        Assert.Equal("False", (string?)fallback.Attribute("HasArtwork"));
        Assert.Contains(fallback.Descendants(), element =>
            element.Name.LocalName == "DataTrigger" &&
            element.ToString().Contains("{Binding HasCover}", StringComparison.Ordinal) &&
            element.ToString().Contains("Collapsed", StringComparison.Ordinal));

        var shadow = wrapper.Descendants().Single(element => element.Name.LocalName == "DropShadowEffect");
        Assert.Equal("#CC000000", (string?)shadow.Attribute("Color"));
        Assert.Equal("0.42", (string?)shadow.Attribute("Opacity"));
        Assert.Equal("10", (string?)shadow.Attribute("BlurRadius"));
        Assert.Equal("3", (string?)shadow.Attribute("ShadowDepth"));
    }

    [Fact]
    public void Hero_title_wraps_with_a_responsive_two_line_guard()
    {
        var hero = FindHero();
        var title = hero.Descendants()
            .First(element =>
                element.Name.LocalName == "TextBlock" &&
                ((string?)element.Attribute("Text"))?.Contains(
                    "{Binding DisplayTitle}",
                    StringComparison.Ordinal) == true);

        Assert.Equal("560", (string?)title.Attribute("MaxWidth"));
        Assert.Equal("78", (string?)title.Attribute("MaxHeight"));
        Assert.Equal("Left", (string?)title.Attribute("HorizontalAlignment"));
        Assert.Equal("Wrap", (string?)title.Attribute("TextWrapping"));
        Assert.Equal("CharacterEllipsis", (string?)title.Attribute("TextTrimming"));
        var effect = title.Descendants().Single(element => element.Name.LocalName == "DropShadowEffect");
        Assert.Equal("6", (string?)effect.Attribute("BlurRadius"));
        Assert.Equal("1", (string?)effect.Attribute("ShadowDepth"));
        Assert.Equal("0.84", (string?)effect.Attribute("Opacity"));
        var effects = hero.Descendants().Where(element => element.Name.LocalName == "DropShadowEffect").ToArray();
        Assert.Contains(effects, candidate =>
            candidate.Ancestors().Any(element => element == title));
    }

    [Fact]
    public void Hero_title_shadow_is_common_to_all_providers_and_activity_states()
    {
        var hero = FindHero();
        var title = hero.Descendants()
            .Single(element =>
                element.Name.LocalName == "TextBlock" &&
                ((string?)element.Attribute("Text"))?.Contains(
                    "{Binding DisplayTitle}",
                    StringComparison.Ordinal) == true);
        var shadow = title.Descendants()
            .Single(element => element.Name.LocalName == "DropShadowEffect");

        Assert.Equal("#CC000000", (string?)shadow.Attribute("Color"));
        Assert.Equal("0.84", (string?)shadow.Attribute("Opacity"));
        Assert.DoesNotContain(title.Ancestors(), element =>
            element.Name.LocalName == "DataTrigger");
        Assert.DoesNotContain(title.Ancestors(), element =>
            element.Name.LocalName == "Style");
        Assert.DoesNotContain(hero.Descendants(), element =>
            element.Name.LocalName == "DataTrigger" &&
            element.ToString().Contains("Provider", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hero.Descendants(), element =>
            element.Name.LocalName == "WrapPanel" &&
            element.ToString().Contains("Activity.HasSessionHistory", StringComparison.Ordinal));
    }

    [Fact]
    public void Hero_uses_one_background_with_a_vertical_fade_behind_detail_content()
    {
        var hero = FindHero();
        var fade = hero.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroVerticalFade"));

        Assert.Equal("False", (string?)fade.Attribute("IsHitTestVisible"));
        Assert.Contains(fade.Descendants(), element =>
            element.Name.LocalName == "LinearGradientBrush" &&
            element.Descendants().Any(stop => stop.Name.LocalName == "GradientStop"));
        Assert.Contains(fade.Descendants(), element =>
            element.Name.LocalName == "GradientStop" &&
            ((string?)element.Attribute("Color")) == "#FF0B1117");

        var source = File.ReadAllText(FindUiFile("Library/GameDetailView.xaml"));
        Assert.Contains("Margin=\"24,-30,24,24\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_and_detail_cards_share_a_centered_content_rail()
    {
        var document = XDocument.Load(FindUiFile("Library/GameDetailView.xaml"));
        var heroRail = document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentRailContainer"));
        var contentRail = document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailContentRail"));

        Assert.Null(heroRail.Attribute("MaxWidth"));
        Assert.Equal("Stretch", (string?)heroRail.Attribute("HorizontalAlignment"));
        Assert.Equal("0", (string?)heroRail.Attribute("Margin"));
        Assert.Null(contentRail.Attribute("MaxWidth"));
        Assert.Equal("Stretch", (string?)contentRail.Attribute("HorizontalAlignment"));
        Assert.Equal("24,-30,24,24", (string?)contentRail.Attribute("Margin"));
    }

    [Fact]
    public void Hero_uses_a_full_bleed_background_with_a_separate_transparent_content_overlay()
    {
        var document = XDocument.Load(FindUiFile("Library/GameDetailView.xaml"));
        var hero = document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        var overlay = hero.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentRail"));

        Assert.Equal("Stretch", (string?)overlay.Attribute("HorizontalAlignment"));
        Assert.Null(overlay.Attribute("Width"));
        Assert.Equal("Transparent", (string?)overlay.Attribute("Background"));
        Assert.DoesNotContain(overlay.Descendants(), element =>
            element.Name.LocalName == "LinearGradientBrush");
        var scrim = hero.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroLeftScrim"));
        Assert.Contains(scrim.Descendants(), element =>
            element.Name.LocalName == "LinearGradientBrush");
    }

    [Fact]
    public void Hero_background_is_sibling_to_the_cards_rail_and_content_keeps_its_gutter()
    {
        var document = XDocument.Load(FindUiFile("Library/GameDetailView.xaml"));
        var hero = document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        var contentRail = document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailContentRail"));
        var scrollViewer = document.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer");
        var heroParent = hero.Parent;
        var contentParent = contentRail.Parent;

        Assert.Equal("Stretch", (string?)scrollViewer.Attribute("HorizontalContentAlignment"));
        Assert.NotNull(heroParent);
        Assert.Same(heroParent, contentParent);
        Assert.DoesNotContain(hero.Ancestors(), element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailContentRail"));
        var gutter = hero.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentGutter"));
        Assert.Equal("24,0", (string?)gutter.Attribute("Margin"));
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
