using PlayStead.Core.Product;

namespace PlayStead.Core.Tests.Product;

public sealed class ProductVersionTests
{
    [Fact]
    public void Current_matches_0_4_1_development_version()
    {
        Assert.Equal("0.4.1-dev", ProductVersion.Current);
    }
}
