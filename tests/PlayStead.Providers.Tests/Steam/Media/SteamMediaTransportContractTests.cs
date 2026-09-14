using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaTransportContractTests
{
    [Fact]
    public void Transport_exposes_authoritative_Task3_download_contract()
    {
        var method = typeof(ISteamMediaTransport).GetMethod(
            "TryDownloadAsync",
            [
                typeof(string),
                typeof(GameMediaAssetType),
                typeof(IReadOnlyList<Uri>),
                typeof(CancellationToken)
            ]);

        Assert.NotNull(method);
        Assert.Equal(
            typeof(Task<GameMediaPayload?>),
            method.ReturnType);
    }
}
