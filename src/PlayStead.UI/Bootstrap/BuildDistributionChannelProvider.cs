using System.Reflection;
using PlayStead.Core.Updates;

namespace PlayStead.UI.Bootstrap;

public sealed class BuildDistributionChannelProvider : IDistributionChannelProvider
{
    public BuildDistributionChannelProvider(Assembly assembly)
    {
        Current = Read(assembly);
    }

    public DistributionChannel Current { get; }

    private static DistributionChannel Read(Assembly assembly)
    {
        var value = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "PlaySteadDistributionChannel")
            ?.Value;

        return Enum.TryParse<DistributionChannel>(value, ignoreCase: true, out var channel)
            ? channel
            : DistributionChannel.GitHub;
    }
}
