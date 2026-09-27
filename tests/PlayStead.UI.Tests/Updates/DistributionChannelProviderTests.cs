using PlayStead.Core.Updates;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Updates;

public sealed class DistributionChannelProviderTests
{
    [Fact]
    public void Current_channel_comes_from_build_metadata_and_defaults_to_github()
    {
        var provider = new BuildDistributionChannelProvider(typeof(BuildDistributionChannelProvider).Assembly);

        Assert.Equal(DistributionChannel.GitHub, provider.Current);
    }
}
