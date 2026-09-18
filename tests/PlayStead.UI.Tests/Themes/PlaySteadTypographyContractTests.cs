using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class PlaySteadTypographyContractTests
{
    private const string EmbeddedGeist =
        "/PlayStead.UI;component/Assets/Fonts/Geist/#Geist, Segoe UI Variable, Segoe UI";

    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Tokens =>
        XDocument.Load(PlaySteadDesignTokenTests.FindUiFile("Themes/PlaySteadTokens.xaml"));

    private static XDocument Controls =>
        XDocument.Load(PlaySteadDesignTokenTests.FindUiFile("Themes/PlaySteadControls.xaml"));

    [Fact]
    public void Authoritative_font_families_include_display_body_and_mono_roles()
    {
        Assert.Equal(
            EmbeddedGeist,
            TokenValue("PlayStead.Font.Display"));
        Assert.Equal(
            EmbeddedGeist,
            TokenValue("PlayStead.Font.Body"));
        Assert.Equal(
            "Cascadia Mono, Consolas",
            TokenValue("PlayStead.Font.Mono"));
    }

    [Theory]
    [InlineData("PlayStead.Text.Display", "PlayStead.FontSize.30", "Bold")]
    [InlineData("PlayStead.Text.PageTitle", "PlayStead.FontSize.26", "SemiBold")]
    [InlineData("PlayStead.Text.SectionTitle", "PlayStead.FontSize.17", "SemiBold")]
    [InlineData("PlayStead.Text.CardTitle", "PlayStead.FontSize.15", "SemiBold")]
    [InlineData("PlayStead.Text.Body", "PlayStead.FontSize.14", "Normal")]
    [InlineData("PlayStead.Text.BodySecondary", "PlayStead.FontSize.13", "Normal")]
    [InlineData("PlayStead.Text.Caption", "PlayStead.FontSize.12", "Normal")]
    [InlineData("PlayStead.Text.ButtonLabel", "PlayStead.FontSize.14", "SemiBold")]
    [InlineData("PlayStead.Text.Technical", "PlayStead.FontSize.13", "Normal")]
    public void Semantic_text_role_has_the_fixed_typography_values(
        string key,
        string expectedSize,
        string expectedWeight)
    {
        var style = Assert.Single(
            Controls.Descendants().Where(element => element.Name.LocalName == "Style"),
            element => (string?)element.Attribute(Xaml + "Key") == key);

        Assert.Equal("TextBlock", style.Attribute("TargetType")?.Value);
        Assert.Equal(
            expectedSize,
            SetterValue(style, "FontSize"));
        Assert.Equal(
            expectedWeight,
            SetterValue(style, "FontWeight"));
        Assert.Contains(
            "PlayStead.Font.",
            SetterValue(style, "FontFamily"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_roles_are_declared_once()
    {
        var keys = Controls.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Attributes(Xaml + "Key")
            .Select(attribute => attribute.Value)
            .Where(key => key.StartsWith("PlayStead.Text.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(10, keys.Length);
        Assert.Equal(10, keys.Distinct(StringComparer.Ordinal).Count());
    }

    private static string TokenValue(string key) =>
        Assert.Single(
            Tokens.Descendants(),
            element => (string?)element.Attribute(Xaml + "Key") == key).Value;

    private static string SetterValue(XElement style, string property)
    {
        var setter = Assert.Single(
            style.Descendants().Where(element => element.Name.LocalName == "Setter"),
            element => (string?)element.Attribute("Property") == property);
        var value = setter.Attribute("Value")?.Value
            ?? setter.Element("Setter.Value")?.Value
            ?? string.Empty;
        const string dynamicPrefix = "{DynamicResource ";
        return value.StartsWith(dynamicPrefix, StringComparison.Ordinal) &&
               value.EndsWith('}')
            ? value[dynamicPrefix.Length..^1]
            : value;
    }
}
