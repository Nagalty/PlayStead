using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Library;

public sealed class LibrarySnapshotTests
{
    [Fact]
    public void Constructor_exposes_games_and_installations_together()
    {
        var now = new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);
        var gameId = new GameId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var game = new LogicalGame(gameId, "Arma Reforger", false, now, now);
        var installation = new GameInstallation(
            new InstallationId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            gameId,
            ProviderKind.Steam,
            "1874880",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            42_000_000_000,
            true,
            true,
            now);

        var snapshot = new LibrarySnapshot([game], [installation]);

        Assert.Single(snapshot.Games);
        Assert.Single(snapshot.Installations);
        Assert.Same(game, snapshot.Games[0]);
        Assert.Same(installation, snapshot.Installations[0]);
    }
}
