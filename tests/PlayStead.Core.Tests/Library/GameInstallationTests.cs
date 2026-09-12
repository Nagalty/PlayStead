using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Library;

public sealed class GameInstallationTests
{
    [Fact]
    public void Constructor_preserves_provider_identity_state_and_last_seen()
    {
        var installationId = new InstallationId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var gameId = new GameId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var lastSeen = new DateTimeOffset(2026, 9, 12, 8, 30, 0, TimeSpan.Zero);

        var installation = new GameInstallation(
            installationId,
            gameId,
            ProviderKind.Steam,
            "1874880",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            42_000_000_000,
            IsPreferred: true,
            IsPresent: true,
            LastSeenUtc: lastSeen);

        Assert.Equal(installationId, installation.Id);
        Assert.Equal(gameId, installation.GameId);
        Assert.Equal(ProviderKind.Steam, installation.Provider);
        Assert.Equal("1874880", installation.ExternalId);
        Assert.Equal(@"G:\SteamLibrary\steamapps\common\Arma Reforger", installation.InstallPath);
        Assert.Equal(42_000_000_000, installation.InstalledSizeBytes);
        Assert.True(installation.IsPreferred);
        Assert.True(installation.IsPresent);
        Assert.Equal(lastSeen, installation.LastSeenUtc);
    }
}
