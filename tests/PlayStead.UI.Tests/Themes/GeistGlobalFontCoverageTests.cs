using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class GeistGlobalFontCoverageTests
{
    private const string SansToken = "{DynamicResource PlayStead.Font.Sans}";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Main_shell_establishes_PlayStead_Sans_as_the_global_runtime_font()
    {
        var mainWindow = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("MainWindow.xaml"));

        Assert.Equal(SansToken, mainWindow.Root?.Attribute("FontFamily")?.Value);
    }

    [Fact]
    public void Wpf_root_font_inheritance_reaches_ordinary_text_and_button_labels()
    {
        RunSta(() =>
        {
            var tokens = (ResourceDictionary)Application.LoadComponent(
                new Uri(
                    "/PlayStead.UI;component/Themes/PlaySteadTokens.xaml",
                    UriKind.Relative));
            var geist = Assert.IsType<FontFamily>(tokens["PlayStead.Font.Sans"]);
            var ordinaryText = new TextBlock { Text = "Ordinary text" };
            var buttonLabel = new TextBlock { Text = "Button label" };
            var button = new Button { Content = buttonLabel };
            var panel = new StackPanel();
            panel.Children.Add(ordinaryText);
            panel.Children.Add(button);

            var window = new Window
            {
                FontFamily = geist,
                Content = panel,
            };

            try
            {
                Assert.Equal(geist.Source, ordinaryText.FontFamily.Source);
                Assert.Equal(geist.Source, button.FontFamily.Source);
                Assert.Equal(geist.Source, buttonLabel.FontFamily.Source);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Shell_wordmark_and_navigation_labels_inherit_without_local_overrides()
    {
        var mainWindow = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("MainWindow.xaml"));

        AssertNoLocalFont(mainWindow, "PlaySteadWordmark");
        AssertNoLocalFont(mainWindow, "HomeNavButton");
        AssertNoLocalFont(mainWindow, "LibraryNavButton");
        AssertNoLocalFont(mainWindow, "AttentionNavButton");
        AssertNoLocalFont(mainWindow, "SettingsNavButton");
        AssertNoLocalFont(mainWindow, "NotificationBellButton");
    }

    [Fact]
    public void Semantic_text_and_button_foundations_consume_PlayStead_font_tokens()
    {
        var controls = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Themes/PlaySteadControls.xaml"));
        var styles = controls.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToArray();

        var buttonBase = Assert.Single(
            styles,
            style => (string?)style.Attribute(Xaml + "Key") == "PlayStead.ButtonBase");
        Assert.Equal(SansToken, FontSetter(buttonBase));

        foreach (var role in new[]
                 {
                     "PlayStead.Text.Display",
                     "PlayStead.Text.PageTitle",
                     "PlayStead.Text.SectionTitle",
                     "PlayStead.Text.CardTitle",
                     "PlayStead.Text.Body",
                     "PlayStead.Text.BodySecondary",
                     "PlayStead.Text.Caption",
                     "PlayStead.Text.ButtonLabel",
                 })
        {
            var style = Assert.Single(
                styles,
                candidate => (string?)candidate.Attribute(Xaml + "Key") == role);
            Assert.StartsWith(
                "{DynamicResource PlayStead.Font.",
                FontSetter(style),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ordinary_product_ui_has_no_explicit_system_Segoe_font()
    {
        var occurrences = ProductionFontAttributes().ToArray();
        var explicitSegoe = occurrences
            .Where(occurrence => occurrence.Value.StartsWith("Segoe", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(9, explicitSegoe.Length);
        Assert.All(explicitSegoe, occurrence =>
        {
            Assert.Equal("Segoe MDL2 Assets", occurrence.Value);
            Assert.Equal("TextBlock", occurrence.Element);
        });
    }

    [Fact]
    public void Every_explicit_ordinary_font_uses_a_centralized_PlayStead_token()
    {
        var ordinary = ProductionFontAttributes()
            .Where(occurrence => occurrence.Value != "Segoe MDL2 Assets")
            .ToArray();

        Assert.NotEmpty(ordinary);
        Assert.All(
            ordinary,
            occurrence => Assert.StartsWith(
                "{DynamicResource PlayStead.Font.",
                occurrence.Value,
                StringComparison.Ordinal));
    }

    private static IEnumerable<(string File, string Element, string Value)>
        ProductionFontAttributes()
    {
        var uiRoot = Path.GetDirectoryName(
            PlaySteadDesignTokenTests.FindUiFile("App.xaml"))!;

        foreach (var file in Directory.GetFiles(uiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            foreach (var element in document.Root!.DescendantsAndSelf())
            {
                var value = element.Attribute("FontFamily")?.Value;
                if (value is not null)
                {
                    yield return (Path.GetFileName(file), element.Name.LocalName, value);
                }
            }
        }
    }

    private static void AssertNoLocalFont(XDocument document, string name)
    {
        var element = Assert.Single(
            document.Descendants(),
            candidate => (string?)candidate.Attribute(Xaml + "Name") == name);
        Assert.Null(element.Attribute("FontFamily"));
    }

    private static string FontSetter(XElement style) =>
        Assert.Single(
                style.Elements(),
                element => element.Name.LocalName == "Setter" &&
                           (string?)element.Attribute("Property") == "FontFamily")
            .Attribute("Value")?.Value
        ?? string.Empty;

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
