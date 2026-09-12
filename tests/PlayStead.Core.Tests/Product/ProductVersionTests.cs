using PlayStead.Core.Product;

namespace PlayStead.Core.Tests.Product;

public sealed class ProductVersionTests
{
    [Fact]
    public void Current_is_a_development_0_x_version()
    {
        Assert.Equal("0.1.0-dev", ProductVersion.Current);
        Assert.StartsWith("0.", ProductVersion.Current, StringComparison.Ordinal);
    }
}
