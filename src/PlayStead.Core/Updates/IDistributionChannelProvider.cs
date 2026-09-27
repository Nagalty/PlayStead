namespace PlayStead.Core.Updates;

public interface IDistributionChannelProvider
{
    DistributionChannel Current { get; }
}
