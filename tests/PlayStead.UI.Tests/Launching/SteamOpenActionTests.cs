using System.Windows;
using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class SteamOpenActionTests
{
    [Fact]
    public void Steam_installation_exposes_open_action_and_uses_app_id()
    {
        var launcher = new RecordingLauncher();
        var service = new GameLaunchService(launcher);
        var gameId = GameId.New();
        var installation = new GameInstallation(InstallationId.New(), gameId, ProviderKind.Steam, "1874880", "C:\\Steam", 1, true, true, DateTimeOffset.UtcNow);
        var sut = new GameLaunchViewModel(gameId, new[] { installation }, service);

        Assert.True(sut.CanOpenSteam);
        Assert.True(sut.TryOpenSteam());
        Assert.Equal("steam://openurl/https://store.steampowered.com/app/1874880", launcher.Last?.ToString());
    }

    [Fact]
    public void Non_steam_or_invalid_app_id_hides_open_action()
    {
        var service = new GameLaunchService(new RecordingLauncher());
        var gameId = GameId.New();
        var installation = new GameInstallation(InstallationId.New(), gameId, ProviderKind.Epic, "abc", "C:\\Games", 1, false, true, DateTimeOffset.UtcNow);
        var sut = new GameLaunchViewModel(gameId, new[] { installation }, service);

        Assert.False(sut.CanOpenSteam);
        Assert.False(sut.TryOpenSteam());
    }

    private sealed class RecordingLauncher : IExternalUriLauncher
    {
        public Uri? Last { get; private set; }
        public void Open(Uri uri) => Last = uri;
    }
}
