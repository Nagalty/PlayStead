using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Library;

public sealed class GameIdTests
{
    [Fact]
    public void New_creates_a_non_empty_identifier()
    {
        var id = GameId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ToString_uses_canonical_D_format()
    {
        var value = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var id = new GameId(value);

        Assert.Equal("11111111-2222-3333-4444-555555555555", id.ToString());
    }
}
