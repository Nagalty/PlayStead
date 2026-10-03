using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed partial class HardcodedUiValueGuardTests
{
    private const int ExpectedHexColorCount = 77;
    private const int ExpectedFontSizeCount = 75;
    private const int ExpectedFontWeightCount = 52;
    private const int ExpectedCornerRadiusCount = 21;
    private const int ExpectedSpacingCount = 321;
    private const int ExpectedLocalButtonAppearanceCount = 0;

    [Fact]
    public void Production_Xaml_matches_the_reviewed_hardcoded_value_baseline()
    {
        var actual = ScanProductionXaml();
        var baseline = ReadBaseline();

        Assert.Equal(baseline, actual);
    }

    [Fact]
    public void Baseline_category_totals_cannot_grow_silently()
    {
        var baseline = ReadBaseline();

        Assert.Equal(ExpectedHexColorCount, Total(baseline, "HexColor"));
        Assert.Equal(ExpectedFontSizeCount, Total(baseline, "FontSize"));
        Assert.Equal(ExpectedFontWeightCount, Total(baseline, "FontWeight"));
        Assert.Equal(ExpectedCornerRadiusCount, Total(baseline, "CornerRadius"));
        Assert.Equal(ExpectedSpacingCount, Total(baseline, "Spacing"));
        Assert.Equal(
            ExpectedLocalButtonAppearanceCount,
            Total(baseline, "LocalButtonAppearance"));
    }

    [Fact]
    public void Synthetic_new_hex_and_radius_are_rejected_by_the_guard()
    {
        const string synthetic =
            "<Button xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            "Background=\"#123456\" CornerRadius=\"7\"/>";

        var additions = FindNewOrIncreased(
            ScanText("Synthetic.xaml", synthetic),
            []);

        Assert.Contains(additions, entry => entry.Category == "HexColor");
        Assert.Contains(additions, entry => entry.Category == "CornerRadius");
    }

    [Fact]
    public void Production_Xaml_has_no_composite_dynamic_resource_Thickness()
    {
        var violations = ProductionXamlFiles()
            .SelectMany(path => XDocument.Load(path)
                .Descendants()
                .Attributes()
                .Where(attribute =>
                    attribute.Name.LocalName is "Margin" or "Padding" or "BorderThickness")
                .Where(attribute =>
                    attribute.Value.Contains(',', StringComparison.Ordinal) &&
                    (attribute.Value.Contains("{DynamicResource ", StringComparison.Ordinal) ||
                     attribute.Value.Contains("{StaticResource ", StringComparison.Ordinal)))
                .Select(attribute =>
                    $"{RelativePath(path)}:{attribute.Name.LocalName}=\"{attribute.Value}\""))
            .ToArray();

        Assert.Empty(violations);
    }

    private static IReadOnlyList<BaselineEntry> ScanProductionXaml() =>
        ProductionXamlFiles()
            .SelectMany(path => ScanText(RelativePath(path), File.ReadAllText(path)))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<BaselineEntry> ScanText(
        string path,
        string xaml)
    {
        var entries = new List<BaselineEntry>();
        AddMatches(entries, path, "HexColor", HexColorPattern().Matches(xaml));
        AddMatches(entries, path, "FontSize", FontSizePattern().Matches(xaml));
        AddMatches(entries, path, "FontWeight", FontWeightPattern().Matches(xaml));
        AddMatches(entries, path, "CornerRadius", CornerRadiusPattern().Matches(xaml));
        AddMatches(entries, path, "Spacing", SpacingPattern().Matches(xaml));

        var localButtonAppearance =
            ButtonStylePattern()
                .Matches(xaml)
                .Count(match => FundamentalButtonSetterPattern().IsMatch(match.Value));
        if (localButtonAppearance > 0)
        {
            entries.Add(new BaselineEntry(
                path,
                "LocalButtonAppearance",
                "FundamentalAppearance",
                localButtonAppearance));
        }

        return entries;
    }

    private static IReadOnlyList<BaselineEntry> ReadBaseline() =>
        File.ReadAllLines(FindRepoFile(
                "tests/PlayStead.UI.Tests/Themes/HardcodedUiBaseline.txt"))
            .Where(line =>
                !string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith('#'))
            .Select(line =>
            {
                var parts = line.Split('|');
                Assert.Equal(4, parts.Length);
                return new BaselineEntry(
                    parts[0],
                    parts[1],
                    parts[2],
                    int.Parse(parts[3]));
            })
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<BaselineEntry> FindNewOrIncreased(
        IReadOnlyList<BaselineEntry> actual,
        IReadOnlyList<BaselineEntry> baseline)
    {
        var limits = baseline.ToDictionary(
            entry => (entry.Path, entry.Category, entry.Value),
            entry => entry.Count);

        return actual
            .Where(entry =>
                !limits.TryGetValue(
                    (entry.Path, entry.Category, entry.Value),
                    out var limit) ||
                entry.Count > limit)
            .ToArray();
    }

    private static int Total(
        IReadOnlyList<BaselineEntry> entries,
        string category) =>
        entries
            .Where(entry => entry.Category == category)
            .Sum(entry => entry.Count);

    private static void AddMatches(
        ICollection<BaselineEntry> entries,
        string path,
        string category,
        MatchCollection matches)
    {
        foreach (var group in matches
                     .Select(match => match.Groups["value"].Success
                         ? match.Groups["value"].Value
                         : match.Value)
                     .GroupBy(value => value, StringComparer.Ordinal))
        {
            entries.Add(new BaselineEntry(path, category, group.Key, group.Count()));
        }
    }

    private static IEnumerable<string> ProductionXamlFiles()
    {
        var uiRoot = FindRepoFile("src/PlayStead.UI");
        return Directory
            .EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path =>
                !path.EndsWith(
                    Path.Combine("Themes", "PlaySteadTokens.xaml"),
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string RelativePath(string path) =>
        Path.GetRelativePath(FindRepoFile(string.Empty), path)
            .Replace('\\', '/');

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "PlayStead.UI")))
            {
                return Path.Combine(
                    directory.FullName,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("PlayStead repository root was not found.");
    }

    private sealed record BaselineEntry(
        string Path,
        string Category,
        string Value,
        int Count);

    [GeneratedRegex("(?<value>#[0-9A-Fa-f]{6,8})")]
    private static partial Regex HexColorPattern();

    [GeneratedRegex("FontSize=\"(?<value>[^\"]+)\"")]
    private static partial Regex FontSizePattern();

    [GeneratedRegex("FontWeight=\"(?<value>[^\"]+)\"")]
    private static partial Regex FontWeightPattern();

    [GeneratedRegex("CornerRadius=\"(?<value>[0-9][^\"]*)\"")]
    private static partial Regex CornerRadiusPattern();

    [GeneratedRegex("(?:Margin|Padding)=\"(?<value>[0-9][^\"]*)\"")]
    private static partial Regex SpacingPattern();

    [GeneratedRegex("<Button\\.Style>[\\s\\S]*?</Button\\.Style>")]
    private static partial Regex ButtonStylePattern();

    [GeneratedRegex("Setter Property=\"(?:Background|Foreground|BorderBrush|CornerRadius|Padding|FontFamily|FontSize|FontWeight)\"")]
    private static partial Regex FundamentalButtonSetterPattern();

}
