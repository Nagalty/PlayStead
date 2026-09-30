using PlayStead.Core.Catalog;

namespace PlayStead.Core.Tests.Catalog;

public sealed class CanonicalCatalogTitleNormalizerTests
{
    [Theory]
    [InlineData("Dune: Awakening™", "DUNE AWAKENING")]
    [InlineData("  Café  Racer® ", "CAFE RACER")]
    public void Normalizes_conservative_title_variants(string input, string expected) =>
        Assert.Equal(expected, CanonicalCatalogTitleNormalizer.Normalize(input));

    [Fact]
    public void Does_not_fuzzy_match_different_titles()
    {
        Assert.NotEqual(
            CanonicalCatalogTitleNormalizer.Normalize("007 First Light"),
            CanonicalCatalogTitleNormalizer.Normalize("007 First Night"));
    }
}
