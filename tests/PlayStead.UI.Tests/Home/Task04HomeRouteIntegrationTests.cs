using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Home;

namespace PlayStead.UI.Tests.Home;

public sealed class Task04HomeRouteIntegrationTests :
    IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            nameof(Task04HomeRouteIntegrationTests),
            Guid.NewGuid().ToString("N"));

    [Fact]
    public void Host_registers_HomeViewModel_as_a_singleton()
    {
        var layout =
            UserDataLayout.FromRoot(
                _root);

        layout.EnsureDirectoriesExist();

        using var host =
            PlaySteadHost.Build(
                layout);

        var first =
            host.Services
                .GetRequiredService<
                    HomeViewModel>();

        var second =
            host.Services
                .GetRequiredService<
                    HomeViewModel>();

        Assert.Same(
            first,
            second);
    }

    [Fact]
    public void MainWindow_source_depends_on_HomeViewModel_and_HomeView()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "using PlayStead.UI.Home;",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "HomeViewModel",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "HomeView",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_Home_route_materializes_HomeView_in_MainContent()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "case AppRoute.Home:",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "new HomeView(",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "MainContent.Content",
            source,
            StringComparison.Ordinal);

        var homeCaseIndex =
            source.IndexOf(
                "case AppRoute.Home:",
                StringComparison.Ordinal);

        var attentionCaseIndex =
            source.IndexOf(
                "case AppRoute.Attention:",
                homeCaseIndex,
                StringComparison.Ordinal);

        Assert.True(
            homeCaseIndex >= 0);

        Assert.True(
            attentionCaseIndex > homeCaseIndex);

        var homeCase =
            source.Substring(
                homeCaseIndex,
                attentionCaseIndex - homeCaseIndex);

        Assert.Contains(
            "new HomeView(",
            homeCase,
            StringComparison.Ordinal);

        Assert.Contains(
            "MainContent.Content",
            homeCase,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "MainContent.Content = null",
            homeCase,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_reuses_a_single_HomeView_instance()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "HomeView?",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "??=",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "new HomeView(",
            source,
            StringComparison.Ordinal);
    }

    private static string FindUiFile(
        string relativePath)
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI");

            if (Directory.Exists(
                    uiDirectory))
            {
                var path =
                    Path.Combine(
                        uiDirectory,
                        relativePath);

                Assert.True(
                    File.Exists(
                        path),
                    $"Required UI file missing: {relativePath}");

                return path;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite
            .SqliteConnection
            .ClearAllPools();

        if (Directory.Exists(
                _root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
