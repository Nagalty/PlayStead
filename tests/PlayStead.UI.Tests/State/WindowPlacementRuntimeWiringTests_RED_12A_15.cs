using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.State;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.State;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class WindowPlacementRuntimeWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        nameof(WindowPlacementRuntimeWiringTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Closing_main_window_persists_normal_bounds_under_layout_data_directory()
    {
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();

        var placementPath = Path.Combine(
            Path.GetDirectoryName(layout.DatabasePath)!,
            "window-placement.json");

        using var host = PlaySteadHost.Build(layout);

        RunSta(
            () =>
            {
                var window = host.Services.GetRequiredService<MainWindow>();

                window.WindowStartupLocation =
                    System.Windows.WindowStartupLocation.Manual;

                window.WindowState =
                    System.Windows.WindowState.Normal;

                window.Left = 140;
                window.Top = 90;
                window.Width = 1180;
                window.Height = 760;

                host.Services.GetRequiredService<PlayStead.UI.Tray.WindowClosePolicy>()
                    .RequestExit();
                window.Close();
            });

        Assert.True(
            File.Exists(placementPath),
            $"Expected placement file at '{placementPath}'.");

        var state = JsonSerializer.Deserialize<WindowPlacementState>(
            File.ReadAllText(placementPath));

        Assert.NotNull(state);
        Assert.Equal(140, state.Left, precision: 3);
        Assert.Equal(90, state.Top, precision: 3);
        Assert.Equal(1180, state.Width, precision: 3);
        Assert.Equal(760, state.Height, precision: 3);
        Assert.False(state.IsMaximized);
    }

    [Fact]
    public async Task Saved_placement_is_restored_when_main_window_is_created()
    {
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();

        var placementPath = Path.Combine(
            Path.GetDirectoryName(layout.DatabasePath)!,
            "window-placement.json");

        var placementService =
            new WindowPlacementService(placementPath);

        await placementService.SaveAsync(
            new WindowPlacementState(
                Left: 210,
                Top: 130,
                Width: 1260,
                Height: 820,
                IsMaximized: false),
            CancellationToken.None);

        using var host = PlaySteadHost.Build(layout);

        RunSta(
            () =>
            {
                var window = host.Services.GetRequiredService<MainWindow>();

                Assert.Equal(
                    System.Windows.WindowStartupLocation.Manual,
                    window.WindowStartupLocation);

                Assert.Equal(210, window.Left, precision: 3);
                Assert.Equal(130, window.Top, precision: 3);
                Assert.Equal(1260, window.Width, precision: 3);
                Assert.Equal(820, window.Height, precision: 3);
                Assert.Equal(
                    System.Windows.WindowState.Normal,
                    window.WindowState);

                host.Services.GetRequiredService<PlayStead.UI.Tray.WindowClosePolicy>()
                    .RequestExit();
                window.Close();
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }

    private static void RunSta(Action action)
        => PlaySteadWpfTestResources.Run(action);
}
