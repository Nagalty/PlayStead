using PlayStead.CatalogBuilder;
using PlayStead.Core.Catalog;

namespace PlayStead.CatalogBuilder.Tests;

public sealed class CanonicalProviderMappingRegistryTests
{
    [Fact]
    public void Adds_verified_Crysis_GOG_reference_from_Steam_AppId()
    {
        var refs = CanonicalProviderMappingRegistry.AddVerifiedMappings(
        [
            new(CatalogProviderKind.Steam, "1715130", null, CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)
        ]);

        var mapped = Assert.Single(refs, x => x.Provider == CatalogProviderKind.Gog);
        Assert.Equal("1103900211", mapped.ExternalId);
        Assert.Equal(CatalogConfidence.Deterministic, mapped.Confidence);
    }

    [Fact]
    public void Does_not_infer_mapping_from_title_or_unrecognized_reference()
    {
        var refs = CanonicalProviderMappingRegistry.AddVerifiedMappings(
        [
            new(CatalogProviderKind.Steam, "unrecognized", null, CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)
        ]);

        Assert.Single(refs);
    }

    [Fact]
    public void Adds_verified_Hell_Let_Loose_Steam_reference_from_Epic_catalog_item()
    {
        var refs = CanonicalProviderMappingRegistry.AddVerifiedMappings(
        [
            new(CatalogProviderKind.Epic, "581c8d4fd9574884bff66cbdbaa42def", "catalog-item", CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)
        ]);

        var mapped = Assert.Single(refs, x => x.Provider == CatalogProviderKind.Steam);
        Assert.Equal("686810", mapped.ExternalId);
        Assert.Equal(CatalogConfidence.Deterministic, mapped.Confidence);
    }
}
