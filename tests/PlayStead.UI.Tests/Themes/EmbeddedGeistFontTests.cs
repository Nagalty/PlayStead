using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class EmbeddedGeistFontTests
{
    private const string FontDirectory = "Assets/Fonts/Geist";
    private const string EmbeddedFamily =
        "/PlayStead.UI;component/Assets/Fonts/Geist/#Geist";
    private const string AuthoritativeChain =
        $"{EmbeddedFamily}, Segoe UI Variable, Segoe UI";

    private static readonly string[] RequiredFaces =
    [
        "Geist-Regular.ttf",
        "Geist-SemiBold.ttf",
        "Geist-Bold.ttf",
    ];

    [Fact]
    public void Ui_project_embeds_exact_required_faces_and_ships_official_license()
    {
        var project = XDocument.Load(PlaySteadDesignTokenTests.FindUiFile("PlayStead.UI.csproj"));
        var resources = project.Descendants("Resource")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(value => value is not null)
            .ToArray();

        foreach (var face in RequiredFaces)
        {
            Assert.Contains($"{FontDirectory}/{face}", resources);
        }

        Assert.Equal(RequiredFaces.Length, resources.Count(value =>
            value!.StartsWith($"{FontDirectory}/Geist-", StringComparison.Ordinal) &&
            value.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)));

        var license = Assert.Single(
            project.Descendants("None"),
            element => element.Attribute("Update")?.Value.Replace('\\', '/') ==
                       $"{FontDirectory}/OFL.txt");
        Assert.Equal(
            "PreserveNewest",
            license.Element("CopyToOutputDirectory")?.Value);
        Assert.Equal(
            "PreserveNewest",
            license.Element("CopyToPublishDirectory")?.Value);
    }

    [Fact]
    public void Official_static_faces_and_unchanged_ofl_notice_are_present()
    {
        var fontDirectory = Path.GetDirectoryName(
            PlaySteadDesignTokenTests.FindUiFile($"{FontDirectory}/Geist-Regular.ttf"))!;
        var fontFiles = Directory.GetFiles(fontDirectory, "*.ttf")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(RequiredFaces.Order(StringComparer.Ordinal), fontFiles);
        Assert.All(RequiredFaces, face =>
            Assert.True(new FileInfo(Path.Combine(fontDirectory, face)).Length > 0));

        var license = File.ReadAllText(Path.Combine(fontDirectory, "OFL.txt"));
        Assert.StartsWith("Copyright", license, StringComparison.Ordinal);
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", license, StringComparison.Ordinal);
    }

    [Fact]
    public void Authoritative_sans_tokens_use_embedded_Geist_then_defensive_Segoe_fallbacks()
    {
        var tokens = XDocument.Load(
            PlaySteadDesignTokenTests.FindUiFile("Themes/PlaySteadTokens.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var key in new[]
                 {
                     "PlayStead.Font.Display",
                     "PlayStead.Font.Sans",
                     "PlayStead.Font.Body",
                 })
        {
            var token = Assert.Single(
                tokens.Descendants(),
                element => (string?)element.Attribute(xaml + "Key") == key);
            Assert.Equal(AuthoritativeChain, token.Value);
        }

        var productionXaml = Directory.GetFiles(
                Path.GetDirectoryName(PlaySteadDesignTokenTests.FindUiFile("App.xaml"))!,
                "*.xaml",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText);
        Assert.DoesNotContain(
            productionXaml,
            source => source.Contains(">Geist Sans,", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Geist-Regular.ttf", "Normal")]
    [InlineData("Geist-SemiBold.ttf", "SemiBold")]
    [InlineData("Geist-Bold.ttf", "Bold")]
    public void Embedded_face_is_a_resolvable_WPF_resource_with_expected_weight(
        string fileName,
        string expectedWeight)
    {
        RunSta(() =>
        {
            var uri = new Uri(
                $"/PlayStead.UI;component/{FontDirectory}/{fileName}",
                UriKind.Relative);
            var resource = Application.GetResourceStream(uri);

            Assert.NotNull(resource);
            using var stream = resource.Stream;
            Assert.True(stream.Length > 0);

            var tokens = (ResourceDictionary)Application.LoadComponent(
                new Uri(
                    "/PlayStead.UI;component/Themes/PlaySteadTokens.xaml",
                    UriKind.Relative));
            var family = Assert.IsType<FontFamily>(tokens["PlayStead.Font.Sans"]);
            var weight = (FontWeight)new FontWeightConverter().ConvertFromString(expectedWeight)!;
            var typeface = new Typeface(
                family,
                FontStyles.Normal,
                weight,
                FontStretches.Normal);

            Assert.True(typeface.TryGetGlyphTypeface(out var glyphTypeface));
            Assert.Equal(weight, glyphTypeface.Weight);
            Assert.EndsWith(fileName, glyphTypeface.FontUri.OriginalString, StringComparison.OrdinalIgnoreCase);
        });
    }

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
