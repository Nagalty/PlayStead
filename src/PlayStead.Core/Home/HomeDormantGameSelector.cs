namespace PlayStead.Core.Home;

public static class HomeDormantGameSelector
{
    public static HomeEditorialDormantGame? Select(
        IReadOnlyList<HomeEditorialDormantGame> candidates,
        Guid? previouslyDisplayedGameId,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(random);

        if (candidates.Count == 0)
        {
            return null;
        }

        var selectable = candidates.Count > 1 && previouslyDisplayedGameId is Guid previous
            ? candidates.Where(candidate => candidate.Game.GameId.Value != previous).ToArray()
            : candidates;

        return selectable[random.Next(selectable.Count)];
    }
}
