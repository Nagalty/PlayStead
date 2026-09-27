using PlayStead.Core.Updates;
using PlayStead.UI.About;
using Xunit;

namespace PlayStead.UI.Tests.About;

public sealed class AboutViewModelTests
{
    [Fact]
    public void Exposes_runtime_product_and_channel()
    {
        var viewModel = new AboutViewModel(new FakeChannelProvider(DistributionChannel.GitHub));

        Assert.Equal("PlayStead", viewModel.ProductName);
        Assert.Equal(PlayStead.Core.Product.ProductVersion.Current, viewModel.Version);
        Assert.Equal(DistributionChannel.GitHub, viewModel.Channel);
        Assert.Equal("GitHub", viewModel.ChannelDisplay);
    }

    [Fact]
    public void Formats_store_channel_without_hardcoding_the_channel_source()
    {
        var viewModel = new AboutViewModel(new FakeChannelProvider(DistributionChannel.MicrosoftStore));

        Assert.Equal("Microsoft Store", viewModel.ChannelDisplay);
    }

    private sealed class FakeChannelProvider(DistributionChannel channel) : IDistributionChannelProvider
    {
        public DistributionChannel Current => channel;
    }
}
