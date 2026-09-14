using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportDependencyTests
{
    [Fact]
    public void Transport_has_HttpClient_constructor()
    {
        var constructor = typeof(HttpSteamMediaTransport).GetConstructor(
            [typeof(HttpClient)]);

        Assert.NotNull(constructor);
    }
}
