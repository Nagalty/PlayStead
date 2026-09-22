using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Xml.Linq;
using PlayStead.UI.Home;

namespace PlayStead.UI.Tests.Home;

public sealed class HomeViewStructureTests
{
    [Fact]
    public void HomeView_is_a_UserControl_constructed_with_HomeViewModel()
    {
        var assembly =
            typeof(HomeViewModel)
                .Assembly;

        var type =
            assembly.GetType(
                "PlayStead.UI.Home.HomeView");

        Assert.NotNull(
            type);

        Assert.True(
            typeof(UserControl)
                .IsAssignableFrom(
                    type));

        var constructor =
            type.GetConstructor(
                BindingFlags.Public |
                BindingFlags.Instance,
                binder: null,
                [typeof(HomeViewModel)],
                modifiers: null);

        Assert.NotNull(
            constructor);
    }

    [Fact]
    public void HomeView_declares_responsive_shared_KPIs_before_the_existing_hero()
    {
        var document =
            LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var root = Assert.IsType<XElement>(document.Root);
        var kpiRow = Assert.Single(
            root.Descendants(presentation + "WrapPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeKpiRow");
        var hero = Assert.Single(
            document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeHero");

        Assert.True(
            root.Descendants().ToList().IndexOf(kpiRow) <
            root.Descendants().ToList().IndexOf(hero),
            "The compact KPI row should precede the existing Home hero.");

        var card = Assert.Single(kpiRow.Elements(presentation + "Border"));
        Assert.Equal("300", (string?)card.Attribute("Width"));
        Assert.Equal("Left", (string?)card.Attribute("HorizontalAlignment"));
        Assert.Null(card.Attribute("Height"));
        Assert.Equal("{DynamicResource PlayStead.Surface.Metric}", (string?)card.Attribute("Style"));
        Assert.DoesNotContain(kpiRow.Descendants(), element => element.Name.LocalName == "KpiCard");
        Assert.DoesNotContain(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Sessions en cours");

        var value = Assert.Single(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding LibraryGameCount}");
        var label = Assert.Single(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Jeux installés");
        Assert.Equal("{DynamicResource PlayStead.Text.Display}", (string?)value.Attribute("Style"));
        Assert.Equal("{DynamicResource PlayStead.Text.BodySecondary}", (string?)label.Attribute("Style"));
        var iconLayout = Assert.Single(card.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute("Width") == "64");
        Assert.Equal("68", (string?)iconLayout.Attribute("Height"));
        var textLayout = Assert.Single(card.Descendants(presentation + "StackPanel"));
        Assert.Equal("{DynamicResource PlayStead.Spacing.2}", (string?)textLayout.Attribute("Margin"));

        var gamepadPath = Assert.Single(card.Descendants(presentation + "Path"));
        Assert.Equal("{StaticResource PlayStead.Icon.Gamepad}", (string?)gamepadPath.Attribute("Data"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Accent}", (string?)gamepadPath.Attribute("Stroke"));
        var gamepadIcon = XDocument.Load(FindUiFile("Themes/PlaySteadControls.xaml"));
        Assert.Contains(gamepadIcon.Descendants(presentation + "StreamGeometry"), geometry =>
            (string?)geometry.Attribute(xaml + "Key") == "PlayStead.Icon.Gamepad");
        Assert.DoesNotContain(card.Descendants(presentation + "Ellipse"), tile =>
            ((string?)tile.Attribute("Fill"))?.Contains("Accent", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("FontSize") is not null ||
            (string?)element.Attribute("FontWeight") is not null);

        var horizontalScroller = Assert.Single(document.Descendants(presentation + "ScrollViewer"));
        Assert.Equal("Disabled", (string?)horizontalScroller.Attribute("HorizontalScrollBarVisibility"));

        Assert.Contains(hero.Descendants(presentation + "MultiDataTrigger"), trigger =>
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasActiveSessionHero}" &&
                (string?)condition.Attribute("Value") == "True") &&
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasHero}" &&
                (string?)condition.Attribute("Value") == "True"));
        Assert.Contains(
            hero.Descendants(presentation + "ImageBrush"),
            image => (string?)image.Attribute("ImageSource") == "{Binding HeroPath}");
        var heroText = hero.Descendants(presentation + "TextBlock").ToArray();
        Assert.Contains(heroText, element => (string?)element.Attribute("Text") == "{Binding HeroEyebrow}");
        Assert.Contains(heroText, element => (string?)element.Attribute("Text") == "{Binding HeroTitle}");
        Assert.Contains(heroText, element => (string?)element.Attribute("Text") == "{Binding HeroSupportingText}");
        Assert.Contains(heroText, element => (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.PageTitle}");
        Assert.Contains(heroText, element => (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.BodySecondary}");

        var recentlyPlayed = Assert.Single(document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");
        Assert.Equal("{Binding RecentlyPlayedGames}", (string?)recentlyPlayed.Attribute("ItemsSource"));
        var recentlyPlayedSection = Assert.Single(document.Descendants(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSection");
        Assert.Equal("{DynamicResource PlayStead.Gap.Block.Small}",
            (string?)recentlyPlayedSection.Attribute("Margin"));
        Assert.DoesNotContain(document.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Sessions en cours");
        Assert.DoesNotContain(document.Descendants(presentation + "ItemsControl"), element =>
            (string?)element.Attribute(xaml + "Name") == "ActiveSessionsList");
        Assert.Equal("{DynamicResource PlayStead.Gap.Block.Medium}", (string?)hero.Attribute("Margin"));

        var homeViewModel = typeof(HomeViewModel);
        Assert.NotNull(homeViewModel.GetProperty("ActiveSessions"));

        var tokens = XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));
        Assert.Equal("0,16,0,0", Assert.Single(tokens.Descendants(presentation + "Thickness"), element =>
            (string?)element.Attribute(xaml + "Key") == "PlayStead.Gap.Block.Medium").Value.Trim());
    }

    [Fact]
    public void HomeHero_selects_idle_art_only_without_a_loaded_idle_asset_and_keeps_surface_fallback()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var hero = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeHero");
        var style = Assert.Single(hero.Elements(presentation + "Border.Style"))
            .Element(presentation + "Style");
        Assert.Contains(style!.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Background" &&
            (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Brush.Surface}");

        var triggers = Assert.Single(style.Elements(presentation + "Style.Triggers"));
        var active = Assert.Single(triggers.Elements(presentation + "MultiDataTrigger"), trigger =>
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasActiveSessionHero}" &&
                (string?)condition.Attribute("Value") == "True") &&
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasHero}" &&
                (string?)condition.Attribute("Value") == "True"));
        Assert.Contains(active.Descendants(presentation + "ImageBrush"), brush =>
            (string?)brush.Attribute("ImageSource") == "{Binding HeroPath}" &&
            (string?)brush.Attribute("Stretch") == "UniformToFill");

        var idle = Assert.Single(triggers.Elements(presentation + "MultiDataTrigger"), trigger =>
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasActiveSessionHero}" &&
                (string?)condition.Attribute("Value") == "False"));
        var conditions = idle.Element(presentation + "MultiDataTrigger.Conditions")!
            .Elements(presentation + "Condition").ToArray();
        Assert.Contains(conditions, condition =>
            (string?)condition.Attribute("Binding") == "{Binding HasActiveSessionHero}" &&
            (string?)condition.Attribute("Value") == "False");
        Assert.Contains(conditions, condition =>
            (string?)condition.Attribute("Binding") == "{Binding HasIdleHeroImage}" &&
            (string?)condition.Attribute("Value") == "True");
        Assert.Contains(idle.Descendants(presentation + "ImageBrush"), brush =>
            (string?)brush.Attribute("ImageSource") == "{Binding IdleHeroImageSource}" &&
            (string?)brush.Attribute("Stretch") == "UniformToFill");
    }

    [Fact]
    public void HomeHero_eyebrow_uses_accent_and_semibold_in_both_states()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var hero = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeHero");
        var eyebrow = Assert.Single(hero.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding HeroEyebrow}");
        var style = Assert.Single(eyebrow.Elements(presentation + "TextBlock.Style"))
            .Element(presentation + "Style");

        Assert.Equal("{StaticResource PlayStead.Text.Caption}", (string?)style?.Attribute("BasedOn"));
        Assert.Empty(style!.Descendants(presentation + "DataTrigger"));
        Assert.Equal("{Binding HeroEyebrow}", (string?)eyebrow.Attribute("Text"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Accent}", (string?)Assert.Single(
            style.Elements(presentation + "Setter"), setter =>
                (string?)setter.Attribute("Property") == "Foreground").Attribute("Value"));
        Assert.Equal("SemiBold", (string?)Assert.Single(style.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "FontWeight").Attribute("Value"));
    }

    [Fact]
    public void HomeView_uses_hero_for_active_sessions_and_hides_recently_played_section_when_empty()
    {
        var document =
            LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.DoesNotContain(
            document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "ActiveSessionsList");
        Assert.DoesNotContain(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Sessions en cours");

        var hero = Assert.Single(
            document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeHero");
        Assert.Contains(hero.Descendants(presentation + "MultiDataTrigger"), trigger =>
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasActiveSessionHero}" &&
                (string?)condition.Attribute("Value") == "True") &&
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasHero}" &&
                (string?)condition.Attribute("Value") == "True"));
        Assert.Contains(hero.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding HeroEyebrow}");
        Assert.Contains(hero.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding HeroTitle}");
        Assert.Contains(hero.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding HeroSupportingText}");

        var recent =
            Assert.Single(
                document.Descendants(
                    presentation + "ItemsControl"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "RecentlyPlayedGamesList");

        Assert.Equal(
            "{Binding RecentlyPlayedGames}",
            (string?)recent.Attribute(
                "ItemsSource"));
        var recentSection = Assert.Single(
            document.Descendants(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSection");
        Assert.Equal(
            "{Binding HasRecentlyPlayedGames, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)recentSection.Attribute("Visibility"));

        var headings =
            document
                .Descendants(
                    presentation + "TextBlock")
                .Select(
                    element =>
                        (string?)element.Attribute(
                            "Text"))
                .Where(
                    value =>
                        value is not null)
                .Cast<string>()
                .ToArray();

        Assert.DoesNotContain(headings, value => value == "Sessions en cours");
        Assert.Contains("Récemment joués", headings);
        Assert.DoesNotContain("Activité récente", headings);
    }

    [Fact]
    public void HomeView_active_hero_scrim_starts_at_left_edge_and_text_remains_padded()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var hero = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeHero");
        Assert.Equal("0", (string?)hero.Attribute("Padding"));
        var content = Assert.Single(hero.Elements(presentation + "Grid"),
            element => (string?)element.Attribute(xaml + "Name") == "HomeHeroContent");
        var scrim = Assert.Single(content.Elements(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "ActiveHeroReadabilityScrim");

        Assert.Equal("2", (string?)scrim.Attribute("Grid.ColumnSpan"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", (string?)scrim.Attribute("CornerRadius"));
        Assert.Null(scrim.Attribute("Margin"));
        Assert.Equal(
            "{Binding HasActiveSessionHero, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)scrim.Attribute("Visibility"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Background}", (string?)scrim.Attribute("Background"));
        Assert.Equal("0.9", (string?)scrim.Attribute("Opacity"));

        var mask = Assert.Single(scrim.Elements(presentation + "Border.OpacityMask"))
            .Element(presentation + "LinearGradientBrush");
        Assert.Equal("0,0.5", (string?)mask?.Attribute("StartPoint"));
        Assert.Equal("1,0.5", (string?)mask?.Attribute("EndPoint"));
        var stops = mask!.Elements(presentation + "GradientStop").ToArray();
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0" && (string?)stop.Attribute("Color") == "#FFFFFFFF");
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.2" && (string?)stop.Attribute("Color") == "#FFFFFFFF");
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.62" && (string?)stop.Attribute("Color") == "#00FFFFFF");

        var safeArea = Assert.Single(content.Elements(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "HeroTextSafeArea");
        Assert.Equal("0", (string?)safeArea.Attribute("Grid.Column"));
        Assert.Equal("{DynamicResource PlayStead.Spacing.4}", (string?)safeArea.Attribute("Margin"));
        Assert.Equal("520", (string?)safeArea.Attribute("MaxWidth"));
        Assert.Contains(safeArea.Elements(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding HeroEyebrow}");
        Assert.Contains(safeArea.Elements(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding HeroTitle}" &&
                       (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.PageTitle}");
        Assert.Contains(safeArea.Elements(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding HeroSupportingText}" &&
                       (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.BodySecondary}");

        Assert.Contains(hero.Descendants(presentation + "MultiDataTrigger"), trigger =>
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasActiveSessionHero}" &&
                (string?)condition.Attribute("Value") == "False") &&
            trigger.Descendants(presentation + "Condition").Any(condition =>
                (string?)condition.Attribute("Binding") == "{Binding HasIdleHeroImage}" &&
                (string?)condition.Attribute("Value") == "True") &&
            trigger.Descendants(presentation + "ImageBrush").Any(brush =>
                (string?)brush.Attribute("ImageSource") == "{Binding IdleHeroImageSource}"));
    }

    [Fact]
    public void HomeView_renders_recently_played_games_as_wrapping_landscape_cards()
    {
        var document = LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var recentGames = Assert.Single(
            document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");

        Assert.Equal("{Binding RecentlyPlayedGames}", (string?)recentGames.Attribute("ItemsSource"));
        Assert.Single(recentGames.Descendants(presentation + "WrapPanel"));
        Assert.Equal("Disabled", (string?)Assert.Single(document.Descendants(presentation + "ScrollViewer"))
            .Attribute("HorizontalScrollBarVisibility"));

        var template = Assert.Single(recentGames.Descendants(presentation + "DataTemplate"));
        var card = Assert.Single(template.Elements(presentation + "Border"));
        var artwork = Assert.Single(template.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedArtwork");

        Assert.Equal("275", (string?)card.Attribute("Width"));
        Assert.Null(card.Attribute("Style"));
        Assert.Null(card.Attribute("Background"));
        Assert.Null(card.Attribute("BorderBrush"));
        Assert.Null(card.Attribute("BorderThickness"));
        Assert.Null(card.Attribute("Padding"));
        Assert.Equal("{DynamicResource PlayStead.Spacing.1}", (string?)card.Attribute("Margin"));
        Assert.Equal("165", (string?)artwork.Attribute("Height"));
        Assert.Contains(artwork.Descendants(presentation + "Image"), element =>
            (string?)element.Attribute("Source") == "{Binding LandscapeMediaPath}" &&
            (string?)element.Attribute("Stretch") == "UniformToFill");
        Assert.DoesNotContain(template.Descendants(), element => element.Name.LocalName == "GameArtwork");
        Assert.Contains(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GameTitle}");
        Assert.Contains(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DisplayStartedAtLabel}");
        Assert.Contains(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DisplayDurationLabel}");
        Assert.NotNull(card);
        Assert.DoesNotContain(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Activité récente");
    }

    [Fact]
    public void HomeView_uses_landscape_media_with_integrated_dark_footer()
    {
        var document = LoadHomeView();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var recentGames = Assert.Single(document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");
        var template = Assert.Single(recentGames.Descendants(presentation + "DataTemplate"));
        var artwork = Assert.Single(template.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedArtwork");
        var image = Assert.Single(artwork.Descendants(presentation + "Image"));

        Assert.Equal("{Binding LandscapeMediaPath}", (string?)image.Attribute("Source"));
        Assert.Equal("UniformToFill", (string?)image.Attribute("Stretch"));
        Assert.DoesNotContain(template.Descendants(), element => element.Name.LocalName == "GameArtwork");
        Assert.Equal("{DynamicResource PlayStead.Brush.SurfaceSubtle}",
            (string?)artwork.Attribute("Background"));
        Assert.Contains(image.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasLandscapeMedia}" &&
            (string?)trigger.Attribute("Value") == "True");

        Assert.Equal("165", (string?)artwork.Attribute("Height"));
        var artworkStyle = artwork.Element(presentation + "Border.Style")!.Element(presentation + "Style")!;
        Assert.Equal("{DynamicResource PlayStead.Brush.Border}", (string?)artworkStyle
            .Elements(presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "BorderBrush")
            .Attribute("Value"));
        Assert.Equal("{DynamicResource PlayStead.Border.Thin}", (string?)artworkStyle
            .Elements(presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "BorderThickness")
            .Attribute("Value"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Small}", (string?)artwork.Attribute("CornerRadius"));
        Assert.Equal("True", (string?)artwork.Attribute("ClipToBounds"));

        var mediaGrid = Assert.Single(artwork.Elements(presentation + "Grid"));
        var gridClip = Assert.Single(mediaGrid.Elements(presentation + "Grid.Clip"))
            .Element(presentation + "RectangleGeometry");
        Assert.Equal("0,0,273,163", (string?)gridClip?.Attribute("Rect"));
        Assert.Equal("{Binding TopLeft, Source={StaticResource PlayStead.Radius.Small}}",
            (string?)gridClip?.Attribute("RadiusX"));
        Assert.Equal("{Binding TopLeft, Source={StaticResource PlayStead.Radius.Small}}",
            (string?)gridClip?.Attribute("RadiusY"));

        Assert.Empty(mediaGrid.Elements(presentation + "Grid.RowDefinitions"));
        Assert.Null(image.Attribute("Grid.Row"));
        Assert.Null(image.Element(presentation + "Image.Clip"));

        var overlay = Assert.Single(mediaGrid.Elements(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedInfoOverlay");
        Assert.Equal("50", (string?)overlay.Attribute("Height"));
        Assert.Equal("Bottom", (string?)overlay.Attribute("VerticalAlignment"));
        var overlayBackground = Assert.Single(overlay.Elements(presentation + "Rectangle"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedOverlayBackground");
        Assert.Equal("{DynamicResource PlayStead.Brush.Background}", (string?)overlayBackground.Attribute("Fill"));
        Assert.Equal("0.83", (string?)overlayBackground.Attribute("Opacity"));
        var textLayer = Assert.Single(overlay.Elements(presentation + "StackPanel"));
        Assert.Null(textLayer.Attribute("Opacity"));
        Assert.Contains(textLayer.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GameTitle}" &&
            (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.CardTitle}");
        Assert.Contains(textLayer.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DisplayStartedAtLabel}" &&
            (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.BodySecondary}");
        Assert.Contains(textLayer.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DisplayDurationLabel}" &&
            (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.BodySecondary}");
        Assert.Equal(275, int.Parse((string?)template.Elements(presentation + "Border")
            .First().Attribute("Width") ?? "0"));
        Assert.Empty(template.Descendants(presentation + "LinearGradientBrush"));
        Assert.DoesNotContain(template.Descendants(), element => element.Name.LocalName == "OpacityMask");
        var title = Assert.Single(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GameTitle}");
        Assert.Equal("{DynamicResource PlayStead.Text.CardTitle}", (string?)title.Attribute("Style"));
        Assert.Null(title.Attribute("Foreground"));
        Assert.Null(title.Attribute("FontWeight"));
        Assert.Null(title.Attribute("FontSize"));
        Assert.DoesNotContain(document.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding LibraryItem.CoverPath}");
        Assert.Contains(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GameTitle}");
        Assert.Contains(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DisplayStartedAtLabel}");
        Assert.Contains(template.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DisplayDurationLabel}");
    }

    private static void AssertGradient(XElement gradient, params (string? Color, string? Offset)[] expectedStops)
    {
        var stops = gradient.Elements(XName.Get("GradientStop", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))
            .Select(stop => ((string?)stop.Attribute("Color"), (string?)stop.Attribute("Offset")))
            .ToArray();
        Assert.Equal(expectedStops, stops);
    }

    [Fact]
    public void Home_recently_played_header_uses_shared_history_icon_and_semantic_subtitle()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var section = Assert.Single(document.Descendants(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSection");
        var header = Assert.Single(section.Elements(presentation + "Grid"));
        var icon = Assert.Single(header.Descendants(presentation + "Path"), element =>
            (string?)element.Attribute("Data") == "{StaticResource PlayStead.Icon.History}");
        var title = Assert.Single(header.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Récemment joués");
        var subtitle = Assert.Single(header.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Là où l’aventure continue.");

        Assert.Equal("{StaticResource PlayStead.Icon.History}", (string?)icon.Attribute("Data"));
        Assert.Equal("20", (string?)icon.Attribute("Width"));
        Assert.Equal("20", (string?)icon.Attribute("Height"));
        Assert.Equal("Center", (string?)icon.Attribute("VerticalAlignment"));
        Assert.Equal("{DynamicResource PlayStead.Brush.TextSecondary}", (string?)icon.Attribute("Stroke"));
        Assert.Equal("{DynamicResource PlayStead.Text.SectionTitle}", (string?)title.Attribute("Style"));
        Assert.Equal("{DynamicResource PlayStead.Text.Caption}", (string?)subtitle.Attribute("Style"));

        var sharedControls = XDocument.Load(FindUiFile("Themes/PlaySteadControls.xaml"));
        Assert.Contains(sharedControls.Descendants(presentation + "StreamGeometry"), geometry =>
            (string?)geometry.Attribute(xaml + "Key") == "PlayStead.Icon.History");
    }

    [Fact]
    public void Home_recently_played_header_exposes_sessions_see_all_action()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var section = Assert.Single(document.Descendants(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSection");
        var header = Assert.Single(section.Elements(presentation + "Grid"));
        var button = Assert.Single(header.Descendants(presentation + "Button"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSeeAllButton");
        Assert.Equal("{Binding NavigateSessionsCommand}", (string?)button.Attribute("Command"));
        Assert.Equal("{Binding HasRecentlyPlayedGames, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)button.Attribute("Visibility"));
        Assert.Equal("{DynamicResource PlayStead.Button.Ghost}", (string?)button.Attribute("Style"));
        Assert.Contains(button.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Tout voir");
        var chevron = Assert.Single(button.Descendants(presentation + "Path"));
        Assert.Equal("{StaticResource PlayStead.Icon.ChevronRight}", (string?)chevron.Attribute("Data"));

        var sharedControls = XDocument.Load(FindUiFile("Themes/PlaySteadControls.xaml"));
        Assert.Contains(sharedControls.Descendants(presentation + "StreamGeometry"), geometry =>
            (string?)geometry.Attribute(xaml + "Key") == "PlayStead.Icon.ChevronRight");

        var homeViewModel = File.ReadAllText(FindUiFile("Home/HomeViewModel.cs"));
        Assert.Contains("NavigateSessionsCommand", homeViewModel, StringComparison.Ordinal);
        Assert.Contains("new NavigationRequest(AppRoute.Sessions)", homeViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_recently_played_artwork_has_actual_rounded_image_and_content_clips()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var recentGames = Assert.Single(document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");
        var artwork = Assert.Single(recentGames.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedArtwork");
        var image = Assert.Single(artwork.Descendants(presentation + "Image"));
        Assert.Null(image.Attribute("Width"));

        var mediaGrid = Assert.Single(image.Parent!.Parent!.Elements(presentation + "Grid"));
        var gridClip = Assert.Single(mediaGrid.Elements(presentation + "Grid.Clip"))
            .Element(presentation + "RectangleGeometry");
        Assert.Equal("0,0,273,163", (string?)gridClip?.Attribute("Rect"));
        Assert.Equal(
            "{Binding TopLeft, Source={StaticResource PlayStead.Radius.Small}}",
            (string?)gridClip?.Attribute("RadiusX"));

        var tokens = XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));
        var radius = Assert.Single(tokens.Descendants(presentation + "CornerRadius"),
            element => (string?)element.Attribute(xaml + "Key") == "PlayStead.Radius.Small");
        Assert.Equal("8", radius.Value.Trim());
    }

    [Fact]
    public void Home_recently_played_cards_are_borderless_and_media_fills_card_bounds()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var recentGames = Assert.Single(document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");
        var template = Assert.Single(recentGames.Descendants(presentation + "DataTemplate"));
        var card = Assert.Single(template.Elements(presentation + "Border"));
        var artwork = Assert.Single(card.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedArtwork");

        Assert.Equal("275", (string?)card.Attribute("Width"));
        Assert.Null(card.Attribute("Style"));
        Assert.Null(card.Attribute("Background"));
        Assert.Null(card.Attribute("BorderBrush"));
        Assert.Null(card.Attribute("BorderThickness"));
        Assert.Null(card.Attribute("Padding"));
        Assert.Equal("{DynamicResource PlayStead.Spacing.1}", (string?)card.Attribute("Margin"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Small}", (string?)artwork.Attribute("CornerRadius"));
        var artworkStyle = artwork.Element(presentation + "Border.Style")!.Element(presentation + "Style")!;
        Assert.Equal("{DynamicResource PlayStead.Border.Thin}", (string?)artworkStyle
            .Elements(presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "BorderThickness")
            .Attribute("Value"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Border}", (string?)artworkStyle
            .Elements(presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "BorderBrush")
            .Attribute("Value"));
        Assert.Null(artwork.Attribute("Padding"));
        Assert.Equal("0,0,273,163",
            (string?)Assert.Single(artwork.Descendants(presentation + "Grid.Clip"))
                .Element(presentation + "RectangleGeometry")?
                .Attribute("Rect"));

        var tokens = XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));
        Assert.Equal("1", Assert.Single(tokens.Descendants(presentation + "Thickness"),
            element => (string?)element.Attribute(xaml + "Key") == "PlayStead.Border.Thin").Value.Trim());
        Assert.Equal("275", (string?)card.Attribute("Width"));
        Assert.Equal("165", (string?)artwork.Attribute("Height"));
    }

    [Fact]
    public void Home_recently_played_cards_expose_hover_and_keyboard_quick_actions()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var list = Assert.Single(document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");
        var template = Assert.Single(list.Descendants(presentation + "DataTemplate"));
        var card = Assert.Single(template.Elements(presentation + "Border"));

        Assert.Equal("True", (string?)card.Attribute("Focusable"));
        Assert.Equal("True", (string?)card.Attribute("KeyboardNavigation.IsTabStop"));
        Assert.Contains(card.Element(presentation + "Border.InputBindings")!.Elements(), binding =>
            binding.Name.LocalName == "MouseBinding" &&
            (string?)binding.Attribute("Command") == "{Binding DataContext.OpenRecentlyPlayedDetailsCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" &&
            (string?)binding.Attribute("CommandParameter") == "{Binding GameId}");
        var artwork = Assert.Single(card.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedArtwork");
        var artworkStyle = artwork.Element(presentation + "Border.Style")!.Element(presentation + "Style")!;
        Assert.Contains(artworkStyle.Elements(presentation + "Style.Triggers").Elements(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsMouseOver, ElementName=RecentlyPlayedCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "BorderBrush" &&
                (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Brush.Accent}"));
        Assert.Contains(artworkStyle.Elements(presentation + "Style.Triggers").Elements(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsKeyboardFocusWithin, ElementName=RecentlyPlayedCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "BorderBrush" &&
                (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Brush.Accent}"));

        var actions = Assert.Single(card.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedQuickActions");
        Assert.Equal("{DynamicResource PlayStead.Spacing.2}", (string?)actions.Attribute("Margin"));
        var actionStyle = actions.Element(presentation + "StackPanel.Style")!.Element(presentation + "Style")!;
        Assert.Equal("Collapsed", (string?)actionStyle.Elements(presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "Visibility").Attribute("Value"));
        Assert.Contains(actionStyle
            .Elements(presentation + "Style.Triggers").Elements(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsMouseOver, ElementName=RecentlyPlayedCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Visible"));
        Assert.Contains(actionStyle.Elements(presentation + "Style.Triggers").Elements(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsKeyboardFocusWithin, ElementName=RecentlyPlayedCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Visible"));

        var play = Assert.Single(actions.Descendants(presentation + "Button"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedPlayButton");
        Assert.Equal("32", (string?)play.Attribute("Width"));
        Assert.Equal("32", (string?)play.Attribute("Height"));
        Assert.Equal("{DynamicResource PlayStead.Gap.Inline.Small}", (string?)play.Attribute("Margin"));
        Assert.Equal("{DynamicResource PlayStead.Button.MediaAction}", (string?)play.Attribute("Style"));
        Assert.Equal("{Binding DataContext.PlayRecentlyPlayedCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}",
            (string?)play.Attribute("Command"));
        Assert.Equal("{Binding GameId}", (string?)play.Attribute("CommandParameter"));
        Assert.Contains(play.Descendants(presentation + "Path"), icon =>
            (string?)icon.Attribute("Data") == "{StaticResource PlayStead.Icon.Play}" &&
            (string?)icon.Attribute("Fill") == "{DynamicResource PlayStead.Brush.Accent}");

        var info = Assert.Single(actions.Descendants(presentation + "Button"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedInfoButton");
        Assert.Equal("32", (string?)info.Attribute("Width"));
        Assert.Equal("32", (string?)info.Attribute("Height"));
        Assert.Null(info.Attribute("Margin"));
        Assert.Equal("{DynamicResource PlayStead.Button.MediaAction}", (string?)info.Attribute("Style"));
        Assert.Equal("{Binding DataContext.OpenRecentlyPlayedDetailsCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}",
            (string?)info.Attribute("Command"));
        Assert.Equal("{Binding GameId}", (string?)info.Attribute("CommandParameter"));
        Assert.Contains(info.Descendants(presentation + "Path"), icon =>
            (string?)icon.Attribute("Data") == "{StaticResource PlayStead.Icon.Info}" &&
            (string?)icon.Attribute("Width") == "18" &&
            (string?)icon.Attribute("Height") == "18" &&
            (string?)icon.Attribute("Stroke") == "{DynamicResource PlayStead.Brush.TextPrimary}");
        Assert.Empty(info.Descendants(presentation + "Border"));

        var controls = XDocument.Load(FindUiFile("Themes/PlaySteadControls.xaml"));
        var mediaAction = Assert.Single(controls.Descendants(presentation + "Style"), style =>
            (string?)style.Attribute(xaml + "Key") == "PlayStead.Button.MediaAction");
        Assert.Equal("{DynamicResource PlayStead.Brush.SurfaceStrong}",
            (string?)mediaAction.Elements(presentation + "Setter").Single(setter =>
                (string?)setter.Attribute("Property") == "Background").Attribute("Value"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Border}",
            (string?)mediaAction.Elements(presentation + "Setter").Single(setter =>
                (string?)setter.Attribute("Property") == "BorderBrush").Attribute("Value"));
        Assert.Equal("{DynamicResource PlayStead.Border.Thin}",
            (string?)mediaAction.Elements(presentation + "Setter").Single(setter =>
                (string?)setter.Attribute("Property") == "BorderThickness").Attribute("Value"));
        foreach (var state in new[] { "IsMouseOver", "IsPressed", "IsKeyboardFocusWithin" })
        {
            Assert.Contains(mediaAction.Elements(presentation + "Style.Triggers").Elements(presentation + "Trigger"), trigger =>
                (string?)trigger.Attribute("Property") == state &&
                trigger.Elements(presentation + "Setter").Any(setter =>
                    (string?)setter.Attribute("Property") == "Background" &&
                    (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Brush.SurfaceStrong}") &&
                trigger.Elements(presentation + "Setter").Any(setter =>
                    (string?)setter.Attribute("Property") == "BorderBrush" &&
                    (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Brush.Accent}") &&
                trigger.Elements(presentation + "Setter").Any(setter =>
                    (string?)setter.Attribute("Property") == "BorderThickness" &&
                    (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Border.Thin}"));
        }

        Assert.Equal("{DynamicResource PlayStead.Border.Thin}", (string?)artworkStyle
            .Elements(presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "BorderThickness")
            .Attribute("Value"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Small}", (string?)artwork.Attribute("CornerRadius"));

        var spacingTokens = XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));
        Assert.Equal("0,0,8,0", Assert.Single(spacingTokens.Descendants(presentation + "Thickness"), token =>
            (string?)token.Attribute(xaml + "Key") == "PlayStead.Gap.Inline.Small").Value.Trim());
    }

    [Fact]
    public void HomeView_does_not_render_orphan_library_or_sessions_buttons()
    {
        var document =
            LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.DoesNotContain(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute(xaml + "Name") is "OpenLibraryButton" or "OpenSessionsButton");
        Assert.DoesNotContain(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Content") is "Bibliothèque" or "Sessions");
    }

    [Fact]
    public void HomeView_uses_existing_semantic_accent_resources_and_no_analytics_chart_surface()
    {
        var path =
            FindUiFile(
                "Home/HomeView.xaml");

        var source = File.ReadAllText(path);
        var document = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Contains(
            "PlayStead.Brush.Background",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "PlayStead.Brush.Surface",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "PlayStead.Brush.Accent",
            source,
            StringComparison.Ordinal);

        var visibleColorLiterals = document
            .Descendants()
            .Where(element => !element.AncestorsAndSelf().Any(ancestor =>
                ancestor.Name == presentation + "Rectangle.OpacityMask" ||
                ancestor.Name == presentation + "Border.OpacityMask"))
            .Attributes()
            .Where(attribute => attribute.Value.Contains('#'));
        Assert.Empty(visibleColorLiterals);

        Assert.DoesNotContain(
            "Chart",
            source,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "Graph",
            source,
            StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadHomeView()
    {
        return XDocument.Load(
            FindUiFile(
                "Home/HomeView.xaml"));
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
