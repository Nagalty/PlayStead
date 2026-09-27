using PlayStead.Core.Product;
using PlayStead.Core.Updates;

namespace PlayStead.UI.About;

public sealed class AboutViewModel
{
    public AboutViewModel(IDistributionChannelProvider channelProvider)
    {
        ArgumentNullException.ThrowIfNull(channelProvider);
        Channel = channelProvider.Current;
    }

    public string ProductName => "PlayStead";

    public string Version => ProductVersion.Current;

    public DistributionChannel Channel { get; }

    public string ChannelDisplay =>
        Channel switch
        {
            DistributionChannel.MicrosoftStore => "Microsoft Store",
            _ => "GitHub"
        };
}
