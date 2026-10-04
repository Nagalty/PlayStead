using PlayStead.CatalogBuilder;
using PlayStead.Core.Catalog;

namespace PlayStead.CatalogBuilder.Tests;

public sealed class EpicProviderAliasAndMediaContractTests
{
    [Fact]
    public void Hll_catalog_item_id_is_added_to_the_existing_epic_alias_set()
    {
        var refs = EpicProviderAliasRegistry.AddProvenAliases([
            new(CatalogProviderKind.Epic, "1ff858e1c36b45c9a54ca66c4279dbb1", "igdb-external-game", CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)
        ]);

        Assert.Contains(refs, x => x.Provider == CatalogProviderKind.Epic && x.ExternalId == "581c8d4fd9574884bff66cbdbaa42def" && x.ExternalType == "catalog-item");
        Assert.Contains(refs, x => x.Provider == CatalogProviderKind.Epic && x.ExternalId == "6430e58041234e41b8f81f68f01450ed" && x.ExternalType == "namespace");
        Assert.Contains(refs, x => x.Provider == CatalogProviderKind.Epic && x.ExternalId == "3e02273b543f4ff0a1c24d3b534a9ac3" && x.ExternalType == "app-name");
    }

    [Fact]
    public void Epic_aliases_are_deterministic_and_do_not_duplicate_existing_refs()
    {
        var input = new CanonicalCatalogProviderReference(CatalogProviderKind.Epic, "1ff858e1c36b45c9a54ca66c4279dbb1", null, CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch);
        var refs = EpicProviderAliasRegistry.AddProvenAliases([input]);

        Assert.Equal(4, refs.Count);
        Assert.Equal(4, refs.Select(x => (x.Provider, x.ExternalId)).Distinct().Count());
    }

    [Theory]
    [InlineData("coc4gc", "https://images.igdb.com/igdb/image/upload/t_cover_big/coc4gc.jpg")]
    [InlineData("", null)]
    public void Cover_urls_use_a_supported_igdb_transformation(string imageId, string? expected)
    {
        Assert.Equal(expected, IgdbMediaUrlFactory.Cover(imageId));
    }

    [Theory]
    [InlineData("ar13k7", "https://images.igdb.com/igdb/image/upload/t_1080p/ar13k7.jpg")]
    [InlineData("", null)]
    public void Hero_urls_use_a_supported_igdb_transformation(string imageId, string? expected)
    {
        Assert.Equal(expected, IgdbMediaUrlFactory.Hero(imageId));
    }
}
