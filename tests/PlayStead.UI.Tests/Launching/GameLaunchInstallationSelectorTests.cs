using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class GameLaunchInstallationSelectorTests
{
    [Fact]
    public void SelectDefault_returns_the_only_present_installation()
    {
        var gameId =
            GameId.New();

        var installation =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "1874880",
                isPreferred: false,
                isPresent: true);

        var selected =
            GameLaunchInstallationSelector.SelectDefault(
                gameId,
                [installation]);

        Assert.Same(
            installation,
            selected);
    }

    [Fact]
    public void SelectDefault_prefers_the_preferred_present_installation()
    {
        var gameId =
            GameId.New();

        var other =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "111",
                isPreferred: false,
                isPresent: true);

        var preferred =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "222",
                isPreferred: true,
                isPresent: true);

        var selected =
            GameLaunchInstallationSelector.SelectDefault(
                gameId,
                [other, preferred]);

        Assert.Same(
            preferred,
            selected);
    }

    [Fact]
    public void SelectDefault_ignores_absent_and_other_game_installations()
    {
        var gameId =
            GameId.New();

        var otherGame =
            CreateInstallation(
                GameId.New(),
                ProviderKind.Steam,
                "333",
                isPreferred: true,
                isPresent: true);

        var absent =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "444",
                isPreferred: true,
                isPresent: false);

        var selected =
            GameLaunchInstallationSelector.SelectDefault(
                gameId,
                [otherGame, absent]);

        Assert.Null(
            selected);
    }

    private static GameInstallation CreateInstallation(
        GameId gameId,
        ProviderKind provider,
        string externalId,
        bool isPreferred,
        bool isPresent)
    {
        return new GameInstallation(
            InstallationId.New(),
            gameId,
            provider,
            externalId,
            $@"D:\Games\{externalId}",
            1_000_000_000,
            isPreferred,
            isPresent,
            new DateTimeOffset(
                2026,
                9,
                14,
                12,
                45,
                0,
                TimeSpan.Zero));
    }
}
