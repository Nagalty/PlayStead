using PlayStead.Core.Home;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Sessions;
using PlayStead.Core.Shortlist;

namespace PlayStead.Core.Tests.Home;

public sealed class HomeEditorialProjectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_session_is_projected_separately_while_shortlist_remains_editorial_primary()
    {
        var olderActive = Game("Older active");
        var active = Game("Active");
        var shortlisted = Game("Shortlisted");
        var recent = Game("Recent");

        var snapshot = Project(
            games: [olderActive, active, shortlisted, recent],
            activeSessions:
            [
                Session(olderActive, SessionState.Active, Now.AddMinutes(-20)),
                Session(active, SessionState.Active, Now.AddMinutes(-10))
            ],
            shortlist: [Shortlist(shortlisted, 0)],
            recentSessions: [Session(recent, SessionState.Ended, Now.AddHours(-2))]);

        Assert.Equal(HomeEditorialPrimarySourceKind.GamesDuMoment, snapshot.Primary.SourceKind);
        Assert.Equal(shortlisted.Id, snapshot.Primary.Game?.GameId);
        var activeProjection = Read<object>(snapshot, "ActiveSession");
        Assert.Equal(active.Id, Read<HomeEditorialGame>(activeProjection, "Game").GameId);
        Assert.Equal(Now.AddMinutes(-10), Read<DateTimeOffset>(activeProjection, "StartedAtUtc"));
    }

    [Fact]
    public void First_valid_ordered_shortlist_entry_wins_without_active_session()
    {
        var first = Game("First");
        var second = Game("Second");

        var snapshot = Project(
            games: [first, second],
            shortlist: [Shortlist(first, 0), Shortlist(second, 1)]);

        Assert.Equal(HomeEditorialPrimarySourceKind.GamesDuMoment, snapshot.Primary.SourceKind);
        Assert.Equal(first.Id, snapshot.Primary.Game?.GameId);
    }

    [Fact]
    public void Invalid_first_shortlist_entry_falls_through_to_next_valid_entry()
    {
        var missing = Game("Missing");
        var valid = Game("Valid");

        var snapshot = Project(
            games: [missing, valid],
            shortlist: [Shortlist(missing, 0), Shortlist(valid, 1)],
            presentGameIds: [valid.Id]);

        Assert.Equal(HomeEditorialPrimarySourceKind.GamesDuMoment, snapshot.Primary.SourceKind);
        Assert.Equal(valid.Id, snapshot.Primary.Game?.GameId);
    }

    [Fact]
    public void Most_recent_completed_session_wins_without_active_or_valid_shortlist_candidate()
    {
        var absent = Game("Absent");
        var olderCompletion = Game("Older completion");
        var newerCompletion = Game("Newer completion");
        var olderSession = Session(
            olderCompletion,
            SessionState.Ended,
            Now.AddHours(-1));
        var newerSession = Session(
            newerCompletion,
            SessionState.Ended,
            Now.AddHours(-3)) with
        {
            ObservedEndedAtUtc = Now.AddMinutes(-10)
        };

        var snapshot = Project(
            games: [absent, olderCompletion, newerCompletion],
            shortlist: [Shortlist(absent, 0)],
            recentSessions: [olderSession, newerSession],
            presentGameIds: [olderCompletion.Id, newerCompletion.Id]);

        Assert.Equal(HomeEditorialPrimarySourceKind.RecentCompletedSession, snapshot.Primary.SourceKind);
        Assert.Equal(newerCompletion.Id, snapshot.Primary.Game?.GameId);
        Assert.Equal(Now.AddHours(-3), snapshot.Primary.SessionStartedAtUtc);
        Assert.Equal(Now.AddMinutes(-10), snapshot.Primary.SessionEndedAtUtc);
    }

    [Fact]
    public void Placeholder_is_selected_when_no_valid_candidate_exists()
    {
        var absent = Game("Absent");

        var snapshot = Project(
            games: [absent],
            shortlist: [Shortlist(absent, 0)],
            presentGameIds: []);

        Assert.Equal(HomeEditorialPrimarySourceKind.Placeholder, snapshot.Primary.SourceKind);
        Assert.Null(snapshot.Primary.Game);
        Assert.Null(snapshot.Primary.NavigationGameId);
    }

    [Fact]
    public void Remaining_shortlist_preserves_explicit_input_order()
    {
        var primary = Game("Primary");
        var second = Game("Second");
        var third = Game("Third");

        var snapshot = Project(
            games: [primary, second, third],
            shortlist: [Shortlist(primary, 0), Shortlist(second, 1), Shortlist(third, 2)]);

        Assert.Equal([primary.Id, second.Id, third.Id], snapshot.GamesDuMoment.Select(item => item.GameId));
        Assert.Equal([second.Id, third.Id], snapshot.RemainingGamesDuMoment.Select(item => item.GameId));
    }

    [Fact]
    public void Active_game_can_remain_editorial_primary_without_mutating_shortlist_order()
    {
        var active = Game("Active");
        var other = Game("Other");

        var snapshot = Project(
            games: [active, other],
            activeSessions: [Session(active, SessionState.Active, Now.AddMinutes(-5))],
            shortlist: [Shortlist(active, 0), Shortlist(other, 1)]);

        Assert.Equal([other.Id], snapshot.RemainingGamesDuMoment.Select(item => item.GameId));
        Assert.Equal(active.Id, Read<HomeEditorialGame>(Read<object>(snapshot, "ActiveSession"), "Game").GameId);
        Assert.Equal(active.Id, snapshot.Primary.Game?.GameId);
    }

    [Fact]
    public void Projection_does_not_mutate_persisted_shortlist_entries()
    {
        var first = Game("First");
        var second = Game("Second");
        GamesDuMomentEntry[] persisted = [Shortlist(first, 0), Shortlist(second, 1)];
        var before = persisted.ToArray();

        _ = Project(games: [first, second], shortlist: persisted);

        Assert.Equal(before, persisted);
    }

    [Fact]
    public void Active_sessions_are_excluded_from_recent_history()
    {
        var game = Game("Game");
        var active = Session(game, SessionState.Active, Now.AddMinutes(-5));

        var snapshot = Project(games: [game], recentSessions: [active]);

        Assert.Empty(snapshot.RecentHistory);
    }

    [Fact]
    public void Completed_session_semantics_and_order_are_preserved()
    {
        var first = Game("First");
        var second = Game("Second");
        var ended = Session(first, SessionState.Ended, Now.AddHours(-1));
        var recovered = Session(second, SessionState.Recovered, Now.AddHours(-2));

        var snapshot = Project(games: [first, second], recentSessions: [ended, recovered]);

        Assert.Equal([ended, recovered], snapshot.RecentHistory);
    }

    [Fact]
    public void Repeated_projection_is_deterministic()
    {
        var game = Game("Game");
        var input = Input(
            games: [game],
            shortlist: [Shortlist(game, 0)]);

        var first = HomeEditorialProjector.Project(input);
        var second = HomeEditorialProjector.Project(input);

        Assert.Equal(first.Primary.SourceKind, second.Primary.SourceKind);
        Assert.Equal(first.Primary.Game?.GameId, second.Primary.Game?.GameId);
        Assert.Equal(first.Primary.Game?.Title, second.Primary.Game?.Title);
        Assert.Equal(
            first.Primary.Game?.MediaIdentity?.ProviderGameId,
            second.Primary.Game?.MediaIdentity?.ProviderGameId);
        Assert.Equal(first.Primary.SessionStartedAtUtc, second.Primary.SessionStartedAtUtc);
        Assert.Equal(first.Primary.SessionEndedAtUtc, second.Primary.SessionEndedAtUtc);
        Assert.Equal(first.RemainingGamesDuMoment, second.RemainingGamesDuMoment);
        Assert.Equal(first.RecentHistory, second.RecentHistory);
        Assert.Equal(first.WeeklySummary, second.WeeklySummary);
        Assert.Equal(first.AttentionItems, second.AttentionItems);
    }

    [Fact]
    public void Projector_has_no_scoring_or_randomization_dependency()
    {
        var type = typeof(HomeEditorialProjector);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic));
    }

    private static HomeEditorialSnapshot Project(
        IReadOnlyList<LogicalGame> games,
        IReadOnlyList<GameSession>? activeSessions = null,
        IReadOnlyList<GamesDuMomentEntry>? shortlist = null,
        IReadOnlyList<GameSession>? recentSessions = null,
        IReadOnlyList<GameId>? presentGameIds = null)
    {
        return HomeEditorialProjector.Project(Input(
            games,
            activeSessions,
            shortlist,
            recentSessions,
            presentGameIds));
    }

    private static T Read<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property!.GetValue(instance));
    }

    private static HomeEditorialProjectionInput Input(
        IReadOnlyList<LogicalGame> games,
        IReadOnlyList<GameSession>? activeSessions = null,
        IReadOnlyList<GamesDuMomentEntry>? shortlist = null,
        IReadOnlyList<GameSession>? recentSessions = null,
        IReadOnlyList<GameId>? presentGameIds = null)
    {
        var present = presentGameIds ?? games.Select(game => game.Id).ToArray();
        var installations = present.Select((gameId, index) => new GameInstallation(
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
            activeSessions ?? [],
            shortlist ?? [],
            recentSessions ?? [],
            WeeklyActivitySummary.Empty,
            [],
            Now);
    }

    private static LogicalGame Game(string title) => new(
        GameId.New(),
        title,
        false,
        Now,
        Now);

    private static GamesDuMomentEntry Shortlist(LogicalGame game, int position) =>
        new(game.Id, position, Now.AddMinutes(position));

    private static GameSession Session(
        LogicalGame game,
        SessionState state,
        DateTimeOffset startedAtUtc)
    {
        DateTimeOffset? endedAtUtc = state == SessionState.Active
            ? null
            : startedAtUtc.AddMinutes(30);

        return new GameSession(
            Guid.NewGuid(),
            game.Id.Value,
            startedAtUtc,
            endedAtUtc ?? startedAtUtc.AddMinutes(5),
            endedAtUtc,
            state,
            state == SessionState.Active ? null : SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            startedAtUtc,
            endedAtUtc ?? startedAtUtc);
    }
}
