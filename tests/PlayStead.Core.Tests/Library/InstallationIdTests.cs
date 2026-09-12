using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Library;

public sealed class InstallationIdTests
{
    [Fact]
    public void New_creates_a_non_empty_identifier()
    {
        var id = InstallationId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ToString_uses_canonical_D_format()
    {
        var value = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var id = new InstallationId(value);

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", id.ToString());
    }
}
