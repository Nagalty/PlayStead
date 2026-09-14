using PlayStead.UI.Settings;

namespace PlayStead.UI.Tests.Settings;

public sealed class UiPreferencesStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_defaults_when_preferences_file_does_not_exist()
    {
        using var temp =
            new TemporaryDirectory();

        var sut =
            new UiPreferencesStore(
                Path.Combine(
                    temp.Path,
                    "ui-preferences.json"));

        var actual =
            await sut.LoadAsync(
                CancellationToken.None);

        Assert.False(
            actual.ReduceMotion);
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_reduce_motion()
    {
        using var temp =
            new TemporaryDirectory();

        var path =
            Path.Combine(
                temp.Path,
                "ui-preferences.json");

        var sut =
            new UiPreferencesStore(
                path);

        await sut.SaveAsync(
            new UiPreferences(
                ReduceMotion: true),
            CancellationToken.None);

        var actual =
            await sut.LoadAsync(
                CancellationToken.None);

        Assert.True(
            actual.ReduceMotion);
    }

    [Fact]
    public async Task SaveAsync_creates_parent_directory_when_needed()
    {
        using var temp =
            new TemporaryDirectory();

        var path =
            Path.Combine(
                temp.Path,
                "nested",
                "settings",
                "ui-preferences.json");

        var sut =
            new UiPreferencesStore(
                path);

        await sut.SaveAsync(
            new UiPreferences(
                ReduceMotion: true),
            CancellationToken.None);

        Assert.True(
            File.Exists(
                path));
    }

    [Fact]
    public async Task LoadAsync_falls_back_to_defaults_when_json_is_invalid()
    {
        using var temp =
            new TemporaryDirectory();

        var path =
            Path.Combine(
                temp.Path,
                "ui-preferences.json");

        await File.WriteAllTextAsync(
            path,
            "{ invalid json",
            CancellationToken.None);

        var sut =
            new UiPreferencesStore(
                path);

        var actual =
            await sut.LoadAsync(
                CancellationToken.None);

        Assert.False(
            actual.ReduceMotion);
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
                    nameof(UiPreferencesStoreTests),
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
