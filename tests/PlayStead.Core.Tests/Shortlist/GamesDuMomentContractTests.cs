using PlayStead.Core.Library;
using PlayStead.Core.Shortlist;

namespace PlayStead.Core.Tests.Shortlist;

public sealed class GamesDuMomentContractTests
{
    [Fact]
    public void Entry_contains_only_identity_order_and_utc_timestamp()
    {
        var id = new GameId(Guid.NewGuid());
        var added = DateTimeOffset.UtcNow;
        var entry = new GamesDuMomentEntry(id, 0, added);
        Assert.Equal(id, entry.GameId);
        Assert.Equal(0, entry.Position);
        Assert.Equal(added, entry.AddedAtUtc);
    }
}
