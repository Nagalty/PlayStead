using System.IO;
using System.Windows;
using PlayStead.UI.Settings;

namespace PlayStead.UI.Tests.Settings;

public sealed class UiMotionPreferenceCoordinatorTests
{
    [Fact]
    public async Task InitializeAsync_loads_persisted_reduce_motion_and_applies_it()
    {
        using var temp =
            new TemporaryDirectory();

        var path =
            Path.Combine(
                temp.Path,
                "ui-preferences.json");

        var store =
            new UiPreferencesStore(
                path);

        await store.SaveAsync(
            new UiPreferences(
                ReduceMotion: true),
            CancellationToken.None);

        var viewModel =
            new SettingsViewModel(
                store);

        var resources =
            CreateResources();

        var sut =
            new UiMotionPreferenceCoordinator(
                viewModel,
                new UiMotionController(),
                resources);

        await sut.InitializeAsync(
            CancellationToken.None);

        Assert.True(
            viewModel.ReduceMotion);

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    [Fact]
    public async Task InitializeAsync_with_default_preferences_keeps_nominal_motion()
    {
        using var temp =
            new TemporaryDirectory();

        var viewModel =
            new SettingsViewModel(
                new UiPreferencesStore(
                    Path.Combine(
                        temp.Path,
                        "ui-preferences.json")));

        var resources =
            CreateResources();

        var sut =
            new UiMotionPreferenceCoordinator(
                viewModel,
                new UiMotionController(),
                resources);

        await sut.InitializeAsync(
            CancellationToken.None);

        Assert.False(
            viewModel.ReduceMotion);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                120),
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                180),
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    [Fact]
    public async Task ReduceMotion_changes_apply_immediately_after_initialization()
    {
        using var temp =
            new TemporaryDirectory();

        var viewModel =
            new SettingsViewModel(
                new UiPreferencesStore(
                    Path.Combine(
                        temp.Path,
                        "ui-preferences.json")));

        var resources =
            CreateResources();

        var sut =
            new UiMotionPreferenceCoordinator(
                viewModel,
                new UiMotionController(),
                resources);

        await sut.InitializeAsync(
            CancellationToken.None);

        viewModel.ReduceMotion =
            true;

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));

        viewModel.ReduceMotion =
            false;

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                120),
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                180),
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    [Fact]
    public void Host_registers_UiMotionController_as_a_singleton()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "Bootstrap/PlaySteadHost.cs"));

        Assert.Contains(
            "UiMotionController",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "AddSingleton",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_initializes_the_motion_preference_coordinator_with_application_resources()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "UiMotionPreferenceCoordinator",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "UiMotionController",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "Application.Current.Resources",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "InitializeAsync(",
            source,
            StringComparison.Ordinal);
    }

    private static ResourceDictionary
        CreateResources()
    {
        return new ResourceDictionary
        {
            ["PlayStead.Duration.Fast"] =
                new Duration(
                    TimeSpan.FromMilliseconds(
                        120)),

            ["PlayStead.Duration.Normal"] =
                new Duration(
                    TimeSpan.FromMilliseconds(
                        180))
        };
    }

    private static TimeSpan GetDuration(
        ResourceDictionary resources,
        string key)
    {
        var duration =
            Assert.IsType<Duration>(
                resources[key]);

        Assert.True(
            duration.HasTimeSpan);

        return duration.TimeSpan;
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

    private sealed class TemporaryDirectory :
        IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "PlayStead.Tests",
                    nameof(UiMotionPreferenceCoordinatorTests),
                    Guid.NewGuid()
                        .ToString("N"));

            Directory.CreateDirectory(
                Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
