using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class GameLaunchViewModelTests
{
    [Fact]
    public void Constructor_exposes_only_launchable_options_and_prefers_preferred_installation()
    {
        var gameId =
            GameId.New();

        var otherSteam =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "111",
                isPreferred: false,
                isPresent: true);

        var preferredSteam =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "222",
                isPreferred: true,
                isPresent: true);

        var manualPreferred =
            CreateInstallation(
                gameId,
                ProviderKind.Manual,
                "manual",
                isPreferred: true,
                isPresent: true);

        var absentSteam =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "333",
                isPreferred: true,
                isPresent: false);

        var otherGame =
            CreateInstallation(
                GameId.New(),
                ProviderKind.Steam,
                "444",
                isPreferred: true,
                isPresent: true);

        var viewModel =
            new GameLaunchViewModel(
                gameId,
                [
                    manualPreferred,
                    absentSteam,
                    otherGame,
                    otherSteam,
                    preferredSteam
                ],
                CreateService());

        Assert.True(
            viewModel.CanPlay);

        Assert.True(
            viewModel.HasMultipleLaunchOptions);

        Assert.Equal(
            2,
            viewModel.LaunchOptions.Count);

        Assert.Same(
            preferredSteam,
            viewModel.DefaultInstallation);
    }

    [Fact]
    public void Constructor_reports_unavailable_when_no_supported_installation_exists()
    {
        var gameId =
            GameId.New();

        var viewModel =
            new GameLaunchViewModel(
                gameId,
                [
                    CreateInstallation(
                        gameId,
                        ProviderKind.Manual,
                        "manual",
                        isPreferred: true,
                        isPresent: true)
                ],
                CreateService());

        Assert.False(
            viewModel.CanPlay);

        Assert.False(
            viewModel.HasMultipleLaunchOptions);

        Assert.Empty(
            viewModel.LaunchOptions);

        Assert.Null(
            viewModel.DefaultInstallation);
    }

    [Fact]
    public void TryPlayDefault_launches_preferred_Steam_installation()
    {
        var gameId =
            GameId.New();

        var opener =
            new RecordingUriLauncher();

        var viewModel =
            new GameLaunchViewModel(
                gameId,
                [
                    CreateInstallation(
                        gameId,
                        ProviderKind.Steam,
                        "111",
                        isPreferred: false,
                        isPresent: true),

                    CreateInstallation(
                        gameId,
                        ProviderKind.Steam,
                        "222",
                        isPreferred: true,
                        isPresent: true)
                ],
                new GameLaunchService(
                    opener));

        var launched =
            viewModel.TryPlayDefault();

        Assert.True(
            launched);

        Assert.Single(
            opener.OpenedUris);

        Assert.Equal(
            "steam://rungameid/222",
            opener.OpenedUris[0].AbsoluteUri);
    }

    [Fact]
    public void Active_session_for_same_game_disables_play_and_blocks_launch()
    {
        var gameId = GameId.New();
        var opener = new RecordingUriLauncher();
        var viewModel = new GameLaunchViewModel(gameId,
            [CreateInstallation(gameId, ProviderKind.Steam, "111", true, true)],
            new GameLaunchService(opener));

        viewModel.SetSessionActive(true);

        Assert.False(viewModel.PlayCommand.CanExecute(null));
        Assert.False(viewModel.CanPlay);
        Assert.False(viewModel.TryPlayDefault());
        Assert.Empty(opener.OpenedUris);
    }

    [Fact]
    public void Active_session_for_another_game_does_not_disable_selected_game()
    {
        var gameId = GameId.New();
        var otherGameId = GameId.New();
        var viewModel = new GameLaunchViewModel(gameId,
            [CreateInstallation(gameId, ProviderKind.Steam, "111", true, true)],
            CreateService());
        var otherGame = new GameLaunchViewModel(otherGameId,
            [CreateInstallation(otherGameId, ProviderKind.Steam, "222", true, true)],
            CreateService());

        otherGame.SetSessionActive(true);

        Assert.True(viewModel.CanPlay);
        Assert.False(otherGame.CanPlay);
    }

    [Fact]
    public void Closing_active_session_reenables_play_and_notifies_binding()
    {
        var gameId = GameId.New();
        var viewModel = new GameLaunchViewModel(gameId,
            [CreateInstallation(gameId, ProviderKind.Steam, "111", true, true)],
            CreateService());
        var changed = new List<string?>();
        var canExecuteChanged = 0;
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        viewModel.PlayCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        viewModel.SetSessionActive(true);
        viewModel.SetSessionActive(false);

        Assert.True(viewModel.CanPlay);
        Assert.True(viewModel.PlayCommand.CanExecute(null));
        Assert.Contains(nameof(GameLaunchViewModel.CanPlay), changed);
        Assert.Equal(2, canExecuteChanged);
    }

    private static GameLaunchService CreateService()
    {
        return new GameLaunchService(
            new RecordingUriLauncher());
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
                13,
                45,
                0,
                TimeSpan.Zero));
    }

    private sealed class RecordingUriLauncher :
        IExternalUriLauncher
    {
        public List<Uri> OpenedUris { get; } =
            [];

        public void Open(
            Uri uri)
        {
            OpenedUris.Add(
                uri);
        }
    }
}
