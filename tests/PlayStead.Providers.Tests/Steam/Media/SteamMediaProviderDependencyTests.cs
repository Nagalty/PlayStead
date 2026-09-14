using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderDependencyTests
{
    [Fact]
    public void Provider_exposes_only_authoritative_constructor()
    {
        var constructors = typeof(SteamMediaProvider).GetConstructors();

        var constructor = Assert.Single(constructors);
        var parameterTypes = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(
            [
                typeof(WindowsSteamRootLocator),
                typeof(SteamLocalMediaLocator),
                typeof(ISteamMediaTransport)
            ],
            parameterTypes);
    }
}
