using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Xml.Linq;
using PlayStead.UI.Home;

namespace PlayStead.UI.Tests.Home;

public sealed class HomeViewStructureTests
{
    [Fact]
    public void Suggestion_capability_uses_coop_icon_without_redundant_install_label()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var card = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionGameCard");

        var coopIcon = Assert.Single(card.Descendants(presentation + "Path"), element =>
            ((string?)element.Attribute("Data"))?.Contains("PlayStead.Icon.People", StringComparison.Ordinal) == true);
        Assert.Equal("24", (string?)coopIcon.Attribute("Width"));
        Assert.Equal("18", (string?)coopIcon.Attribute("Height"));
        Assert.Equal("{DynamicResource PlayStead.Brush.TextSecondary}", (string?)coopIcon.Attribute("Fill"));
        Assert.Equal("Center", (string?)coopIcon.Attribute("VerticalAlignment"));
        Assert.Contains(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionCapabilityText}" &&
            (string?)element.Attribute("Margin") == "8,0,0,0");
        Assert.DoesNotContain("Installé", card.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Small_home_cards_use_the_shared_actual_size_rounded_clip_behavior()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var gamesCard = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "GamesDuMomentCard");
        var gamesRoot = Assert.Single(gamesCard.Elements(presentation + "Grid"));
        Assert.Equal("True", AttachedAttribute(gamesRoot, "RoundedClipBehavior.IsEnabled"));

        var recentArtwork = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedArtwork");
        var recentRoot = Assert.Single(recentArtwork.Elements(presentation + "Grid"));
        Assert.Equal("True", AttachedAttribute(recentRoot, "RoundedClipBehavior.IsEnabled"));
        Assert.DoesNotContain(recentRoot.Descendants(presentation + "RectangleGeometry"), _ => true);
    }

    [Fact]
    public void Games_du_moment_cards_are_keyboard_focusable_and_retain_mouse_navigation()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var list = Assert.Single(document.Descendants(presentation + "ItemsControl"),
            element => (string?)element.Attribute(xaml + "Name") == "GamesDuMomentList");
        var card = Assert.Single(list.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "GamesDuMomentCard");

        Assert.Equal("True", (string?)card.Attribute("Focusable"));
        Assert.Equal("True", (string?)card.Attribute("KeyboardNavigation.IsTabStop"));

        var bindings = card.Element(presentation + "Border.InputBindings")!.Elements().ToArray();
        Assert.Contains(bindings, binding => binding.Name.LocalName == "MouseBinding" &&
            (string?)binding.Attribute("MouseAction") == "LeftClick");
        Assert.Contains(bindings, binding => binding.Name.LocalName == "KeyBinding" &&
            (string?)binding.Attribute("Key") == "Enter");
        Assert.Contains(bindings, binding => binding.Name.LocalName == "KeyBinding" &&
            (string?)binding.Attribute("Key") == "Space");

        var style = card.Element(presentation + "Border.Style")!.Element(presentation + "Style")!;
        var triggers = style.Element(presentation + "Style.Triggers")!.Elements(presentation + "DataTrigger");
        Assert.Contains(triggers, trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsMouseOver, ElementName=GamesDuMomentCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "BorderBrush"));
        Assert.Contains(triggers, trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsKeyboardFocusWithin, ElementName=GamesDuMomentCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "BorderBrush"));
    }

    [Fact]
    public void Games_du_moment_cards_expose_a_secondary_remove_action()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var card = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "GamesDuMomentCard");
        var actions = Assert.Single(card.Descendants(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "RemoveGamesDuMomentActions");
        var remove = Assert.Single(actions.Descendants(presentation + "Button"),
            element => (string?)element.Attribute(xaml + "Name") == "RemoveGamesDuMomentButton");

        Assert.Equal("Retirer des habitués", (string?)remove.Attribute("ToolTip"));
        Assert.Equal("Retirer des habitués", (string?)remove.Attribute("AutomationProperties.Name"));
        Assert.Equal("{Binding GameId}", (string?)remove.Attribute("CommandParameter"));
        Assert.Contains("RemoveGamesDuMomentCommand", (string?)remove.Attribute("Command"));
        Assert.Contains(remove.Descendants(presentation + "Path"), icon =>
            ((string?)icon.Attribute("Data"))?.Contains("PlayStead.Icon.Close", StringComparison.Ordinal) == true);
        Assert.Contains(actions.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding IsMouseOver, ElementName=GamesDuMomentCard}" &&
            trigger.Elements(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Visible"));
    }

    [Fact]
    public void Suggestion_editorial_card_has_real_and_placeholder_states()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var real = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionGameCard");
        Assert.Equal("0", (string?)real.Attribute("Grid.Column"));
        Assert.Equal("{DynamicResource HomeEditorialHeight}", (string?)real.Attribute("Height"));
        Assert.Contains(real.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionEditorialLine}");
        Assert.Contains(real.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionSupportingText}");
        Assert.Contains(real.Descendants(presentation + "Button"), element =>
            ((string?)element.Attribute("Command"))?.Contains("PlaySuggestionCommand", StringComparison.Ordinal) == true);
        Assert.Contains(real.Descendants(presentation + "Button"), element =>
            ((string?)element.Attribute("Command"))?.Contains("OpenSuggestionDetailsCommand", StringComparison.Ordinal) == true);

        var placeholder = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");
        Assert.Contains(placeholder.Descendants(presentation + "DataTrigger"), element =>
            (string?)element.Attribute("Value") == "True" &&
            ((string?)element.Attribute("Binding"))?.Contains("HasSuggestion", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(placeholder.Descendants(presentation + "Button"), _ => true);
    }

    [Fact]
    public void Suggestion_real_card_uses_left_readability_gradient_without_global_veil()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var card = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionGameCard");
        var gradient = Assert.Single(card.Descendants(presentation + "LinearGradientBrush"));
        Assert.Equal("0,0.5", (string?)gradient.Attribute("StartPoint"));
        Assert.Equal("1,0.5", (string?)gradient.Attribute("EndPoint"));
        Assert.True(gradient.Elements(presentation + "GradientStop").Count() >= 4);
        Assert.DoesNotContain(card.Descendants(presentation + "Border"), element =>
            element.Attribute("Opacity") is not null);
    }

    [Fact]
    public void Suggestion_real_card_uses_single_artwork_and_hardened_scrim_safe_zone()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var card = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionGameCard");
        Assert.DoesNotContain(card.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionTextSafeImage");
        Assert.DoesNotContain(card.Descendants(presentation + "BlurEffect"), _ => true);

        Assert.Contains(card.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute("MaxWidth") == "480");
        var suggestionOverlay = Assert.Single(card.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionReadabilityOverlay");
        var gradient = Assert.Single(suggestionOverlay.Descendants(presentation + "LinearGradientBrush"));
        var stops = gradient.Elements(presentation + "GradientStop").ToArray();
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0" && ((string?)stop.Attribute("Color"))?.StartsWith("#F2", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.4" && ((string?)stop.Attribute("Color"))?.StartsWith("#F2", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.48");
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.58");
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.68");
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.78" && ((string?)stop.Attribute("Color"))?.StartsWith("#00", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "1" && ((string?)stop.Attribute("Color"))?.StartsWith("#00", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Editorial_placeholder_is_an_independent_block_below_active_session()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var root = Assert.IsType<XElement>(document.Root);

        var active = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "ActiveSessionHero");
        var placeholder = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");

        Assert.True(root.Descendants().ToList().IndexOf(active) < root.Descendants().ToList().IndexOf(placeholder));
        Assert.Null(placeholder.Attribute("Visibility"));
        Assert.Equal("{DynamicResource HomeHeroHeight}", (string?)active.Attribute("Height"));
        var editorialRegion = Assert.Single(document.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeEditorialRegion");
        Assert.Equal("{DynamicResource HomeEditorialHeight}", (string?)editorialRegion.Attribute("Height"));
        Assert.Equal("Stretch", (string?)placeholder.Attribute("VerticalAlignment"));
        Assert.Equal("0", (string?)placeholder.Attribute("Margin"));
        Assert.Contains(placeholder.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "SUGGESTION DU MOMENT");
        Assert.Contains(placeholder.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Une place pour ta prochaine aventure");
        Assert.Contains(placeholder.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Je prépare encore une idée pour ta prochaine partie.");
        Assert.DoesNotContain(placeholder.Descendants(), element =>
            ((string?)element.Attribute("Text"))?.Contains("{Binding Editorial", StringComparison.Ordinal) == true ||
            ((string?)element.Attribute("Visibility"))?.Contains("HasPrimaryGame", StringComparison.Ordinal) == true);
        Assert.Empty(placeholder.Descendants(presentation + "Button"));
    }

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
            element => (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");

        Assert.True(
            root.Descendants().ToList().IndexOf(kpiRow) <
            root.Descendants().ToList().IndexOf(hero),
            "The compact KPI row should precede the existing Home hero.");

        var cards = kpiRow.Elements(presentation + "Border").ToArray();
        Assert.Equal(4, cards.Length);
        Assert.All(cards, card =>
        {
            Assert.Equal("250", (string?)card.Attribute("Width"));
            Assert.Equal("{DynamicResource PlayStead.Surface.Metric}", (string?)card.Attribute("Style"));
            Assert.Equal("{DynamicResource PlayStead.Spacing.1}", (string?)card.Attribute("Margin"));
            Assert.Equal("{DynamicResource PlayStead.Spacing.2}", (string?)card.Attribute("Padding"));
        });
        Assert.DoesNotContain(kpiRow.Descendants(), element => element.Name.LocalName == "KpiCard");
        Assert.DoesNotContain(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Sessions en cours");

        var card = cards[0];
        var value = Assert.Single(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding LibraryGameCount}");
        var label = Assert.Single(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Jeux installés");
        Assert.Equal("{DynamicResource PlayStead.Text.Display}", (string?)value.Attribute("Style"));
        Assert.Equal("{DynamicResource PlayStead.Text.BodySecondary}", (string?)label.Attribute("Style"));
        var gamepadPath = Assert.Single(card.Descendants(presentation + "Path"));
        Assert.Equal("{StaticResource PlayStead.Icon.Gamepad}", (string?)gamepadPath.Attribute("Data"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Accent}", (string?)gamepadPath.Attribute("Stroke"));
        var gamepadIcon = XDocument.Load(FindUiFile("Themes/PlaySteadControls.xaml"));
        Assert.Contains(gamepadIcon.Descendants(presentation + "StreamGeometry"), geometry =>
            (string?)geometry.Attribute(xaml + "Key") == "PlayStead.Icon.Gamepad");
        Assert.DoesNotContain(card.Descendants(presentation + "Ellipse"), tile =>
            ((string?)tile.Attribute("Fill"))?.Contains("Accent", StringComparison.Ordinal) == true);
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding WeeklyPlayTimeLabel}");
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding WeeklySummary.SessionCount}");
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GamesChangedSinceLastPlayCount}");
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding WeeklyPlayTimeTitle}");
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding WeeklySessionsTitle}");
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GamesChangedSinceLastPlayLabel}");
        Assert.Contains(kpiRow.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding GamesChangedSinceLastPlaySubtitle}");
        Assert.DoesNotContain(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("FontSize") is not null ||
            (string?)element.Attribute("FontWeight") is not null);

        var horizontalScroller = Assert.Single(document.Descendants(presentation + "ScrollViewer"));
        Assert.Equal("Disabled", (string?)horizontalScroller.Attribute("HorizontalScrollBarVisibility"));

        Assert.Contains(hero.Descendants(presentation + "ImageBrush"), image =>
            (string?)image.Attribute("ImageSource") == "{Binding EditorialPlaceholderImageSource}" &&
            (string?)image.Attribute("Stretch") == "UniformToFill");
        var heroText = hero.Descendants(presentation + "TextBlock").ToArray();
        Assert.Contains(heroText, element => (string?)element.Attribute("Text") == "SUGGESTION DU MOMENT");
        Assert.Contains(heroText, element => (string?)element.Attribute("Text") == "Une place pour ta prochaine aventure");
        Assert.Contains(heroText, element => (string?)element.Attribute("Text") == "Je prépare encore une idée pour ta prochaine partie.");
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
        Assert.Equal("0", (string?)hero.Attribute("Margin"));

        var homeViewModel = typeof(HomeViewModel);
        Assert.NotNull(homeViewModel.GetProperty("ActiveSessions"));

        var tokens = XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));
        Assert.Equal("0,16,0,0", Assert.Single(tokens.Descendants(presentation + "Thickness"), element =>
            (string?)element.Attribute(xaml + "Key") == "PlayStead.Gap.Block.Medium").Value.Trim());
    }

    [Fact]
    public void Editorial_placeholder_uses_fixed_banner_with_semantic_surface_fallback()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var placeholder = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");
        var style = Assert.Single(placeholder.Elements(presentation + "Border.Style"))
            .Element(presentation + "Style");
        Assert.Contains(style!.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Background" &&
            (string?)setter.Attribute("Value") == "{DynamicResource PlayStead.Brush.Surface}");

        var fixedBanner = Assert.Single(placeholder.Descendants(presentation + "ImageBrush"));
        Assert.Equal("{Binding EditorialPlaceholderImageSource}",
            (string?)fixedBanner.Attribute("ImageSource"));
        Assert.Equal("UniformToFill", (string?)fixedBanner.Attribute("Stretch"));
        Assert.DoesNotContain(placeholder.Descendants(), element =>
            ((string?)element.Attribute("Binding"))?.Contains("HasPrimaryGame", StringComparison.Ordinal) == true ||
            (string?)element.Attribute("ImageSource") is "{Binding HeroPath}" or "{Binding IdleHeroImageSource}");
    }
    [Fact]
    public void Editorial_placeholder_eyebrow_uses_accent_and_semibold()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var hero = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");
        var eyebrow = Assert.Single(hero.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "SUGGESTION DU MOMENT");
        var style = Assert.Single(eyebrow.Elements(presentation + "TextBlock.Style"))
            .Element(presentation + "Style");

        Assert.Equal("{StaticResource PlayStead.Text.Caption}", (string?)style?.Attribute("BasedOn"));
        Assert.Empty(style!.Descendants(presentation + "DataTrigger"));
        Assert.Equal("SUGGESTION DU MOMENT", (string?)eyebrow.Attribute("Text"));
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
            element => (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");
        Assert.Contains(hero.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "SUGGESTION DU MOMENT");
        Assert.Contains(hero.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Une place pour ta prochaine aventure");
        Assert.Contains(hero.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Je prépare encore une idée pour ta prochaine partie.");

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
        Assert.Null((string?)recentSection.Attribute("Visibility"));
        Assert.Equal(
            "{Binding HasRecentlyPlayedGames, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)recent.Attribute("Visibility"));

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
    public void HomeView_declares_games_du_moment_between_hero_and_recent_history()
    {
        var document = LoadHomeView();
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var root = Assert.IsType<XElement>(document.Root);
        var hero = Assert.Single(document.Descendants(p + "Border"), e => (string?)e.Attribute(x + "Name") == "EditorialPlaceholder");
        var section = Assert.Single(document.Descendants(p + "StackPanel"), e => (string?)e.Attribute(x + "Name") == "GamesDuMomentSection");
        var recent = Assert.Single(document.Descendants(p + "StackPanel"), e => (string?)e.Attribute(x + "Name") == "RecentlyPlayedSection");
        var all = root.Descendants().ToList();
        Assert.True(all.IndexOf(hero) < all.IndexOf(section));
        Assert.True(all.IndexOf(section) < all.IndexOf(recent));
        Assert.Null((string?)section.Attribute("Visibility"));
        var list = Assert.Single(section.Descendants(p + "ItemsControl"));
        Assert.Equal("{Binding HasGamesDuMoment, Converter={StaticResource BooleanToVisibilityConverter}}", (string?)list.Attribute("Visibility"));
        Assert.Equal("{Binding GamesDuMoment}", (string?)list.Attribute("ItemsSource"));
        Assert.Contains(section.Descendants(p + "TextBlock"), e => (string?)e.Attribute("Text") == "Les habitués");
        Assert.Contains(section.Descendants(p + "TextBlock"), e =>
            (string?)e.Attribute("Text") == "On sait très bien que tu vas y retourner.");
        var gameTemplate = Assert.Single(list.Descendants(p + "DataTemplate"));
        var gameCard = Assert.Single(gameTemplate.Descendants(p + "Border"), e => (string?)e.Attribute("Height") == "135");
        Assert.Equal("True", (string?)gameCard.Attribute("ClipToBounds"));
        var gameScrim = Assert.Single(gameCard.Descendants(p + "LinearGradientBrush"));
        Assert.Equal("0,0", (string?)gameScrim.Attribute("StartPoint"));
        Assert.Equal("0,1", (string?)gameScrim.Attribute("EndPoint"));
        Assert.Contains(gameScrim.Elements(p + "GradientStop"), e => (string?)e.Attribute("Offset") == "1");
    }

    [Fact]
    public void Active_session_scrim_starts_at_left_edge_and_text_remains_padded()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var active = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "ActiveSessionHero");
        var content = Assert.Single(active.Elements(presentation + "Grid"));
        var scrim = Assert.Single(content.Elements(presentation + "Border"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", (string?)scrim.Attribute("CornerRadius"));
        Assert.Null(scrim.Attribute("Margin"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Background}", (string?)scrim.Attribute("Background"));
        Assert.Equal("0.9", (string?)scrim.Attribute("Opacity"));

        var mask = Assert.Single(scrim.Elements(presentation + "Border.OpacityMask"))
            .Element(presentation + "LinearGradientBrush");
        Assert.Equal("0,0.5", (string?)mask?.Attribute("StartPoint"));
        Assert.Equal("1,0.5", (string?)mask?.Attribute("EndPoint"));
        var stops = mask!.Elements(presentation + "GradientStop").ToArray();
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0" && (string?)stop.Attribute("Color") == "#FFFFFFFF");
        Assert.Contains(stops, stop => (string?)stop.Attribute("Offset") == "0.62" && (string?)stop.Attribute("Color") == "#00FFFFFF");

        var safeArea = Assert.Single(content.Elements(presentation + "StackPanel"));
        Assert.Equal("{DynamicResource PlayStead.Spacing.4}", (string?)safeArea.Attribute("Margin"));
        Assert.Equal("520", (string?)safeArea.Attribute("MaxWidth"));
        Assert.Contains(safeArea.Elements(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding ActiveSessionEyebrow}");
        Assert.Contains(safeArea.Elements(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding ActiveSessionTitle}" &&
                       (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.PageTitle}");
    }
    [Fact]
    public void Build_change_context_is_bound_only_as_optional_context()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var suggestion = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "SuggestionGameCard");
        var dormant = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "DormantGameCard");

        Assert.Contains(suggestion.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute("Visibility") == "{Binding HasSuggestionBuildChanges, Converter={StaticResource BooleanToVisibilityConverter}}");
        Assert.Contains(suggestion.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionBuildChangeText}");
        Assert.Contains(suggestion.Descendants(presentation + "Button"), element =>
            (string?)element.Attribute("Content") == "Voir les changements" &&
            (string?)element.Attribute("Command") == "{Binding OpenSuggestionChangesCommand}");
        Assert.Contains(dormant.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute("Visibility") == "{Binding HasDormantBuildChanges, Converter={StaticResource BooleanToVisibilityConverter}}");
        Assert.Contains(dormant.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DormantBuildChangeText}");
        Assert.Contains(dormant.Descendants(presentation + "Button"), element =>
            (string?)element.Attribute("Content") == "Voir les changements" &&
            (string?)element.Attribute("Command") == "{Binding OpenDormantChangesCommand}");
    }

    [Fact]
    public void Editorial_placeholder_has_fixed_copy_without_primary_game_actions()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var card = Assert.Single(document.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");
        Assert.Contains(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "SUGGESTION DU MOMENT");
        Assert.Contains(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Une place pour ta prochaine aventure");
        Assert.Contains(card.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Je prépare encore une idée pour ta prochaine partie.");
        Assert.Empty(card.Descendants(presentation + "Button"));
        Assert.DoesNotContain(card.Descendants(), element =>
            ((string?)element.Attribute("Text"))?.Contains("{Binding Editorial", StringComparison.Ordinal) == true);

        Assert.DoesNotContain(document.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "WeeklyActivitySection");
        Assert.Contains(document.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionSection");
        Assert.Contains(document.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSection");
    }
    [Fact]
    public void Home_editorial_region_composes_dominant_primary_and_optional_secondary_context_card()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace sys = "clr-namespace:System;assembly=System.Runtime";

        var region = Assert.Single(document.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeEditorialRegion");
        var columns = Assert.Single(region.Elements(presentation + "Grid.ColumnDefinitions"))
            .Elements(presentation + "ColumnDefinition")
            .ToArray();
        Assert.Equal(3, columns.Length);
        Assert.Equal("13*", (string?)columns[0].Attribute("Width"));
        Assert.Equal("16", (string?)columns[1].Attribute("Width"));
        Assert.Equal("7*", (string?)columns[2].Attribute("Width"));

        var primary = Assert.Single(region.Elements(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "EditorialPlaceholder");
        var secondary = Assert.Single(region.Elements(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "DormantGameCard");
        var fallback = Assert.Single(region.Elements(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "DormantGamePlaceholder");
        Assert.Equal("0", (string?)primary.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)secondary.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)fallback.Attribute("Grid.Column"));
        Assert.Equal("Stretch", (string?)primary.Attribute("VerticalAlignment"));
        Assert.Equal("Stretch", (string?)secondary.Attribute("VerticalAlignment"));
        Assert.Equal("Stretch", (string?)fallback.Attribute("VerticalAlignment"));
        Assert.Equal("0", (string?)primary.Attribute("Margin"));
        Assert.Equal("0", (string?)secondary.Attribute("Margin"));
        Assert.Equal("{DynamicResource HomeEditorialHeight}", (string?)region.Attribute("Height"));
        var resources = Assert.Single(document.Descendants(sys + "Double"), element =>
            (string?)element.Attribute(xaml + "Key") == "HomeEditorialHeight");
        Assert.Equal("320", resources.Value);
        Assert.Equal(
            "{Binding HasDormantGame, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)secondary.Attribute("Visibility"));

        Assert.Contains(secondary.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "ÇA FAIT UN BAIL…");
        Assert.Contains(secondary.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DormantGameTitle}");
        var dormantTitle = Assert.Single(secondary.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DormantGameTitle}");
        Assert.Equal("0,6,0,0", (string?)dormantTitle.Attribute("Margin"));
        var dormantContext = Assert.Single(secondary.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DormantGameContext}");
        Assert.Equal("8,0,0,0", (string?)dormantContext.Attribute("Margin"));
        Assert.Equal("Center", (string?)dormantContext.Attribute("VerticalAlignment"));
        var dormantDescription = Assert.Single(secondary.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DormantGameDescription}");
        Assert.Equal("{Binding HasDormantGameDescription, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)dormantDescription.Attribute("Visibility"));
        Assert.Equal("40", (string?)dormantDescription.Attribute("MaxHeight"));
        Assert.Equal("320", (string?)dormantDescription.Attribute("MaxWidth"));
        Assert.Equal("0,16,0,0", (string?)dormantDescription.Attribute("Margin"));
        Assert.Equal("Wrap", (string?)dormantDescription.Attribute("TextWrapping"));
        Assert.Equal("CharacterEllipsis", (string?)dormantDescription.Attribute("TextTrimming"));
        var dormantClock = Assert.Single(secondary.Descendants(presentation + "Path"), element =>
            (string?)element.Attribute("Data") == "{DynamicResource PlayStead.Icon.History}" &&
            (string?)element.Attribute("Stroke") == "{DynamicResource PlayStead.Brush.TextSecondary}");
        var dormantContextRow = dormantClock.Parent;
        Assert.Equal("0,8,0,0", (string?)dormantContextRow?.Attribute("Margin"));
        Assert.Equal("18", (string?)dormantClock.Attribute("Width"));
        Assert.Equal("18", (string?)dormantClock.Attribute("Height"));
        Assert.Equal("Uniform", (string?)dormantClock.Attribute("Stretch"));
        Assert.Contains(secondary.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding DormantGameContext}");
        var suggestionCard = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "SuggestionGameCard");
        Assert.Contains(suggestionCard.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionEditorialLine}" &&
            (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Text.PageTitle}" &&
            element.Attribute("TextTrimming") is null);
        Assert.Contains(suggestionCard.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionDescription}" &&
            (string?)element.Attribute("MaxHeight") == "40" &&
            (string?)element.Attribute("TextWrapping") == "Wrap" &&
            (string?)element.Attribute("TextTrimming") == "CharacterEllipsis");
        Assert.Contains(suggestionCard.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionGameIdentity}");
        Assert.Contains(suggestionCard.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionCapabilityText}");
        Assert.Contains(suggestionCard.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionEditorialLine}");
        Assert.Contains(suggestionCard.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SuggestionUpdateStatusText}");
        Assert.Contains(suggestionCard.Descendants(presentation + "StackPanel"), element =>
            ((string?)element.Attribute("Visibility"))?.Contains("HasSuggestionUpdate", StringComparison.Ordinal) == true);
        Assert.Contains(secondary.Descendants(presentation + "ImageBrush"), element =>
            (string?)element.Attribute("ImageSource") == "{Binding DormantGameMediaPath}" &&
            (string?)element.Attribute("Stretch") == "UniformToFill");
        var dormantOverlay = Assert.Single(secondary.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "DormantGameReadabilityOverlay");
        var dormantGradient = Assert.Single(dormantOverlay.Descendants(presentation + "LinearGradientBrush"));
        var dormantStops = dormantGradient.Elements(presentation + "GradientStop").ToArray();
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "0" && ((string?)stop.Attribute("Color"))?.StartsWith("#F2", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "0.4" && ((string?)stop.Attribute("Color"))?.StartsWith("#F2", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "0.48");
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "0.58");
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "0.68");
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "0.78" && ((string?)stop.Attribute("Color"))?.StartsWith("#00", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(dormantStops, stop => (string?)stop.Attribute("Offset") == "1" && ((string?)stop.Attribute("Color"))?.StartsWith("#00", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Empty(secondary.Descendants(presentation + "BlurEffect"));
        Assert.Contains(secondary.Descendants(presentation + "Button"), action =>
            (string?)action.Attribute("Content") == "Voir le jeu" &&
            (string?)action.Attribute("Command") == "{Binding OpenDormantGameDetailsCommand}" &&
            (string?)action.Attribute("Margin") == "0,20,0,0" &&
            (string?)action.Attribute("Style") == "{DynamicResource PlayStead.Button.Secondary}");
        Assert.Contains(secondary.Descendants(presentation + "Button"), action =>
            (string?)action.Attribute("Content") == "Voir les changements" &&
            (string?)action.Attribute("Command") == "{Binding OpenDormantChangesCommand}");
        Assert.Contains(fallback.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "ÇA FAIT UN BAIL…");
        Assert.Contains(fallback.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasDormantGame}" &&
            (string?)trigger.Attribute("Value") == "True");
    }

    [Fact]
    public void Active_session_block_precedes_editorial_region_and_exposes_only_safe_detail_action()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var root = Assert.IsType<XElement>(document.Root);
        var active = Assert.Single(document.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "ActiveSessionHero");
        var editorial = Assert.Single(document.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeEditorialRegion");

        Assert.True(root.Descendants().ToList().IndexOf(active) < root.Descendants().ToList().IndexOf(editorial));
        Assert.Equal(
            "{Binding HasActiveSessionHero, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)active.Attribute("Visibility"));
        Assert.Equal("{DynamicResource HomeHeroHeight}", (string?)active.Attribute("Height"));
        Assert.Contains(active.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding ActiveSessionEyebrow}");
        Assert.Contains(active.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding ActiveSessionTitle}");
        Assert.Contains(active.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding ActiveSessionSupportingText}");
        var action = Assert.Single(active.Descendants(presentation + "Button"));
        Assert.Equal("Voir le jeu", (string?)action.Attribute("Content"));
        Assert.Equal("{Binding OpenActiveSessionDetailsCommand}", (string?)action.Attribute("Command"));
        Assert.DoesNotContain(active.Descendants(presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "Jouer");
    }

    [Fact]
    public void Home_editorial_region_declares_existing_responsive_size_changed_contract()
    {
        var document = LoadHomeView();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.Equal("HomeView_OnSizeChanged", (string?)document.Root?.Attribute("SizeChanged"));
        Assert.Contains(document.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeEditorialRegion");
        Assert.Contains(document.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "DormantGameCard");
    }

    [Fact]
    public void Home_low_data_sections_use_distinct_onboarding_collapse_and_quiet_zero_strategies()
    {
        var document = LoadHomeView();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var gamesSection = Assert.Single(document.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute(xaml + "Name") == "GamesDuMomentSection");
        Assert.Null(gamesSection.Attribute("Visibility"));
        var gamesList = Assert.Single(gamesSection.Descendants(presentation + "ItemsControl"), element =>
            (string?)element.Attribute(xaml + "Name") == "GamesDuMomentList");
        Assert.Equal(
            "{Binding HasGamesDuMoment, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)gamesList.Attribute("Visibility"));
        var onboarding = Assert.Single(gamesSection.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "GamesDuMomentOnboarding");
        Assert.Equal(
            "{Binding HasNoGamesDuMoment, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)onboarding.Attribute("Visibility"));
        Assert.Contains(onboarding.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Tes habitués n’attendent que toi.");
        Assert.Contains(onboarding.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Ajoute ceux que tu veux garder sous la main.");
        var libraryAction = Assert.Single(onboarding.Descendants(presentation + "Button"));
        Assert.Equal("Parcourir la bibliothèque", (string?)libraryAction.Attribute("Content"));
        Assert.Equal("{Binding NavigateLibraryCommand}", (string?)libraryAction.Attribute("Command"));

        Assert.DoesNotContain(document.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute(xaml + "Name") == "WeeklyActivitySection");

        var attention = Assert.Single(document.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionSection");
        Assert.Null(attention.Attribute("Visibility"));
        var attentionStrip = Assert.Single(attention.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionStrip");
        Assert.Contains(attentionStrip.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasHomeAttention}" &&
            (string?)trigger.Attribute("Value") == "False");
        Assert.Contains(attentionStrip.Descendants(presentation + "Path"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionSectionIcon");
        Assert.Contains(attentionStrip.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Quelques éléments méritent ton attention.");
        var attentionItems = Assert.Single(attentionStrip.Descendants(presentation + "ItemsControl"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionItemsList");
        Assert.Equal("{Binding HomeAttentionItems}", (string?)attentionItems.Attribute("ItemsSource"));
        var attentionSeeAll = Assert.Single(attentionStrip.Descendants(presentation + "Button"));
        Assert.Equal("Tout voir", (string?)attentionSeeAll.Attribute("Content"));
        Assert.Equal("{Binding NavigateAttentionCommand}", (string?)attentionSeeAll.Attribute("Command"));
        var attentionStripContainer = Assert.Single(attention.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionStripContainer");
        Assert.Contains(attentionStripContainer.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasHomeAttention}");
        var attentionPanel = Assert.Single(attentionItems.Descendants(presentation + "WrapPanel"));
        Assert.Equal("260", (string?)attentionPanel.Attribute("ItemWidth"));
        var attentionThumbnail = Assert.Single(attentionItems.Descendants(presentation + "Image"));
        Assert.Equal("{Binding MediaPath}", (string?)attentionThumbnail.Attribute("Source"));
        Assert.Contains(attentionItems.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute("Width") == "96" &&
            (string?)element.Attribute("Height") == "96");
        var attentionItemBorder = Assert.Single(
            attentionItems.Descendants(presentation + "DataTemplate").Descendants(presentation + "Border"),
            element => (string?)element.Attribute("VerticalAlignment") == "Center");
        Assert.Null(attentionItemBorder.Attribute("MinHeight"));
        Assert.Equal("Center", (string?)attentionItemBorder.Attribute("VerticalAlignment"));
        Assert.Equal("True", (string?)attentionItemBorder.Attribute("Focusable"));
        Assert.Equal("True", (string?)attentionItemBorder.Attribute("KeyboardNavigation.IsTabStop"));
        Assert.Contains(attentionItemBorder.Element(presentation + "Border.InputBindings")!.Elements(), binding =>
            binding.Name.LocalName == "KeyBinding" && (string?)binding.Attribute("Key") == "Enter");
        Assert.Contains(attentionItemBorder.Element(presentation + "Border.InputBindings")!.Elements(), binding =>
            binding.Name.LocalName == "KeyBinding" && (string?)binding.Attribute("Key") == "Space");
        Assert.Contains(attentionThumbnail.Descendants(presentation + "RectangleGeometry"), geometry =>
            (string?)geometry.Attribute("Rect") == "0,0,96,96");
        Assert.DoesNotContain(attentionItems.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute("Style") == "{DynamicResource PlayStead.Surface.CardNested}");
        var fallbackIcon = Assert.Single(attentionItems.Descendants(presentation + "Path"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionItemFallbackIcon");
        Assert.Null(fallbackIcon.Attribute("Visibility"));
        Assert.Contains(fallbackIcon.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasMedia}" &&
            (string?)trigger.Attribute("Value") == "True");
        var attentionEmpty = Assert.Single(attention.Descendants(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "HomeAttentionEmptyState");
        Assert.True((string?)attentionEmpty.Attribute(xaml + "Column") is null or "1");
        Assert.Contains(attentionEmpty.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Tout roule. Rien ne demande ton attention.");
        Assert.Contains(attentionEmpty.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Les éléments importants apparaîtront ici.");
        Assert.Contains(attentionItems.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("TextTrimming") == "CharacterEllipsis");

        var recent = Assert.Single(document.Descendants(presentation + "StackPanel"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedSection");
        Assert.Null(recent.Attribute("Visibility"));
        var recentList = Assert.Single(recent.Descendants(presentation + "ItemsControl"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedGamesList");
        Assert.Equal(
            "{Binding HasRecentlyPlayedGames, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)recentList.Attribute("Visibility"));
        var recentEmpty = Assert.Single(recent.Descendants(presentation + "Border"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedEmptyState");
        Assert.Equal(
            "{Binding HasNoRecentlyPlayedGames, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)recentEmpty.Attribute("Visibility"));
        Assert.Contains(recentEmpty.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "L’histoire commence à ta prochaine partie.");
        Assert.Contains(recentEmpty.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "Tes dernières sessions apparaîtront ici.");

        var source = File.ReadAllText(FindUiFile("Home/HomeView.xaml"));
        Assert.DoesNotContain("SuggestedGame", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Recommendation", source, StringComparison.Ordinal);
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
        Assert.Equal("145", (string?)artwork.Attribute("Height"));
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

        Assert.Equal("145", (string?)artwork.Attribute("Height"));
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
        Assert.Equal("True", AttachedAttribute(mediaGrid, "RoundedClipBehavior.IsEnabled"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Small}",
            AttachedAttribute(mediaGrid, "RoundedClipBehavior.CornerRadius"));

        Assert.Empty(mediaGrid.Elements(presentation + "Grid.RowDefinitions"));
        Assert.Null(image.Attribute("Grid.Row"));
        Assert.Null(image.Element(presentation + "Image.Clip"));

        var overlay = Assert.Single(mediaGrid.Elements(presentation + "Grid"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedInfoOverlay");
        Assert.Equal("72", (string?)overlay.Attribute("Height"));
        Assert.Equal("Bottom", (string?)overlay.Attribute("VerticalAlignment"));
        var overlayBackground = Assert.Single(overlay.Elements(presentation + "Rectangle"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedOverlayBackground");
        var recentScrim = Assert.Single(overlayBackground.Elements(presentation + "Rectangle.Fill")).Element(presentation + "LinearGradientBrush");
        Assert.Equal("0,0", (string?)recentScrim?.Attribute("StartPoint"));
        Assert.Equal("0,1", (string?)recentScrim?.Attribute("EndPoint"));
        Assert.Contains(recentScrim!.Elements(presentation + "GradientStop"), e => (string?)e.Attribute("Offset") == "1");
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
        Assert.Contains(textLayer.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding ProviderLabel}");
        Assert.DoesNotContain(textLayer.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding InstalledSizeLabel}");
        Assert.Contains(textLayer.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding StatusLabel}");
        Assert.Equal(275, int.Parse((string?)template.Elements(presentation + "Border")
            .First().Attribute("Width") ?? "0"));
        var recentFooterScrim = Assert.Single(template.Descendants(presentation + "LinearGradientBrush"));
        Assert.Equal("0,0", (string?)recentFooterScrim.Attribute("StartPoint"));
        Assert.Equal("0,1", (string?)recentFooterScrim.Attribute("EndPoint"));
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
        Assert.Equal("True", AttachedAttribute(mediaGrid, "RoundedClipBehavior.IsEnabled"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Small}",
            AttachedAttribute(mediaGrid, "RoundedClipBehavior.CornerRadius"));

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
        var mediaGrid = Assert.Single(artwork.Elements(presentation + "Grid"));
        Assert.Equal("True", AttachedAttribute(mediaGrid, "RoundedClipBehavior.IsEnabled"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Small}",
            AttachedAttribute(mediaGrid, "RoundedClipBehavior.CornerRadius"));

        var tokens = XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));
        Assert.Equal("1", Assert.Single(tokens.Descendants(presentation + "Thickness"),
            element => (string?)element.Attribute(xaml + "Key") == "PlayStead.Border.Thin").Value.Trim());
        Assert.Equal("275", (string?)card.Attribute("Width"));
        Assert.Equal("145", (string?)artwork.Attribute("Height"));
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

        Assert.Single(actions.Descendants(presentation + "Button"));
        Assert.DoesNotContain(actions.Descendants(presentation + "Button"), element =>
            (string?)element.Attribute(xaml + "Name") == "RecentlyPlayedInfoButton");

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
                ancestor.Name == presentation + "Border.OpacityMask" ||
                ancestor.Name == presentation + "LinearGradientBrush" ||
                ancestor.Attribute("{http://schemas.microsoft.com/winfx/2006/xaml}Name")?.Value is
                    "DormantGameReadabilityOverlay" or "SuggestionReadabilityOverlay"))
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

    private static string? AttachedAttribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

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
