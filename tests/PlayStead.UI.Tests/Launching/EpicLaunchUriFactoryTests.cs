using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class EpicLaunchUriFactoryTests
{
    [Fact]
    public void Creates_exact_hll_launcher_uri()
    {
        var installation = Installation(
            ProviderKind.Epic,
            "581c8d4fd9574884bff66cbdbaa42def",
            "6430e58041234e41b8f81f68f01450ed",
            "3e02273b543f4ff0a1c24d3b534a9ac3");

        var uri = EpicLaunchUriFactory.CreateOrNull(installation);

        Assert.Equal(
            "com.epicgames.launcher://apps/6430e58041234e41b8f81f68f01450ed%3A581c8d4fd9574884bff66cbdbaa42def%3A3e02273b543f4ff0a1c24d3b534a9ac3?action=launch&silent=true",
            uri?.AbsoluteUri);
    }

    [Fact]
    public void Encodes_identifier_components_and_rejects_incomplete_or_other_providers()
    {
        var encoded = EpicLaunchUriFactory.CreateOrNull(Installation(ProviderKind.Epic, "catalog/id", "namespace value", "app?name"));
        Assert.Contains("namespace%20value%3Acatalog%2Fid%3Aapp%3Fname", encoded?.AbsoluteUri);
        Assert.Null(EpicLaunchUriFactory.CreateOrNull(Installation(ProviderKind.Epic, "", "namespace", "app")));
        Assert.Null(EpicLaunchUriFactory.CreateOrNull(Installation(ProviderKind.Epic, "catalog", "", "app")));
        Assert.Null(EpicLaunchUriFactory.CreateOrNull(Installation(ProviderKind.Epic, "catalog", "namespace", "")));
        Assert.Null(EpicLaunchUriFactory.CreateOrNull(Installation(ProviderKind.Steam, "123", "namespace", "app")));
        Assert.Null(EpicLaunchUriFactory.CreateOrNull(null!));
    }

    private static GameInstallation Installation(ProviderKind provider, string id, string ns, string app) =>
        new(InstallationId.New(), GameId.New(), provider, id, @"G:\HellLetLooseG0WU4", 1, true, true, DateTimeOffset.UtcNow,
            LaunchMetadata: new ProviderLaunchMetadata(provider, new Dictionary<string, string>
            {
                ["CatalogNamespace"] = ns,
                ["CatalogItemId"] = id,
                ["AppName"] = app
            }));
}
