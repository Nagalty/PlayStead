using PlayStead.Core.Home;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Shortlist;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Core.Tests.Home;

public sealed class HomeDormantGameProjectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Editorial_contract_exposes_optional_dormant_game_and_evaluation_time()
    {
        var snapshotProperty = typeof(HomeEditorialSnapshot).GetProperty("DormantGame");
        var inputProperty = typeof(HomeEditorialProjectionInput).GetProperty("EvaluatedAtUtc");

        Assert.NotNull(snapshotProperty);
        Assert.Equal("HomeEditorialDormantGame", snapshotProperty!.PropertyType.Name.TrimEnd('?'));
        Assert.NotNull(inputProperty);
        Assert.Equal(typeof(DateTimeOffset), inputProperty!.PropertyType);

        var dormantType = snapshotProperty.PropertyType;
        Assert.NotNull(dormantType.GetProperty("Game"));
        Assert.Equal(typeof(DateTimeOffset), dormantType.GetProperty("LastCompletedAtUtc")?.PropertyType);
        Assert.Equal(typeof(int), dormantType.GetProperty("DaysSinceLastPlayed")?.PropertyType);
        Assert.Equal(typeof(GameId), dormantType.GetProperty("NavigationGameId")?.PropertyType);
    }

    [Fact]
    public void No_game_with_a_completed_session_at_least_45_days_old_returns_null()
    {
        var game = Game("Never played");

        var snapshot = Project([game]);

        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Completed_exactly_45_days_ago_is_eligible()
    {
        var primary = Game("Primary");
        var game = Game("Boundary");

        var snapshot = Project(
            [primary, game],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(game, Now.AddDays(-45))]);

        Assert.Equal(game.Id, snapshot.DormantGame?.Game.GameId);
        Assert.Equal(Now.AddDays(-45), snapshot.DormantGame?.LastCompletedAtUtc);
        Assert.Equal(45, snapshot.DormantGame?.DaysSinceLastPlayed);
        Assert.Equal(game.Id, snapshot.DormantGame?.NavigationGameId);
    }

    [Fact]
    public void Provider_last_played_overrides_observed_timestamp()
    {
        var primary = Game("Primary");
        var game = Game("Provider dormant");
        var providerDate = Now.AddDays(-60);
        var snapshot = HomeEditorialProjector.Project(Input(
            [primary, game],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(game, Now.AddDays(-2))],
            providerActivity: [new ProviderActivityMetadata(game.Id, ProviderKind.Steam, "42", TimeSpan.FromHours(2), providerDate, Now, ProviderActivityAvailability.Complete)]));

        Assert.Equal(providerDate, snapshot.DormantGame?.LastCompletedAtUtc);
        Assert.Equal(HomeEditorialLastPlayedSource.Provider, snapshot.DormantGame?.LastPlayedSource);
    }

    [Fact]
    public void Completed_less_than_45_days_ago_is_excluded()
    {
        var primary = Game("Primary");
        var game = Game("Too recent");

        var snapshot = Project(
            [primary, game],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(game, Now.AddDays(-45).AddMinutes(1))]);

        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Oldest_latest_completed_session_wins()
    {
        var primary = Game("Primary");
        var oldest = Game("Oldest");
        var newer = Game("Newer");

        var snapshot = Project(
            [primary, oldest, newer],
            shortlist: [Shortlist(primary, 0)],
            recent:
            [
                Completed(newer, Now.AddDays(-50)),
                Completed(oldest, Now.AddDays(-80))
            ]);

        Assert.Equal(oldest.Id, snapshot.DormantGame?.Game.GameId);
    }

    [Fact]
    public void Latest_completed_session_per_game_is_authoritative()
    {
        var primary = Game("Primary");
        var game = Game("Returned recently");

        var snapshot = Project(
            [primary, game],
            shortlist: [Shortlist(primary, 0)],
            recent:
            [
                Completed(game, Now.AddDays(-100)),
                Completed(game, Now.AddDays(-10))
            ]);

        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Active_game_is_excluded_even_with_old_history()
    {
        var game = Game("Running");

        var snapshot = Project(
            [game],
            active: [Active(game)],
            recent: [Completed(game, Now.AddDays(-100))]);

        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Primary_game_is_excluded()
    {
        var primary = Game("Primary");

        var snapshot = Project(
            [primary],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(primary, Now.AddDays(-100))]);

        Assert.Equal(primary.Id, snapshot.Primary.NavigationGameId);
        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Remaining_games_du_moment_are_excluded()
    {
        var primary = Game("Primary");
        var remaining = Game("Remaining");

        var snapshot = Project(
            [primary, remaining],
            shortlist: [Shortlist(primary, 0), Shortlist(remaining, 1)],
            recent: [Completed(remaining, Now.AddDays(-100))]);

        Assert.Equal([remaining.Id], snapshot.RemainingGamesDuMoment.Select(game => game.GameId));
        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Unavailable_game_is_excluded()
    {
        var game = Game("Uninstalled");

        var snapshot = Project(
            [game],
            recent: [Completed(game, Now.AddDays(-100))],
            present: []);

        Assert.Null(snapshot.DormantGame);
    }

    [Fact]
    public void Equal_last_completed_times_use_stable_game_id_tiebreaker()
    {
        var primary = Game("Primary");
        var lower = Game("Lower", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higher = Game("Higher", Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var sameTime = Now.AddDays(-60);

        var snapshot = Project(
            [primary, higher, lower],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(higher, sameTime), Completed(lower, sameTime)]);

        Assert.Equal(lower.Id, snapshot.DormantGame?.Game.GameId);
    }

    [Fact]
    public void Repeated_projections_are_deterministic()
    {
        var primary = Game("Primary");
        var first = Game("First", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = Game("Second", Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var input = Input(
            [primary, second, first],
            shortlist: [Shortlist(primary, 0)],
            recent:
            [
                Completed(second, Now.AddDays(-60)),
                Completed(first, Now.AddDays(-60))
            ]);

        var projection1 = HomeEditorialProjector.Project(input);
        var projection2 = HomeEditorialProjector.Project(input);

        Assert.Equal(
            projection1.DormantGame?.Game.GameId,
            projection2.DormantGame?.Game.GameId);
        Assert.Equal(
            projection1.DormantGame?.LastCompletedAtUtc,
            projection2.DormantGame?.LastCompletedAtUtc);
        Assert.Equal(
            projection1.DormantGame?.DaysSinceLastPlayed,
            projection2.DormantGame?.DaysSinceLastPlayed);
    }

    [Fact]
    public void Selection_does_not_depend_on_input_order_scoring_or_randomness()
    {
        var primary = Game("Primary");
        var lower = Game("Lower", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higher = Game("Higher", Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var sameTime = Now.AddDays(-60);

        var forward = Project(
            [primary, lower, higher],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(lower, sameTime), Completed(higher, sameTime)]);
        var reversed = Project(
            [primary, higher, lower],
            shortlist: [Shortlist(primary, 0)],
            recent: [Completed(higher, sameTime), Completed(lower, sameTime)]);

        Assert.Equal(lower.Id, forward.DormantGame?.Game.GameId);
        Assert.Equal(
            forward.DormantGame?.Game.GameId,
            reversed.DormantGame?.Game.GameId);
        Assert.Equal(
            forward.DormantGame?.LastCompletedAtUtc,
            reversed.DormantGame?.LastCompletedAtUtc);
    }

    [Fact]
    public void Projection_does_not_mutate_shortlist_or_history()
    {
        var primary = Game("Primary");
        var dormant = Game("Dormant");
        GamesDuMomentEntry[] shortlist = [Shortlist(primary, 0)];
        GameSession[] history = [Completed(dormant, Now.AddDays(-60))];
        var shortlistBefore = shortlist.ToArray();
        var historyBefore = history.ToArray();

        _ = Project([primary, dormant], shortlist: shortlist, recent: history);

        Assert.Equal(shortlistBefore, shortlist);
        Assert.Equal(historyBefore, history);
    }

    private static HomeEditorialSnapshot Project(
        IReadOnlyList<LogicalGame> games,
        IReadOnlyList<GameSession>? active = null,
        IReadOnlyList<GamesDuMomentEntry>? shortlist = null,
        IReadOnlyList<GameSession>? recent = null,
        IReadOnlyList<GameId>? present = null,
        IReadOnlyList<ProviderActivityMetadata>? providerActivity = null) =>
        HomeEditorialProjector.Project(Input(games, active, shortlist, recent, present, providerActivity));

    private static HomeEditorialProjectionInput Input(
        IReadOnlyList<LogicalGame> games,
        IReadOnlyList<GameSession>? active = null,
        IReadOnlyList<GamesDuMomentEntry>? shortlist = null,
        IReadOnlyList<GameSession>? recent = null,
        IReadOnlyList<GameId>? present = null,
        IReadOnlyList<ProviderActivityMetadata>? providerActivity = null)
    {
        var presentIds = present ?? games.Select(game => game.Id).ToArray();
        var installations = presentIds.Select((gameId, index) => new GameInstallation(
            InstallationId.New(),
            gameId,
            ProviderKind.Manual,
            $"game-{index}",
            $"C:\\Games\\{index}",
            null,
            true,
            true,
            Now)).ToArray();

        return new HomeEditorialProjectionInput(
            new LibrarySnapshot(games, installations),
            active ?? [],
            shortlist ?? [],
            recent ?? [],
            WeeklyActivitySummary.Empty,
            [],
            Now,
            providerActivity);
    }

    private static LogicalGame Game(string title, Guid? id = null) => new(
        id is Guid value ? new GameId(value) : GameId.New(),
        title,
        false,
        Now,
        Now);

    private static GamesDuMomentEntry Shortlist(LogicalGame game, int position) =>
        new(game.Id, position, Now.AddMinutes(position));

    private static GameSession Active(LogicalGame game) =>
        Session(game, SessionState.Active, Now.AddHours(-1), null);

    private static GameSession Completed(LogicalGame game, DateTimeOffset endedAtUtc) =>
        Session(game, SessionState.Ended, endedAtUtc.AddHours(-1), endedAtUtc);

    private static GameSession Session(
        LogicalGame game,
        SessionState state,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? endedAtUtc) =>
        new(
            Guid.NewGuid(),
            game.Id.Value,
            startedAtUtc,
            endedAtUtc ?? Now,
            endedAtUtc,
            state,
            endedAtUtc is null ? null : SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            startedAtUtc,
            endedAtUtc ?? Now);
}
