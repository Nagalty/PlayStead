using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class PlaySteadDesignTokenTests
{
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Tokens =>
        XDocument.Load(FindUiFile("Themes/PlaySteadTokens.xaml"));

    [Theory]
    [InlineData("PlayStead.Brush.Accent", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.AccentHover", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.AccentPressed", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.AccentMuted", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.SurfaceElevated", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.SurfaceNested", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.Error", "SolidColorBrush")]
    [InlineData("PlayStead.Brush.Neutral", "SolidColorBrush")]
    [InlineData("PlayStead.Inset.Page", "Thickness")]
    [InlineData("PlayStead.Inset.Card", "Thickness")]
    [InlineData("PlayStead.Inset.Control", "Thickness")]
    [InlineData("PlayStead.Gap.Block.Small", "Thickness")]
    [InlineData("PlayStead.Gap.Inline.XSmall", "Thickness")]
    [InlineData("PlayStead.Border.None", "Thickness")]
    [InlineData("PlayStead.Border.Thin", "Thickness")]
    [InlineData("PlayStead.Border.Emphasis", "Thickness")]
    [InlineData("PlayStead.Font.Sans", "FontFamily")]
    [InlineData("PlayStead.Control.Padding", "Thickness")]
    [InlineData("PlayStead.Control.IconPadding", "Thickness")]
    [InlineData("PlayStead.Control.Height", "Double")]
    [InlineData("PlayStead.Control.IconSize", "Double")]
    [InlineData("PlayStead.Control.Radius", "Double")]
    public void Foundation_token_exists_once_with_expected_WPF_type(
        string key,
        string expectedType)
    {
        var token = Assert.Single(
            Tokens.Descendants(),
            element => (string?)element.Attribute(Xaml + "Key") == key);

        Assert.Equal(expectedType, token.Name.LocalName);
    }

    [Fact]
    public void Sans_and_body_fonts_use_the_authoritative_fallback_chain()
    {
        const string expected =
            "/PlayStead.UI;component/Assets/Fonts/Geist/#Geist, Segoe UI Variable, Segoe UI";

        Assert.Equal(expected, TokenValue("PlayStead.Font.Sans"));
        Assert.Equal(expected, TokenValue("PlayStead.Font.Body"));
    }

    [Theory]
    [InlineData("PlayStead.FontSize.11", "11")]
    [InlineData("PlayStead.FontSize.12", "12")]
    [InlineData("PlayStead.FontSize.13", "13")]
    [InlineData("PlayStead.FontSize.14", "14")]
    [InlineData("PlayStead.FontSize.15", "15")]
    [InlineData("PlayStead.FontSize.16", "16")]
    [InlineData("PlayStead.FontSize.17", "17")]
    [InlineData("PlayStead.FontSize.20", "20")]
    [InlineData("PlayStead.FontSize.22", "22")]
    [InlineData("PlayStead.FontSize.24", "24")]
    [InlineData("PlayStead.FontSize.26", "26")]
    [InlineData("PlayStead.FontSize.30", "30")]
    public void Audited_font_size_primitive_is_a_typed_Double(
        string key,
        string expectedValue)
    {
        var token = Assert.Single(
            Tokens.Descendants(),
            element => (string?)element.Attribute(Xaml + "Key") == key);

        Assert.Equal("Double", token.Name.LocalName);
        Assert.Equal(expectedValue, token.Value);
    }

    [Fact]
    public void Representative_consumers_use_semantic_typed_tokens()
    {
        var splitButton = File.ReadAllText(
            FindUiFile("Controls/PlaySplitButton.xaml"));
        var emptyState = File.ReadAllText(
            FindUiFile("Controls/EmptyState.xaml"));
        var gameCard = File.ReadAllText(
            FindUiFile("Controls/GameCard.xaml"));
        var controls = File.ReadAllText(
            FindUiFile("Themes/PlaySteadControls.xaml"));

        Assert.Contains(
            "Style=\"{DynamicResource PlayStead.Button.Primary}\"",
            splitButton,
            StringComparison.Ordinal);
        Assert.Contains(
            "Value=\"{DynamicResource PlayStead.Brush.Accent}\"",
            controls,
            StringComparison.Ordinal);
        Assert.Contains(
            "Margin=\"{DynamicResource PlayStead.Gap.Inline.XSmall}\"",
            splitButton,
            StringComparison.Ordinal);
        Assert.Contains(
            "Padding=\"{DynamicResource PlayStead.Inset.Card}\"",
            emptyState,
            StringComparison.Ordinal);
        Assert.Contains(
            "Style=\"{DynamicResource PlayStead.Surface.Card}\"",
            gameCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Style x:Key=\"PlayStead.Surface.Card\" TargetType=\"Border\" BasedOn=\"{StaticResource PlayStead.Surface.Panel}\"/>",
            controls,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"CornerRadius\" Value=\"{DynamicResource PlayStead.Radius.Medium}\"/>",
            controls,
            StringComparison.Ordinal);
        Assert.Contains(
            "FontFamily\" Value=\"{DynamicResource PlayStead.Font.Sans}\"",
            controls,
            StringComparison.Ordinal);
    }

    private static string TokenValue(string key) =>
        Assert.Single(
            Tokens.Descendants(),
            element => (string?)element.Attribute(Xaml + "Key") == key).Value;

    internal static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var uiDirectory = Path.Combine(directory.FullName, "src", "PlayStead.UI");
            if (Directory.Exists(uiDirectory))
            {
                return Path.Combine(uiDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("PlayStead.UI source directory was not found.");
    }
}
