using CommunityToolkit.Mvvm.Input;
using PlayStead.UI.Settings;

namespace PlayStead.UI.Tests.Settings;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task LoadAsync_reads_persisted_reduce_motion_preference()
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

        var sut =
            new SettingsViewModel(
                store);

        await sut.LoadAsync(
            CancellationToken.None);

        Assert.True(
            sut.ReduceMotion);
    }

    [Fact]
    public async Task SaveAsync_persists_current_reduce_motion_preference()
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

        var sut =
            new SettingsViewModel(
                store)
            {
                ReduceMotion = true
            };

        await sut.SaveAsync(
            CancellationToken.None);

        var reloaded =
            await store.LoadAsync(
                CancellationToken.None);

        Assert.True(
            reloaded.ReduceMotion);
    }

    [Fact]
    public void ReduceMotion_change_raises_PropertyChanged_exactly_once()
    {
        using var temp =
            new TemporaryDirectory();

        var sut =
            new SettingsViewModel(
                new UiPreferencesStore(
                    Path.Combine(
                        temp.Path,
                        "ui-preferences.json")));

        var changed =
            new List<string?>();

        sut.PropertyChanged +=
            (_, args) =>
                changed.Add(
                    args.PropertyName);

        sut.ReduceMotion =
            true;

        Assert.Equal(
            [nameof(SettingsViewModel.ReduceMotion)],
            changed);
    }

    [Fact]
    public async Task SaveCommand_persists_the_current_preference()
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

        var sut =
            new SettingsViewModel(
                store)
            {
                ReduceMotion = true
            };

        var command =
            Assert.IsAssignableFrom<IAsyncRelayCommand>(
                sut.SaveCommand);

        await command.ExecuteAsync(
            null);

        var reloaded =
            await store.LoadAsync(
                CancellationToken.None);

        Assert.True(
            reloaded.ReduceMotion);
    }

    [Fact]
    public async Task Protection_preferences_round_trip()
    {
        using var temp = new TemporaryDirectory();
        var store = new UiPreferencesStore(Path.Combine(temp.Path, "ui-preferences.json"));
        var sut = new SettingsViewModel(store)
        {
            ProtectionOnboardingCompleted = true,
            AutoProtectRecognizedArtifacts = true
        };

        await sut.SaveAsync(CancellationToken.None);

        var reloaded = await store.LoadAsync(CancellationToken.None);

        Assert.True(reloaded.ProtectionOnboardingCompleted);
        Assert.True(reloaded.AutoProtectRecognizedArtifacts);
    }

    [Fact]
    public async Task Protection_game_override_round_trips_explicit_false()
    {
        using var temp = new TemporaryDirectory();
        var store = new UiPreferencesStore(Path.Combine(temp.Path, "ui-preferences.json"));
        var gameId = Guid.NewGuid();

        await store.SaveAsync(
            new UiPreferences(LocalProtectionEnabledByGame: new Dictionary<Guid, bool> { [gameId] = false }),
            CancellationToken.None);

        var reloaded = await store.LoadAsync(CancellationToken.None);

        Assert.NotNull(reloaded.LocalProtectionEnabledByGame);
        Assert.False(reloaded.LocalProtectionEnabledByGame![gameId]);
    }

    [Fact]
    public void Completed_protection_keeps_the_card_visible_as_a_summary()
    {
        using var temp = new TemporaryDirectory();
        var sut = new SettingsViewModel(
            new UiPreferencesStore(Path.Combine(temp.Path, "ui-preferences.json")))
        {
            ProtectionOnboardingCompleted = true
        };

        Assert.False(sut.IsProtectionOnboardingPending);
        Assert.True(sut.IsProtectionSummaryVisible);
        Assert.True(sut.IsProtectionCardVisible);
        Assert.False(sut.HasProtectionPrompt);
    }

    [Fact]
    public void Pending_protection_without_recognized_artifacts_does_not_show_an_empty_card()
    {
        using var temp = new TemporaryDirectory();
        var sut = new SettingsViewModel(
            new UiPreferencesStore(Path.Combine(temp.Path, "ui-preferences.json")));

        Assert.True(sut.IsProtectionOnboardingPending);
        Assert.False(sut.IsProtectionSummaryVisible);
        Assert.False(sut.IsProtectionCardVisible);
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
                    nameof(SettingsViewModelTests),
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
