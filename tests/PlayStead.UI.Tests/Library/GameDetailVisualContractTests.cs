namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailVisualContractTests
{
    [Fact]
    public void Detail_reuses_shared_visual_components()
    {
        var xaml = ReadView();

        Assert.Contains("controls:GameArtwork", xaml, StringComparison.Ordinal);
        Assert.Contains("controls:PlaySplitButton", xaml, StringComparison.Ordinal);
        Assert.Contains("controls:SectionHeader", xaml, StringComparison.Ordinal);
        Assert.Contains("controls:StatusBadge", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_uses_playstead_design_system_resources()
    {
        var xaml = ReadView();

        Assert.Contains("PlayStead.Brush.", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Spacing.", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Radius.", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Font.Body", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreground=\"#", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detail_keeps_one_primary_action_and_existing_layout_contracts()
    {
        var xaml = ReadView();

        Assert.Equal(
            1,
            CountOccurrences(xaml, "controls:PlaySplitButton"));
        Assert.Contains("GameActivity", xaml, StringComparison.Ordinal);
        Assert.Contains("InstallationPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("DetailColumns", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_does_not_render_future_modules_or_internal_metadata()
    {
        var xaml = ReadView();

        Assert.DoesNotContain("CanonicalContentId", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamGridDB", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IGDB", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Screenshots", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Achievements", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadView() =>
        File.ReadAllText(
            FindUiFile(
                Path.Combine(
                    "Library",
                    "GameDetailView.xaml")));

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
}
