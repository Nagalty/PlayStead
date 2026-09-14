using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamLocalMediaLocatorTests
{
    [Fact]
    public void Locator_exposes_Task3_cover_lookup_contract()
    {
        var method = typeof(SteamLocalMediaLocator).GetMethod(
            "TryLocate",
            [
                typeof(string),
                typeof(string),
                typeof(GameMediaAssetType)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }
}
