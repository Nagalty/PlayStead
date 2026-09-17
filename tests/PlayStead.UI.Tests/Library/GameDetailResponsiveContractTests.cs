namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailResponsiveContractTests
{
    [Fact]
    public void Detail_view_declares_adaptive_layout_contract()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        var codeBehind =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml.cs")));

        Assert.Contains(
            "HorizontalScrollBarVisibility=\"Disabled\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"DetailColumns\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "SizeChanged=\"GameDetailView_OnSizeChanged\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "1100",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "DetailColumns",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_remains_outside_the_adaptive_detail_columns()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.Contains(
            "<Border Grid.Row=\"0\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "controls:GameArtwork",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Adaptive_columns_can_stack_the_activity_and_installation_modules()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.Contains(
            "GameActivity",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "InstallationPanel",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "InstallationInformation",
            xaml,
            StringComparison.Ordinal);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(directory.FullName, "src", "PlayStead.UI");

            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }
}
