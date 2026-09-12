using PlayStead.UI.State;

namespace PlayStead.UI.Tests.State;

public sealed class WindowPlacementServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_then_load_preserves_normal_bounds_and_maximized_state()
    {
        Directory.CreateDirectory(_root);

        var filePath = Path.Combine(
            _root,
            "window-placement.json");

        var sut = new WindowPlacementService(filePath);

        var expected = new WindowPlacementState(
            Left: 120,
            Top: 80,
            Width: 1600,
            Height: 900,
            IsMaximized: true);

        await sut.SaveAsync(
            expected,
            CancellationToken.None);

        var actual = await sut.LoadAsync(
            WorkAreas.Single(
                left: 0,
                top: 0,
                width: 2560,
                height: 1440),
            CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Load_recenters_saved_window_when_it_is_outside_all_current_work_areas()
    {
        Directory.CreateDirectory(_root);

        var filePath = Path.Combine(
            _root,
            "window-placement.json");

        var sut = new WindowPlacementService(filePath);

        await sut.SaveAsync(
            new WindowPlacementState(
                Left: 5000,
                Top: 3000,
                Width: 1200,
                Height: 800,
                IsMaximized: false),
            CancellationToken.None);

        var workAreas = WorkAreas.Single(
            left: 0,
            top: 0,
            width: 1920,
            height: 1080);

        var actual = await sut.LoadAsync(
            workAreas,
            CancellationToken.None);

        Assert.NotNull(actual);

        Assert.Equal(360, actual.Left);
        Assert.Equal(140, actual.Top);
        Assert.Equal(1200, actual.Width);
        Assert.Equal(800, actual.Height);
        Assert.False(actual.IsMaximized);
    }

    [Fact]
    public async Task Load_returns_null_when_no_saved_placement_exists()
    {
        var filePath = Path.Combine(
            _root,
            "window-placement.json");

        var sut = new WindowPlacementService(filePath);

        var actual = await sut.LoadAsync(
            WorkAreas.Single(
                left: 0,
                top: 0,
                width: 1920,
                height: 1080),
            CancellationToken.None);

        Assert.Null(actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static class WorkAreas
    {
        public static IReadOnlyList<WindowWorkArea> Single(
            double left,
            double top,
            double width,
            double height) =>
            [
                new WindowWorkArea(
                    left,
                    top,
                    width,
                    height,
                    IsPrimary: true)
            ];
    }
}
