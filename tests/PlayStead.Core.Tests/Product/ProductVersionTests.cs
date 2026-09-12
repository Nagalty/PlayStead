using PlayStead.Core.Product;

namespace PlayStead.Core.Tests.Product;

public sealed class ProductVersionTests
{
    [Fact]
    public void Current_matches_0_3_development_version()
    {
        Assert.Equal("0.3.0-dev", ProductVersion.Current);
    }
}
