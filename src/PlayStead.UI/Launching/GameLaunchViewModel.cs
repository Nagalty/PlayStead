using PlayStead.Core.Library;

namespace PlayStead.UI.Launching;

public sealed class GameLaunchViewModel
{
    private readonly GameLaunchService _launchService;

    public GameLaunchViewModel(
        GameId gameId,
        IEnumerable<GameInstallation> installations,
        GameLaunchService launchService)
    {
        ArgumentNullException.ThrowIfNull(installations);
        ArgumentNullException.ThrowIfNull(launchService);

        _launchService = launchService;

        LaunchOptions =
            installations
                .Where(
                    installation =>
                        installation.GameId == gameId &&
                        _launchService.CanLaunch(installation))
                .ToArray();

        DefaultInstallation =
            GameLaunchInstallationSelector.SelectDefault(
                gameId,
                LaunchOptions);
    }

    public IReadOnlyList<GameInstallation> LaunchOptions { get; }

    public GameInstallation? DefaultInstallation { get; }

    public bool CanPlay => DefaultInstallation is not null;

    public bool HasMultipleLaunchOptions => LaunchOptions.Count > 1;

    public bool TryPlayDefault()
    {
        return DefaultInstallation is not null &&
            _launchService.TryLaunch(DefaultInstallation);
    }

    public bool TryPlay(GameInstallation installation)
    {
        return LaunchOptions.Contains(installation) &&
            _launchService.TryLaunch(installation);
    }
}
