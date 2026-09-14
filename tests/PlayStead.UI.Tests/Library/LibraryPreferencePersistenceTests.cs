using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Library;
using PlayStead.UI.Settings;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryPreferencePersistenceTests :
    IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public void Ui_preferences_have_safe_library_defaults()
    {
        var preferences =
            new UiPreferences();

        Assert.False(
            preferences.ReduceMotion);

        Assert.Equal(
            LibraryViewMode.Grid,
            preferences.LibraryViewMode);

        Assert.Equal(
            "Title",
            preferences.LibrarySortKey);

        Assert.Null(
            preferences.LibraryFilterKey);
    }

    [Fact]
    public void Library_accepts_only_sort_and_filter_keys_backed_by_real_metadata()
    {
        using var host =
            CreateHost();

        var sut =
            host.Services
                .GetRequiredService<LibraryViewModel>();

        sut.SetSortKey(
            "Title");

        Assert.Equal(
            "Title",
            sut.SortKey);

        sut.SetSortKey(
            "Provider");

        Assert.Equal(
            "Provider",
            sut.SortKey);

        sut.SetFilterKey(
            "Steam");

        Assert.Equal(
            "Steam",
            sut.FilterKey);

        sut.SetFilterKey(
            null);

        Assert.Null(
            sut.FilterKey);

        Action setUnsupportedSort =
            () => sut.SetSortKey(
                "LastPlayed");

        Action setUnsupportedFilter =
            () => sut.SetFilterKey(
                "Genre:RPG");

        Assert.Throws<
            ArgumentOutOfRangeException>(
            setUnsupportedSort);

        Assert.Throws<
            ArgumentOutOfRangeException>(
            setUnsupportedFilter);
    }

    [Fact]
    public async Task Library_preferences_roundtrip_without_overwriting_reduce_motion()
    {
        using var host =
            CreateHost();

        var store =
            host.Services
                .GetRequiredService<UiPreferencesStore>();

        await store.SaveAsync(
            new UiPreferences(
                ReduceMotion: true,
                LibraryViewMode:
                    LibraryViewMode.Grid,
                LibrarySortKey:
                    "Title",
                LibraryFilterKey:
                    null),
            CancellationToken.None);

        var sut =
            host.Services
                .GetRequiredService<LibraryViewModel>();

        await sut.LoadUiPreferencesAsync(
            CancellationToken.None);

        Assert.Equal(
            LibraryViewMode.Grid,
            sut.ViewMode);

        Assert.Equal(
            "Title",
            sut.SortKey);

        Assert.Null(
            sut.FilterKey);

        sut.SetViewMode(
            LibraryViewMode.List);

        sut.SetSortKey(
            "Provider");

        sut.SetFilterKey(
            "Steam");

        await sut.SaveUiPreferencesAsync(
            CancellationToken.None);

        var persisted =
            await store.LoadAsync(
                CancellationToken.None);

        Assert.True(
            persisted.ReduceMotion);

        Assert.Equal(
            LibraryViewMode.List,
            persisted.LibraryViewMode);

        Assert.Equal(
            "Provider",
            persisted.LibrarySortKey);

        Assert.Equal(
            "Steam",
            persisted.LibraryFilterKey);
    }

    [Fact]
    public async Task Settings_save_updates_motion_and_default_view_without_losing_library_sort_or_filter()
    {
        using var host =
            CreateHost();

        var store =
            host.Services
                .GetRequiredService<UiPreferencesStore>();

        await store.SaveAsync(
            new UiPreferences(
                ReduceMotion: false,
                LibraryViewMode:
                    LibraryViewMode.List,
                LibrarySortKey:
                    "Provider",
                LibraryFilterKey:
                    "Steam"),
            CancellationToken.None);

        var settings =
            host.Services
                .GetRequiredService<SettingsViewModel>();

        await settings.LoadAsync(
            CancellationToken.None);

        Assert.Equal(
            LibraryViewMode.List,
            settings.LibraryViewMode);

        settings.ReduceMotion =
            true;

        settings.LibraryViewMode =
            LibraryViewMode.Grid;

        await settings.SaveAsync(
            CancellationToken.None);

        var persisted =
            await store.LoadAsync(
                CancellationToken.None);

        Assert.True(
            persisted.ReduceMotion);

        Assert.Equal(
            LibraryViewMode.Grid,
            persisted.LibraryViewMode);

        Assert.Equal(
            "Provider",
            persisted.LibrarySortKey);

        Assert.Equal(
            "Steam",
            persisted.LibraryFilterKey);
    }

    private Microsoft.Extensions.Hosting.IHost
        CreateHost()
    {
        Directory.CreateDirectory(
            _root);

        var layout =
            UserDataLayout.FromRoot(
                _root);

        return PlaySteadHost.Build(
            layout);
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
