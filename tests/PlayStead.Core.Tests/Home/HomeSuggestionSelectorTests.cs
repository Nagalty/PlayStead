using PlayStead.Core.Home;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.ProviderInstallUpdate;
using Metadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Tests.Home;

public sealed class HomeSuggestionSelectorTests
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Thursday)]
    public async Task Monday_thursday_allow_general_installed_pool(DayOfWeek day)
    {
        var game = Game("Solo");
        var result = await Select(day, [game], [], []);
        Assert.Equal(game.Id, result!.GameId);
    }

    [Theory]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Sunday)]
    public async Task Weekend_excludes_false_and_unknown_coop_and_keeps_true(DayOfWeek day)
    {
        var falseGame = Game("False");
        var unknownGame = Game("Unknown");
        var trueGame = Game("True");
        var metadata = new[] { Meta(falseGame, false, false), Meta(trueGame, true, null) };
        var result = await Select(day, [falseGame, unknownGame, trueGame], metadata, []);
        Assert.Equal(trueGame.Id, result!.GameId);
    }

    [Fact]
    public async Task Weekend_with_no_coop_returns_null_without_solo_fallback()
    {
        var game = Game("Solo");
        Assert.Null(await Select(DayOfWeek.Friday, [game], [Meta(game, false, false)], []));
    }

    [Fact]
    public async Task Selected_game_carries_matching_provider_metadata_for_editorial_projection()
    {
        var game = Game("Enshrouded");
        var metadata = Metadata.Create(game.Id, ProviderKind.Steam, game.Id.Value.ToString("N"), Monday,
            onlineCoop: true, shortDescription: "Une courte description.");

        var result = await Select(DayOfWeek.Friday, [game], [metadata], []);

        Assert.Equal("Une courte description.", result!.Metadata!.ShortDescription);
        Assert.True(result.Metadata.OnlineCoop);
    }

    [Fact]
    public async Task Selected_game_carries_actionable_install_update_state_only()
    {
        var game = Game("Helldivers 2");
        var state = new ProviderInstallUpdateState(
            game.Id, ProviderKind.Steam, game.Id.Value.ToString("N"), "1", "2",
            ProviderInstallUpdateStatus.UpdateAvailable, null, null, null, null, null, null, Monday);

        var result = await new HomeSuggestionSelector(new Store(), Clock(DayOfWeek.Friday), new ZeroRandom())
            .SelectAsync(Snapshot(game), [Meta(game, true, null)], [], CancellationToken.None, [state]);

        Assert.Equal(ProviderInstallUpdateStatus.UpdateAvailable, result!.UpdateState!.Status);
    }

    [Fact]
    public async Task Active_game_is_excluded_but_shortlist_history_and_dormant_are_not()
    {
        var active = Game("Active");
        var other = Game("Other");
        var result = await Select(DayOfWeek.Monday, [active, other], [], [active.Id]);
        Assert.Equal(other.Id, result!.GameId);
    }

    [Fact]
    public async Task Selection_is_stable_for_session_and_refreshes()
    {
        var first = Game("First");
        var second = Game("Second");
        var store = new Store();
        var selector = new HomeSuggestionSelector(store, Clock(DayOfWeek.Monday), new ZeroRandom());
        var library = Snapshot(first, second);
        var selected = await selector.SelectAsync(library, [], [], CancellationToken.None);
        Assert.Equal(selected, await selector.SelectAsync(Snapshot(first, second, Game("New")), [], [], CancellationToken.None));
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task Previous_suggestion_is_excluded_when_alternative_exists()
    {
        var first = Game("First");
        var second = Game("Second");
        var store = new Store { Previous = first.Id };
        var selector = new HomeSuggestionSelector(store, Clock(DayOfWeek.Monday), new ZeroRandom());
        var result = await selector.SelectAsync(Snapshot(first, second), [], [], CancellationToken.None);
        Assert.Equal(second.Id, result!.GameId);
    }

    [Fact]
    public async Task Invalid_selected_game_clears_without_reroll()
    {
        var first = Game("First");
        var second = Game("Second");
        var store = new Store();
        var selector = new HomeSuggestionSelector(store, Clock(DayOfWeek.Monday), new ZeroRandom());
        var selected = await selector.SelectAsync(Snapshot(first, second), [], [], CancellationToken.None);
        var remaining = selected!.GameId == first.Id ? second : first;
        Assert.Null(await selector.SelectAsync(Snapshot(remaining), [], [], CancellationToken.None));
    }

    private static async Task<HomeSuggestion?> Select(DayOfWeek day, IReadOnlyList<LogicalGame> games, IReadOnlyCollection<Metadata> metadata, IReadOnlyCollection<GameId> active) =>
        await new HomeSuggestionSelector(new Store(), Clock(day), new ZeroRandom()).SelectAsync(Snapshot(games.ToArray()), metadata, active, CancellationToken.None);

    private static LibrarySnapshot Snapshot(params LogicalGame[] games) => new(games, games.Select(x => new GameInstallation(InstallationId.New(), x.Id, ProviderKind.Steam, x.Id.Value.ToString("N"), "C:\\Games", null, true, true, Monday)).ToArray());
    private static LogicalGame Game(string title) => new(GameId.New(), title, false, Monday, Monday);
    private static Metadata Meta(LogicalGame game, bool? online, bool? local) => Metadata.Create(game.Id, ProviderKind.Steam, game.Id.Value.ToString("N"), Monday, onlineCoop: online, localCoop: local);
    private static TimeProvider Clock(DayOfWeek day) => new FixedTimeProvider(Monday.AddDays(((int)day - (int)DayOfWeek.Monday + 7) % 7));

    private sealed class Store : IHomeSuggestionSelectionStore
    {
        public GameId? Previous { get; set; }
        public int Writes { get; private set; }
        public Task<GameId?> GetLastSuggestionGameIdAsync(CancellationToken _) => Task.FromResult(Previous);
        public Task SetLastSuggestionGameIdAsync(GameId? gameId, CancellationToken _) { Previous = gameId; Writes++; return Task.CompletedTask; }
    }

    private sealed class ZeroRandom : Random { public override int Next(int maxValue) => 0; }
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc; }
}
