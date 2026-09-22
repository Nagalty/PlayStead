using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PlayStead.Core.Library;
using PlayStead.Core.Steam;
using PlayStead.UI.Controls;
using PlayStead.UI.Library;
using PlayStead.UI.Tests.TestSupport;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class LibraryPolishStructureTests
{
    [Fact]
    public void Library_header_keeps_handlers_and_presents_a_selected_segment_and_quiet_utility()
    {
        var document = LoadUi("Library/LibraryView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var heading = FindNamed(document, "LibraryHeading");
        Assert.Equal("{DynamicResource PlayStead.Text.PageTitle}", Attr(heading, "Style"));

        var viewModeStyle = document.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "Library.ViewModeButton");
        Assert.Equal("{StaticResource PlayStead.Button.Ghost}", Attr(viewModeStyle, "BasedOn"));
        Assert.Contains(viewModeStyle.Descendants(presentation + "MultiDataTrigger"), trigger =>
            HasCondition(trigger, "{Binding IsGridMode}", "True") &&
            HasCondition(trigger, "{Binding RelativeSource={RelativeSource Self}, Path=Tag}", "Grid"));
        Assert.Contains(viewModeStyle.Descendants(presentation + "MultiDataTrigger"), trigger =>
            HasCondition(trigger, "{Binding IsListMode}", "True") &&
            HasCondition(trigger, "{Binding RelativeSource={RelativeSource Self}, Path=Tag}", "List"));

        Assert.Equal("GridModeButton_OnClick", Attr(FindNamed(document, "GridModeButton"), "Click"));
        Assert.Equal("Grid", Attr(FindNamed(document, "GridModeButton"), "Tag"));
        Assert.Equal("ListModeButton_OnClick", Attr(FindNamed(document, "ListModeButton"), "Click"));
        Assert.Equal("List", Attr(FindNamed(document, "ListModeButton"), "Tag"));
        Assert.Equal("VerifySteamButton_OnClick", Attr(FindNamed(document, "VerifySteamButton"), "Click"));
        Assert.Equal("{Binding CanVerifySteam}", Attr(FindNamed(document, "VerifySteamButton"), "IsEnabled"));
        var verifyStyleKey = Attr(FindNamed(document, "VerifySteamButton"), "Style")
            ?.Replace("{DynamicResource ", string.Empty, StringComparison.Ordinal)
            .TrimEnd('}');
        var verifyStyle = document.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == verifyStyleKey);
        Assert.Equal("{StaticResource PlayStead.Button.Ghost}", Attr(verifyStyle, "BasedOn"));
    }

    [Fact]
    public void Library_game_card_keeps_metadata_order_and_quiets_only_unknown_status()
    {
        var document = LoadUi("Controls/GameCard.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace steam = "clr-namespace:PlayStead.Core.Steam;assembly=PlayStead.Core";

        var title = Assert.Single(document.Descendants(presentation + "TextBlock"),
            element => Attr(element, "Text") == "{Binding Title}");
        Assert.Equal("{DynamicResource PlayStead.Text.CardTitle}", Attr(title, "Style"));
        Assert.Equal("{Binding Title}", Attr(title, "ToolTip"));
        Assert.Equal("CharacterEllipsis", Attr(title, "TextTrimming"));
        Assert.Equal("0", Attr(title, "Grid.Row"));

        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => Attr(element, "Text") == "{Binding ProviderLabel}" &&
                       Attr(element, "Style") == "{DynamicResource PlayStead.Text.BodySecondary}");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => Attr(element, "Text") == "{Binding InstalledSizeLabel, Mode=OneWay}" &&
                       Attr(element, "Style") == "{DynamicResource PlayStead.Text.Caption}");
        var sessionStatus = Assert.Single(document.Descendants(),
            element => Attr(element, "Text") == "{Binding SessionStatusLabel}");
        Assert.Contains(sessionStatus.Ancestors(), ancestor => Attr(ancestor, "Grid.Row") == "2");

        var badgeStyle = document.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "Library.SteamStatusBadge");
        Assert.Equal("{StaticResource PlayStead.Status.Badge.Neutral}", Attr(badgeStyle, "BasedOn"));
        Assert.Contains(badgeStyle.Descendants(presentation + "Setter"), setter =>
            Attr(setter, "Property") == "Visibility" &&
            Attr(setter, "Value") == "{Binding HasSteamStatus, Converter={StaticResource BooleanToVisibilityConverter}}");
        Assert.Contains(badgeStyle.Descendants(presentation + "DataTrigger"), trigger =>
            Attr(trigger, "Binding") == "{Binding SteamState}" &&
            Attr(trigger, "Value") == "{x:Static steam:SteamUpdateState.Unknown}" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                Attr(setter, "Property") == "Background" &&
                Attr(setter, "Value") == "{DynamicResource PlayStead.Brush.SurfaceSubtle}") &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                Attr(setter, "Property") == "Visibility" && Attr(setter, "Value") == "Collapsed"));
        var badge = Assert.Single(document.Descendants(presentation + "Border"), element =>
            Attr(element, "Style") == "{DynamicResource Library.SteamStatusBadge}");
        Assert.Null(Attr(badge, "Visibility"));

        var statusTextStyle = document.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "Library.SteamStatusText");
        Assert.Equal("{StaticResource PlayStead.Text.Caption}", Attr(statusTextStyle, "BasedOn"));
        Assert.Contains(statusTextStyle.Descendants(presentation + "DataTrigger"), trigger =>
            Attr(trigger, "Binding") == "{Binding SteamState}" &&
            Attr(trigger, "Value") == "{x:Static steam:SteamUpdateState.Unknown}" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                Attr(setter, "Property") == "Foreground" &&
                Attr(setter, "Value") == "{DynamicResource PlayStead.Brush.TextMuted}"));

        Assert.Equal("GameCard_OnMouseLeftButtonUp", Attr(document.Root!, "MouseLeftButtonUp"));
        Assert.Equal("0,0,12,12", Attr(document.Root!, "Margin"));

        var libraryView = LoadUi("Library/LibraryView.xaml");
        var libraryCard = Assert.Single(libraryView.Descendants(), element => element.Name.LocalName == "GameCard");
        Assert.Equal("GameCard_OnSelectionRequested", Attr(libraryCard, "SelectionRequested"));
        Assert.Equal("GameCard_OnDetailsRequested", Attr(libraryCard, "DetailsRequested"));
        Assert.Equal("GameCard_OnMediaRequested", Attr(libraryCard, "MediaRequested"));
    }

    [Fact]
    public void Library_cover_frame_hugs_the_media_and_uses_shared_rounded_clipping()
    {
        var document = LoadUi("Controls/GameCard.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace controls = "clr-namespace:PlayStead.UI.Controls";

        var root = document.Root!;
        Assert.Equal("220", Attr(root, "Width"));

        var cardShell = FindNamed(document, "CardShell");
        Assert.Equal("{DynamicResource PlayStead.Surface.Card}", Attr(cardShell, "Style"));
        var mediaRow = cardShell.Descendants(presentation + "RowDefinition")
            .First(element => Attr(element, "Height") == "300");
        Assert.NotNull(mediaRow);

        var mediaFrame = FindNamed(document, "MediaFallback");
        Assert.Equal("-16,-16,-16,0", Attr(mediaFrame, "Margin"));
        Assert.Equal("{DynamicResource PlayStead.Brush.SurfaceSubtle}", Attr(mediaFrame, "Background"));
        Assert.Equal("0", Attr(mediaFrame, "BorderThickness"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", Attr(mediaFrame, "CornerRadius"));
        Assert.Equal("True", Attr(mediaFrame, "ClipToBounds"));

        var maskShape = mediaFrame
            .Element(presentation + "Border.OpacityMask")!
            .Descendants(presentation + "Border")
            .Single();
        Assert.Equal("218", Attr(maskShape, "Width"));
        Assert.Equal("316", Attr(maskShape, "Height"));
        Assert.Equal("{DynamicResource PlayStead.Radius.Medium}", Attr(maskShape, "CornerRadius"));

        var artwork = Assert.Single(mediaFrame.Descendants(controls + "GameArtwork"));
        Assert.Equal("{Binding CoverPath}", Attr(artwork, "SourcePath"));
        Assert.Equal("{Binding HasCover}", Attr(artwork, "HasArtwork"));
        Assert.Equal("GameArtwork_OnMouseDoubleClick", Attr(artwork, "MouseDoubleClick"));

        var cardSurface = LoadUi("Themes/PlaySteadControls.xaml");
        var sharedCardStyle = cardSurface.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "PlayStead.Surface.Card");
        Assert.Equal("{StaticResource PlayStead.Surface.Panel}", Attr(sharedCardStyle, "BasedOn"));

        var sharedPanelStyle = cardSurface.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "PlayStead.Surface.Panel");
        Assert.Contains(sharedPanelStyle.Descendants(presentation + "Setter"), setter =>
            Attr(setter, "Property") == "BorderBrush" &&
            Attr(setter, "Value") == "{DynamicResource PlayStead.Brush.Border}");
        Assert.Contains(sharedPanelStyle.Descendants(presentation + "Setter"), setter =>
            Attr(setter, "Property") == "BorderThickness" &&
            Attr(setter, "Value") == "{DynamicResource PlayStead.Border.Thin}");
        Assert.Contains(sharedPanelStyle.Descendants(presentation + "Setter"), setter =>
            Attr(setter, "Property") == "CornerRadius" &&
            Attr(setter, "Value") == "{DynamicResource PlayStead.Radius.Medium}");
        Assert.Contains(sharedPanelStyle.Descendants(presentation + "Setter"), setter =>
            Attr(setter, "Property") == "Padding" &&
            Attr(setter, "Value") == "{DynamicResource PlayStead.Inset.Card}");

        var mediaFrameEffects = mediaFrame.Elements(presentation + "Border.Effect");
        Assert.Empty(mediaFrameEffects);

        var metadataPanel = cardShell.Descendants(presentation + "Grid")
            .Single(element => Attr(element, "Grid.Row") == "1" && Attr(element, "Margin") == "14");
        Assert.Equal("14", Attr(metadataPanel, "Margin"));
    }

    [Fact]
    public void GameCard_hover_keeps_only_the_external_border_over_the_cover()
    {
        var document = LoadUi("Controls/GameCard.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Null(document.Descendants().SingleOrDefault(element => Attr(element, "Name") == "HoverTint"));

        var hoverBorder = FindNamed(document, "HoverBorder");
        Assert.Equal("{DynamicResource PlayStead.Brush.Copper}", Attr(hoverBorder, "BorderBrush"));
        Assert.Equal("Transparent", Attr(hoverBorder, "Background"));
        Assert.Equal("1", Attr(hoverBorder, "BorderThickness"));
        Assert.Equal("False", Attr(hoverBorder, "IsHitTestVisible"));

        var triggers = document.Root!.Element(presentation + "UserControl.Triggers")!;
        var enterTargets = triggers.Elements(presentation + "EventTrigger")
            .Single(trigger => Attr(trigger, "RoutedEvent") == "MouseEnter")
            .Descendants(presentation + "DoubleAnimation")
            .Select(animation => Attr(animation, "Storyboard.TargetName"))
            .ToArray();
        var leaveTargets = triggers.Elements(presentation + "EventTrigger")
            .Single(trigger => Attr(trigger, "RoutedEvent") == "MouseLeave")
            .Descendants(presentation + "DoubleAnimation")
            .Select(animation => Attr(animation, "Storyboard.TargetName"))
            .ToArray();

        Assert.Equal(new[] { "CardLift", "HoverBorder" }, enterTargets);
        Assert.Equal(new[] { "CardLift", "HoverBorder" }, leaveTargets);

        var mediaFrame = FindNamed(document, "MediaFallback");
        Assert.Equal("-16,-16,-16,0", Attr(mediaFrame, "Margin"));
        Assert.Equal("218", Attr(mediaFrame.Descendants(presentation + "Border").Single(), "Width"));
        Assert.Equal("316", Attr(mediaFrame.Descendants(presentation + "Border").Single(), "Height"));
        Assert.Equal("220", Attr(document.Root!, "Width"));
        Assert.Equal("300", Attr(FindNamed(document, "CardShell")
            .Descendants(presentation + "RowDefinition").First(), "Height"));
    }

    [Fact]
    public void GameCard_hides_neutral_status_and_keeps_meaningful_status_visible()
    {
        PlaySteadWpfTestResources.Run(() =>
        {
            var card = new GameCard();
            var badgeStyle = card.Resources["Library.SteamStatusBadge"];
            var neutralItem = CreateSteamItem(SteamUpdateState.Unknown);
            card.DataContext = neutralItem;
            MeasureCard(card);

            var badge = FindVisualChildren<Border>(card)
                .Single(border => ReferenceEquals(border.Style, badgeStyle));

            Assert.True(neutralItem.HasSteamStatus);
            Assert.Equal("État inconnu", neutralItem.SteamStatusLabel);
            Assert.Equal(Visibility.Collapsed, badge.Visibility);
            Assert.Equal(new Size(0, 0), badge.DesiredSize);

            var meaningfulItem = CreateSteamItem(SteamUpdateState.UpdateAvailable);
            card.DataContext = meaningfulItem;
            MeasureCard(card);

            Assert.Same(badge, FindVisualChildren<Border>(card)
                .Single(border => ReferenceEquals(border.Style, badgeStyle)));
            Assert.True(meaningfulItem.HasSteamStatus);
            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Equal("Mise à jour disponible", meaningfulItem.SteamStatusLabel);
        });
    }

    [Fact]
    public void Library_outer_spacing_uses_page_token_and_list_contract_remains_bound()
    {
        var document = LoadUi("Library/LibraryView.xaml");

        var page = document.Root!.Elements().First(element => element.Name.LocalName == "Grid");
        Assert.Equal("{DynamicResource PlayStead.Inset.Page}", Attr(page, "Margin"));
        Assert.Equal("{Binding GridRows}", Attr(FindNamed(document, "GameGridRows"), "ItemsSource"));
        Assert.Equal(
            "{Binding IsListMode, Converter={StaticResource BooleanToVisibilityConverter}}",
            Attr(FindNamed(document, "GameList"), "Visibility"));
        Assert.Equal("{Binding VisibleItems}", Attr(FindNamed(document, "GameList"), "ItemsSource"));
    }

    [Fact]
    public void Library_list_uses_compact_structured_rows_without_install_path_noise()
    {
        var document = LoadUi("Library/LibraryView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace controls = "clr-namespace:PlayStead.UI.Controls";

        var header = FindNamed(document, "ListHeaderRow");
        Assert.Equal("Jeu", Attr(header.Descendants(presentation + "TextBlock").ElementAt(0), "Text"));
        Assert.Contains(header.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "Plateforme");
        Assert.Contains(header.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "Taille");
        Assert.DoesNotContain(header.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "Dernière activité");
        Assert.DoesNotContain(header.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "Temps de jeu");

        var row = FindNamed(document, "ListGameRow");
        Assert.Equal("0,0,0,8", Attr(row, "Padding"));
        Assert.Equal("56", Attr(Assert.Single(row.Descendants(controls + "GameArtwork")), "Height"));
        Assert.Equal("48", Attr(Assert.Single(row.Descendants(controls + "GameArtwork")), "Width"));
        var title = Assert.Single(row.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "{Binding Title}");
        Assert.Equal("{Binding InstallPath}", Attr(title, "ToolTip"));
        Assert.Equal("CharacterEllipsis", Attr(title, "TextTrimming"));
        Assert.Contains(row.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "{Binding ProviderLabel}");
        Assert.Contains(row.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "{Binding InstalledSizeLabel, Mode=OneWay}");
        Assert.DoesNotContain(row.Descendants(presentation + "TextBlock"), element => Attr(element, "Text") == "{Binding InstallPath}");

        var statusStyle = document.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "Library.ListSteamStatusBadge");
        Assert.Contains(statusStyle.Descendants(presentation + "DataTrigger"), trigger =>
            Attr(trigger, "Binding") == "{Binding SteamState}" &&
            Attr(trigger, "Value") == "{x:Static steam:SteamUpdateState.Unknown}" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                Attr(setter, "Property") == "Visibility" && Attr(setter, "Value") == "Collapsed"));
        var rowStyle = document.Descendants(presentation + "Style")
            .Single(style => Attr(style, "Key") == "Library.ListRow");
        Assert.Contains(rowStyle.Descendants(presentation + "Trigger"), trigger =>
            Attr(trigger, "Property") == "IsMouseOver" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                Attr(setter, "Property") == "Background" &&
                Attr(setter, "Value") == "{DynamicResource PlayStead.Brush.SurfaceSubtle}"));
        Assert.Contains(rowStyle.Descendants(presentation + "Trigger"), trigger =>
            Attr(trigger, "Property") == "IsKeyboardFocusWithin" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                Attr(setter, "Property") == "BorderBrush" &&
                Attr(setter, "Value") == "{DynamicResource PlayStead.Brush.Accent}"));
    }

    private static bool HasCondition(XElement trigger, string binding, string value)
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        return trigger.Descendants(presentation + "Condition").Any(condition =>
            Attr(condition, "Binding") == binding && Attr(condition, "Value") == value);
    }

    private static LibraryItemViewModel CreateSteamItem(SteamUpdateState state) =>
        new(GameId.New(), "Steam Game", ProviderKind.Steam, "Steam", @"G:\SteamLibrary\steamapps\common\Steam Game", null, state);

    private static void MeasureCard(GameCard card)
    {
        card.Measure(new Size(220, 1000));
        card.Arrange(new Rect(0, 0, 220, 1000));
        card.UpdateLayout();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static XDocument LoadUi(string relativePath) => XDocument.Load(FindUiFile(relativePath));

    private static XElement FindNamed(XDocument document, string name) =>
        document.Descendants().Single(element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name));

    private static string? Attr(XElement element, string name) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PlayStead.UI", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find UI file '{relativePath}'.");
    }
}
