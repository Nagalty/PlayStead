using PlayStead.Core.Library;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailInstallationTests
{
    [Fact]
    public void Installation_module_exposes_real_local_fields()
    {
        var item =
            new LibraryItemViewModel(
                GameId.New(),
                "Game",
                ProviderKind.Steam,
                "Steam",
                @"C:\Games\Game",
                42_000_000_000,
                SteamUpdateState.UpToDate,
                IsSessionActive: true);

        var viewModel =
            new GameDetailViewModel(item);

        Assert.Equal("Steam", viewModel.ProviderLabel);
        Assert.Equal(@"C:\Games\Game", viewModel.InstallPath);
        Assert.Equal("42,0 Go", viewModel.InstalledSizeLabel);
        Assert.Equal("À jour", viewModel.SteamStatusLabel);
        Assert.Equal("En cours", viewModel.SessionStatusLabel);
    }

    [Fact]
    public void Installation_view_hides_absent_path_size_and_status_independently()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.Contains(
            "x:Name=\"InstallationPanel\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"InstallationPathRow\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "HasInstallPath",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"InstalledSizeRow\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "HasInstalledSize",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"LocalStatusPanel\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"SteamStatusRow\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "HasSteamStatus",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "BooleanToVisibilityConverter",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_does_not_display_internal_ids_or_future_metadata()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.DoesNotContain(
            "CanonicalContentId",
            xaml,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "SteamGridDB",
            xaml,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "IGDB",
            xaml,
            StringComparison.OrdinalIgnoreCase);
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
