using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Library;

public sealed class ManualInstallRootHeuristicsTests
{
    [Theory]
    [InlineData(@"H:\007 First Light\Retail\007FirstLight.exe", @"H:\007 First Light")]
    [InlineData(@"C:\Game\Binaries\Win64\Game.exe", @"C:\Game")]
    [InlineData(@"C:\Game\Binaries\Win64\Shipping\Game.exe", @"C:\Game")]
    [InlineData(@"C:\Game\Game.exe", @"C:\Game")]
    public void Suggests_only_known_bounded_layouts(string executable, string expected)
    {
        Assert.Equal(expected, ManualInstallRootHeuristics.Suggest(executable));
    }

    [Fact]
    public void Resolve_repairs_legacy_retail_root_equal_to_working_directory()
    {
        var executable = @"H:\007 First Light\Retail\007FirstLight.exe";

        Assert.Equal(
            @"H:\007 First Light",
            ManualInstallRootHeuristics.Resolve(
                executable,
                @"H:\007 First Light\Retail",
                @"H:\007 First Light\Retail"));
    }
}
