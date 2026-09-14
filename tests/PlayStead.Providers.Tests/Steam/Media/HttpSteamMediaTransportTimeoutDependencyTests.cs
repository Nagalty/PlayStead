using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportTimeoutDependencyTests
{
    [Fact]
    public void Transport_has_testable_timeout_constructor()
    {
        var constructor = typeof(HttpSteamMediaTransport).GetConstructor(
            [
                typeof(HttpClient),
                typeof(TimeSpan)
            ]);

        Assert.NotNull(constructor);
    }
}
