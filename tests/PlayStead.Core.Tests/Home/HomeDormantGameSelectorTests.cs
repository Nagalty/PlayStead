using PlayStead.Core.Home;
using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Home;

public sealed class HomeDormantGameSelectorTests
{
    [Fact]
    public void Empty_candidates_return_null()
    {
        Assert.Null(HomeDormantGameSelector.Select([], null, new Random(1)));
    }

    [Fact]
    public void One_candidate_is_selected()
    {
        var candidate = Candidate("one");

        Assert.Same(candidate, HomeDormantGameSelector.Select([candidate], Guid.NewGuid(), new Random(1)));
    }

    [Fact]
    public void Multiple_candidates_exclude_previous_when_possible()
    {
        var previous = Candidate("previous", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var next = Candidate("next", Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var selected = HomeDormantGameSelector.Select([previous, next], previous.Game.GameId.Value, new Random(1));

        Assert.Same(next, selected);
    }

    [Fact]
    public void Previous_candidate_is_reused_when_it_is_the_only_eligible_game()
    {
        var previous = Candidate("previous");

        Assert.Same(previous, HomeDormantGameSelector.Select([previous], previous.Game.GameId.Value, new Random(1)));
    }

    [Fact]
    public void Previous_candidate_is_ignored_when_it_is_no_longer_eligible()
    {
        var current = Candidate("current");
        var selected = HomeDormantGameSelector.Select([current], Guid.NewGuid(), new Random(1));

        Assert.Same(current, selected);
    }

    [Fact]
    public void Selection_is_limited_to_the_eligible_candidates()
    {
        var candidates = new[] { Candidate("one"), Candidate("two"), Candidate("three") };
        var selected = HomeDormantGameSelector.Select(candidates, null, new Random(2));

        Assert.NotNull(selected);
        Assert.Contains(selected!, candidates);
    }

    private static HomeEditorialDormantGame Candidate(string title, Guid? id = null)
    {
        var gameId = new GameId(id ?? Guid.NewGuid());
        var game = new HomeEditorialGame(gameId, title, null);
        return new HomeEditorialDormantGame(
            game,
            DateTimeOffset.UtcNow.AddDays(-60),
            60);
    }
}
