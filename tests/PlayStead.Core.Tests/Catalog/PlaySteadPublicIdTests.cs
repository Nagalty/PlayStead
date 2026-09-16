using PlayStead.Core.Catalog;

namespace PlayStead.Core.Tests.Catalog;

public sealed class PlaySteadPublicIdTests
{
    [Theory]
    [InlineData("PlayStead-000001")]
    [InlineData("PlayStead-123456")]
    [InlineData("PlayStead-123456789")]
    [InlineData("PlayStead-DLC-000001")]
    [InlineData("PlayStead-DLC-987654")]
    public void Parse_accepts_valid_ids(string value)
    {
        var id = PlaySteadPublicId.Parse(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(value, id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("PlayStead-1")]
    [InlineData("PlayStead-12345")]
    [InlineData("playstead-123456")]
    [InlineData("PlayStead-ABCDEF")]
    [InlineData("Steam-123456")]
    [InlineData("PlayStead-DLC-12345")]
    public void Parse_rejects_invalid_ids(string value)
    {
        Assert.Throws<FormatException>(
            () => PlaySteadPublicId.Parse(value));
    }
}
