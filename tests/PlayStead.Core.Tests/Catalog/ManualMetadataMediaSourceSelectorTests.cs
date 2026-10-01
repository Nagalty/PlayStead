using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Catalog;

public sealed class ManualMetadataMediaSourceSelectorTests
{
    [Fact]
    public void Canonical_normalization_matches_catalog_builder_value()
    {
        Assert.Equal("007 FIRST LIGHT", CanonicalCatalogTitleNormalizer.Normalize("007 First Light"));
    }

    [Theory]
    [InlineData(new[] { "Steam:3768760" }, "3768760")]
    [InlineData(new[] { "Steam:3768760", "Epic:c04cf17392964f2594620101490bdb21" }, "3768760")]
    [InlineData(new[] { "Steam:3768760", "Gog:123" }, "3768760")]
    public void Unique_deterministic_steam_reference_is_selected(string[] values, string expected)
    {
        var refs = values.Select(Parse).ToArray();
        var result = ManualMetadataMediaSourceSelector.Select(refs);
        Assert.NotNull(result);
        Assert.Equal(ProviderKind.Steam, result!.Provider);
        Assert.Equal(expected, result.ExternalId);
    }

    [Theory]
    [InlineData("Epic:epic-id")]
    [InlineData("Gog:gog-id")]
    [InlineData("Steam:1|Steam:2")]
    public void No_unique_deterministic_steam_reference_returns_null(string value)
    {
        var refs = value.Split('|').Select(Parse).ToArray();
        Assert.Null(ManualMetadataMediaSourceSelector.Select(refs));
    }

    private static CatalogProviderRef Parse(string value)
    {
        var parts = value.Split(':', 2);
        var provider = Enum.Parse<CatalogProviderKind>(parts[0]);
        return new CatalogProviderRef(
            CatalogContentId.New(), provider, parts[1], null,
            CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic,
            DateTimeOffset.UtcNow);
    }
}
