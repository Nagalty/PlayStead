using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;
using PlayStead.Core.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaArchitectureTests
{
    [Theory]
    [InlineData("PlayStead.Providers.Steam.Media.SteamLocalMediaLocator")]
    [InlineData("PlayStead.Providers.Steam.Media.ISteamMediaTransport")]
    [InlineData("PlayStead.Providers.Steam.Media.HttpSteamMediaTransport")]
    public void Task3_required_media_component_exists(string fullTypeName)
    {
        var type = Type.GetType(
            $"{fullTypeName}, PlayStead.Providers",
            throwOnError: false);

        Assert.NotNull(type);
    }

    [Fact]
    public void SteamMediaProvider_has_authoritative_Task3_constructor()
    {
        var localLocatorType = Type.GetType(
            "PlayStead.Providers.Steam.Media.SteamLocalMediaLocator, PlayStead.Providers",
            throwOnError: false);

        var transportType = Type.GetType(
            "PlayStead.Providers.Steam.Media.ISteamMediaTransport, PlayStead.Providers",
            throwOnError: false);

        Assert.NotNull(localLocatorType);
        Assert.NotNull(transportType);

        var constructor = typeof(SteamMediaProvider).GetConstructor(
            [
                typeof(WindowsSteamRootLocator),
                localLocatorType,
                transportType,
                typeof(IMediaDiagnostics)
            ]);

        Assert.NotNull(constructor);
        Assert.True(constructor!.GetParameters()[3].IsOptional);
    }
}
