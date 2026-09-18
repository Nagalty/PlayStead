using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class PlaySteadButtonStyleTests
{
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Controls =>
        XDocument.Load(PlaySteadDesignTokenTests.FindUiFile("Themes/PlaySteadControls.xaml"));

    [Theory]
    [InlineData("PlayStead.Button.Primary")]
    [InlineData("PlayStead.Button.Secondary")]
    [InlineData("PlayStead.Button.Ghost")]
    [InlineData("PlayStead.Button.Icon")]
    public void Authoritative_button_style_derives_from_the_common_button_base(string key)
    {
        var style = FindStyle(key);

        Assert.Equal("Button", style.Attribute("TargetType")?.Value);
        Assert.Equal("{StaticResource PlayStead.ButtonBase}", style.Attribute("BasedOn")?.Value);
    }

    [Theory]
    [InlineData("PlayStead.Button.Primary")]
    [InlineData("PlayStead.Button.Secondary")]
    [InlineData("PlayStead.Button.Ghost")]
    [InlineData("PlayStead.Button.Icon")]
    public void Authoritative_button_style_defines_every_interaction_state(string key)
    {
        var style = FindStyle(key);
        var triggers = style
            .Descendants()
            .Where(element => element.Name.LocalName == "Trigger")
            .Select(element => (
                Property: element.Attribute("Property")?.Value,
                Value: element.Attribute("Value")?.Value))
            .ToArray();

        Assert.Contains(("IsMouseOver", "True"), triggers);
        Assert.Contains(("IsPressed", "True"), triggers);
        Assert.Contains(("IsEnabled", "False"), triggers);
        Assert.Contains(("IsKeyboardFocusWithin", "True"), triggers);
    }

    [Fact]
    public void Common_button_base_centralizes_metrics_typography_and_focus()
    {
        var style = FindStyle("PlayStead.ButtonBase");

        Assert.Equal("{DynamicResource PlayStead.Control.Height}", Setter(style, "MinHeight"));
        Assert.Equal("{DynamicResource PlayStead.Control.Padding}", Setter(style, "Padding"));
        Assert.Equal("{DynamicResource PlayStead.Border.Thin}", Setter(style, "BorderThickness"));
        Assert.Equal("{DynamicResource PlayStead.Font.Sans}", Setter(style, "FontFamily"));
        Assert.Equal("{DynamicResource PlayStead.FontSize.14}", Setter(style, "FontSize"));
        Assert.Equal("SemiBold", Setter(style, "FontWeight"));
        Assert.Equal("{StaticResource PlayStead.FocusVisual}", Setter(style, "FocusVisualStyle"));
        Assert.Equal("{StaticResource PlayStead.ButtonTemplate}", Setter(style, "Template"));
    }

    [Fact]
    public void Primary_disabled_state_uses_PlayStead_brushes_instead_of_native_Windows_visuals()
    {
        var disabled = FindTrigger(FindStyle("PlayStead.Button.Primary"), "IsEnabled", "False");

        Assert.Contains("PlayStead.Brush.SurfaceStrong", disabled.ToString(), StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.TextPrimary", disabled.ToString(), StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.BorderStrong", disabled.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Primary_uses_the_accent_palette_with_contrasting_shared_metrics()
    {
        var style = FindStyle("PlayStead.Button.Primary");

        Assert.Equal("{DynamicResource PlayStead.Brush.Accent}", Setter(style, "Background"));
        Assert.Equal("{DynamicResource PlayStead.Brush.Background}", Setter(style, "Foreground"));
        Assert.Equal("{DynamicResource PlayStead.Brush.AccentHover}", Setter(style, "BorderBrush"));

        var template = Assert.Single(
            Controls.Descendants().Where(element => element.Name.LocalName == "ControlTemplate"),
            element => (string?)element.Attribute(Xaml + "Key") == "PlayStead.ButtonTemplate");
        Assert.Contains("PlayStead.Radius.Small", template.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_play_icon_is_vector_geometry_used_by_the_primary_split_action()
    {
        var geometry = Assert.Single(
            Controls.Descendants(),
            element =>
                element.Name.LocalName == "StreamGeometry" &&
                (string?)element.Attribute(Xaml + "Key") == "PlayStead.Icon.Play");
        Assert.False(string.IsNullOrWhiteSpace(geometry.Value));

        var split = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Controls/PlaySplitButton.xaml"));
        var primaryButton = split.Descendants()
            .Single(element =>
                element.Name.LocalName == "Button" &&
                element.Attribute("Click")?.Value == "Play_OnClick");
        var icon = Assert.Single(
            primaryButton.Descendants(),
            element => element.Name.LocalName == "Path");

        Assert.Equal("{DynamicResource PlayStead.Icon.Play}", icon.Attribute("Data")?.Value);
        Assert.Contains("IsSessionActive", icon.ToString(), StringComparison.Ordinal);
        Assert.Contains("Collapsed", icon.ToString(), StringComparison.Ordinal);
        Assert.Contains("PlayLabel", primaryButton.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Game_detail_cta_is_a_responsive_row_without_a_fake_Steam_action()
    {
        var detail = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Library/GameDetailView.xaml"));
        var row = FindNamed(detail, "GameDetailCtaRow");

        Assert.Equal("WrapPanel", row.Name.LocalName);
        Assert.Single(row.Descendants(), element => element.Name.LocalName == "PlaySplitButton");
        Assert.DoesNotContain("Ouvrir Steam", detail.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Icon_style_centralizes_square_control_metrics()
    {
        var style = FindStyle("PlayStead.Button.Icon");

        Assert.Equal("{DynamicResource PlayStead.Control.IconSize}", Setter(style, "Width"));
        Assert.Equal("{DynamicResource PlayStead.Control.IconSize}", Setter(style, "Height"));
        Assert.Equal("{DynamicResource PlayStead.Control.IconPadding}", Setter(style, "Padding"));
    }

    [Fact]
    public void Representative_views_consume_each_authoritative_button_role()
    {
        var settings = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Settings/SettingsView.xaml"));
        var library = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Library/LibraryView.xaml"));
        var split = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Controls/PlaySplitButton.xaml"));

        AssertStyle(FindNamed(settings, "SaveSettingsButton"), "PlayStead.Button.Primary");
        AssertStyle(FindNamed(library, "OpenGameDetailButton"), "PlayStead.Button.Secondary");
        AssertStyle(FindNamed(library, "RescanButton"), "PlayStead.Button.Ghost");
        AssertStyle(FindNamed(library, "CloseQuickPanelButton"), "PlayStead.Button.Icon");

        var primarySplitButton = split.Descendants()
            .Single(element =>
                element.Name.LocalName == "Button" &&
                element.Attribute("Click")?.Value == "Play_OnClick");
        AssertStyle(primarySplitButton, "PlayStead.Button.Primary");
    }

    [Fact]
    public void Play_split_button_keeps_behavior_bindings_without_a_local_visual_style()
    {
        var split = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Controls/PlaySplitButton.xaml"));
        var source = split.ToString();

        Assert.Contains("Play_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("Options_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("Installation_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonMinWidth", source, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonPadding", source, StringComparison.Ordinal);
        Assert.Contains("PlayLabel", source, StringComparison.Ordinal);
        Assert.Contains("IsSessionActive", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button.Style>", source, StringComparison.Ordinal);
    }

    private static XElement FindStyle(string key) =>
        Assert.Single(
            Controls.Descendants().Where(element => element.Name.LocalName == "Style"),
            element => (string?)element.Attribute(Xaml + "Key") == key);

    private static XElement FindTrigger(
        XElement style,
        string property,
        string value) =>
        Assert.Single(
            style.Descendants().Where(element => element.Name.LocalName == "Trigger"),
            element =>
                element.Attribute("Property")?.Value == property &&
                element.Attribute("Value")?.Value == value);

    private static string Setter(XElement style, string property) =>
        Assert.Single(
            style.Elements().Where(element => element.Name.LocalName == "Setter"),
            element => element.Attribute("Property")?.Value == property)
            .Attribute("Value")?.Value
        ?? string.Empty;

    private static XElement FindNamed(XDocument document, string name) =>
        Assert.Single(
            document.Descendants(),
            element => element.Attribute(Xaml + "Name")?.Value == name);

    private static void AssertStyle(XElement element, string expectedKey) =>
        Assert.Equal($"{{DynamicResource {expectedKey}}}", element.Attribute("Style")?.Value);
}
