using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderTests
{
    [Fact]
    public void CanResolve_returns_true_for_valid_Steam_identity()
    {
        var provider = CreateProvider();

        var canResolve = provider.CanResolve(
            new GameMediaIdentity(
                ProviderKind.Steam,
                "1874880",
                "Arma Reforger"));

        Assert.True(canResolve);
    }

    [Theory]
    [InlineData(ProviderKind.Epic)]
    [InlineData(ProviderKind.Gog)]
    [InlineData(ProviderKind.Manual)]
    public void CanResolve_returns_false_for_non_Steam_identity(
        ProviderKind providerKind)
    {
        var provider = CreateProvider();

        var canResolve = provider.CanResolve(
            new GameMediaIdentity(
                providerKind,
                "1874880",
                "Arma Reforger"));

        Assert.False(canResolve);
    }

    [Fact]
    public void CanResolve_does_not_depend_on_title_matching()
    {
        var provider = CreateProvider();

        var canResolve = provider.CanResolve(
            new GameMediaIdentity(
                ProviderKind.Steam,
                "1874880",
                "A completely unrelated title"));

        Assert.True(canResolve);
    }


    [Fact]
    public async Task ResolveAsync_returns_null_for_non_Steam_without_transport_call()
    {
        var transport = new NullTransport();

        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator(Array.Empty<string?>()),
            new SteamLocalMediaLocator(),
            transport);

        var identity = new GameMediaIdentity(
            ProviderKind.Epic,
            "epic-game-id",
            "Arma Reforger");

        Assert.False(provider.CanResolve(identity));

        var payload = await provider.ResolveAsync(
            identity,
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Null(payload);
        Assert.Equal(0, transport.CallCount);
    }

    private static SteamMediaProvider CreateProvider() =>
        new(
            new WindowsSteamRootLocator(Array.Empty<string?>()),
            new SteamLocalMediaLocator(),
            new NullTransport());

    private sealed class NullTransport : ISteamMediaTransport
    {
        public int CallCount { get; private set; }

        public Task<GameMediaPayload?> TryDownloadAsync(
            string appId,
            GameMediaAssetType assetType,
            IReadOnlyList<Uri> candidates,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return Task.FromResult<GameMediaPayload?>(null);
        }
    }
}
