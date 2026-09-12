using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Library;

public sealed class LogicalGameTests
{
    [Fact]
    public void Constructor_preserves_identity_title_visibility_and_timestamps()
    {
        var id = new GameId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var created = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        var updated = created.AddMinutes(5);

        var game = new LogicalGame(
            id,
            "Arma Reforger",
            IsHidden: false,
            CreatedAtUtc: created,
            UpdatedAtUtc: updated);

        Assert.Equal(id, game.Id);
        Assert.Equal("Arma Reforger", game.Title);
        Assert.False(game.IsHidden);
        Assert.Equal(created, game.CreatedAtUtc);
        Assert.Equal(updated, game.UpdatedAtUtc);
    }
}
