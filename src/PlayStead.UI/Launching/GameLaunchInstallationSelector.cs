using PlayStead.Core.Library;

namespace PlayStead.UI.Launching;

public static class GameLaunchInstallationSelector
{
    public static GameInstallation? SelectDefault(
        GameId gameId,
        IEnumerable<GameInstallation> installations)
    {
        ArgumentNullException.ThrowIfNull(
            installations);

        var candidates =
            installations
                .Where(
                    installation =>
                        installation.GameId == gameId &&
                        installation.IsPresent)
                .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        return candidates
                   .FirstOrDefault(
                       installation =>
                           installation.IsPreferred)
               ?? candidates[0];
    }
}
