using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class PlaySteadThemeContractTests
{
    private static readonly string[] DictionaryPaths =
    [
        "Themes/PlaySteadTokens.xaml",
        "Themes/PlaySteadControls.xaml"
    ];

    [Fact]
    public void App_merges_tokens_then_controls()
    {
        var app = XDocument.Load(FindUiFile("App.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var sources = app.Descendants(presentation + "ResourceDictionary.MergedDictionaries")
            .Elements(presentation + "ResourceDictionary")
            .Select(element => (string?)element.Attribute("Source"))
            .ToArray();

        foreach (var path in DictionaryPaths)
        {
            Assert.Single(sources, source => source == path);
        }

        Assert.True(Array.IndexOf(sources, DictionaryPaths[0]) < Array.IndexOf(sources, DictionaryPaths[1]));
    }

    [Fact]
    public void Required_tokens_are_declared_exactly_once()
    {
        string[] requiredKeys =
        [
            "PlayStead.Brush.Background", "PlayStead.Brush.Surface",
            "PlayStead.Brush.SurfaceRaised", "PlayStead.Brush.Border",
            "PlayStead.Brush.TextPrimary", "PlayStead.Brush.TextSecondary",
            "PlayStead.Brush.Copper", "PlayStead.Brush.CopperHover",
            "PlayStead.Spacing.1", "PlayStead.Spacing.2", "PlayStead.Spacing.3",
            "PlayStead.Spacing.4", "PlayStead.Spacing.5", "PlayStead.Spacing.6",
            "PlayStead.Radius.Small", "PlayStead.Radius.Medium", "PlayStead.Radius.Large",
            "PlayStead.Duration.Fast", "PlayStead.Duration.Normal"
        ];
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var keys = DictionaryPaths.SelectMany(path =>
                XDocument.Load(FindUiFile(path)).Descendants()
                    .Attributes(xaml + "Key").Select(attribute => attribute.Value))
            .ToArray();

        foreach (var key in requiredKeys)
        {
            Assert.Single(keys, candidate => candidate == key);
        }
    }

    [Theory]
    [InlineData("SectionHeader", "Title")]
    [InlineData("StatusBadge", "Text")]
    [InlineData("KpiCard", "Label")]
    [InlineData("KpiCard", "Value")]
    [InlineData("EmptyState", "Message")]
    public void Reusable_control_preserves_public_bindable_contract(string name, string propertyName)
    {
        RunSta(() =>
        {
            var type = typeof(PlayStead.UI.Library.LibraryView).Assembly
                .GetType($"PlayStead.UI.Controls.{name}");
            Assert.NotNull(type);
            var control = Assert.IsAssignableFrom<UserControl>(Activator.CreateInstance(type));
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.Equal(typeof(string), property.PropertyType);
            Assert.True(property.CanRead && property.CanWrite);
            var field = type.GetField($"{propertyName}Property", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field);
            var dependencyProperty = Assert.IsType<DependencyProperty>(field.GetValue(null));
            Assert.Equal(type, dependencyProperty.OwnerType);
            Assert.Equal(typeof(string), dependencyProperty.PropertyType);
            property.SetValue(control, "Contract value");
            Assert.Equal("Contract value", control.GetValue(dependencyProperty));
            control.SetValue(dependencyProperty, "Updated value");
            Assert.Equal("Updated value", property.GetValue(control));
        });
    }

    [Fact]
    public void Representative_controls_consume_semantic_typography_roles()
    {
        var sectionHeader = File.ReadAllText(FindUiFile("Controls/SectionHeader.xaml"));
        var kpiCard = File.ReadAllText(FindUiFile("Controls/KpiCard.xaml"));
        var emptyState = File.ReadAllText(FindUiFile("Controls/EmptyState.xaml"));

        Assert.Contains("Style=\"{DynamicResource PlayStead.Text.SectionTitle}\"", sectionHeader, StringComparison.Ordinal);
        Assert.Contains("Style=\"{DynamicResource PlayStead.Text.Caption}\"", kpiCard, StringComparison.Ordinal);
        Assert.Contains("Style=\"{DynamicResource PlayStead.Text.Display}\"", kpiCard, StringComparison.Ordinal);
        Assert.Contains("Style=\"{DynamicResource PlayStead.Text.Body}\"", emptyState, StringComparison.Ordinal);

        Assert.DoesNotContain("FontSize=", sectionHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("FontWeight=", sectionHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("FontFamily=", sectionHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", kpiCard, StringComparison.Ordinal);
        Assert.DoesNotContain("FontWeight=", kpiCard, StringComparison.Ordinal);
        Assert.DoesNotContain("FontFamily=", kpiCard, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", emptyState, StringComparison.Ordinal);
        Assert.DoesNotContain("FontWeight=", emptyState, StringComparison.Ordinal);
        Assert.DoesNotContain("FontFamily=", emptyState, StringComparison.Ordinal);
    }

    [Fact]
    public void Game_detail_hero_title_contract_remains_30_bold()
    {
        var gameDetail = File.ReadAllText(FindUiFile("Library/GameDetailView.xaml"));
        Assert.Contains("FontSize=\"30\"", gameDetail, StringComparison.Ordinal);
        Assert.Contains("FontWeight=\"Bold\"", gameDetail, StringComparison.Ordinal);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var uiDirectory = Path.Combine(directory.FullName, "src", "PlayStead.UI");
            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("PlayStead.UI source directory was not found.");
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
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
