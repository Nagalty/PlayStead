using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Catalog;

public sealed class CatalogModelTests
{
    [Fact]
    public void Canonical_content_supports_active_game_without_redirect()
    {
        var id =
            new CatalogContentId(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"));

        var content = new CatalogContent(
            id,
            PlaySteadPublicId.Parse("PlayStead-000001"),
            CatalogContentKind.Game,
            "Gray Zone Warfare",
            "gray zone warfare",
            new DateOnly(2024, 4, 30),
            "MADFINGER Games",
            "MADFINGER Games",
            CatalogContentStatus.Active,
            null);

        Assert.Equal(id, content.Id);
        Assert.Equal(CatalogContentKind.Game, content.Kind);
        Assert.Null(content.RedirectTargetId);
    }

    [Fact]
    public void Catalog_provider_values_are_independent_from_library_provider_values()
    {
        Assert.Equal(1, (int)CatalogProviderKind.Steam);
        Assert.Equal(100, (int)CatalogProviderKind.Igdb);
        Assert.Equal(101, (int)CatalogProviderKind.SteamGridDb);
        Assert.Equal(102, (int)CatalogProviderKind.Rawg);
    }

    [Fact]
    public void Catalog_supporting_models_expose_the_authoritative_contracts()
    {
        var contentId = new CatalogContentId(Guid.NewGuid());
        var observed = DateTimeOffset.UtcNow;

        Assert.Equal(CatalogProvenance.ProviderDirect, CatalogProvenance.ProviderDirect);
        Assert.Equal(CatalogConfidence.Deterministic, CatalogConfidence.Deterministic);
        Assert.Equal(CatalogRelationKind.RequiresBaseGame,
            CatalogRelationKind.RequiresBaseGame);

        var providerRef = new CatalogProviderRef(contentId, CatalogProviderKind.Steam,
            "1874880", "app", CatalogProvenance.ProviderDirect,
            CatalogConfidence.Deterministic, observed);
        var alias = new CatalogAlias(contentId, "Gray Zone Warfare",
            "gray zone warfare", CatalogProvenance.ProviderDirect);
        var relation = new CatalogContentRelation(contentId,
            CatalogRelationKind.RequiresBaseGame, contentId,
            CatalogProvenance.UserConfirmed, CatalogConfidence.Deterministic);
        var metadata = new CatalogMetadata(1, 0, observed);

        Assert.Equal(contentId, providerRef.ContentId);
        Assert.Equal("gray zone warfare", alias.NormalizedAlias);
        Assert.Equal(CatalogRelationKind.RequiresBaseGame, relation.RelationKind);
        Assert.Equal(1, metadata.SchemaVersion);
    }

    [Fact]
    public void Canonical_catalog_store_is_read_only_and_exposes_lookup_contract()
    {
        var methods = typeof(ICanonicalCatalogStore).GetMethods();

        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.GetMetadataAsync));
        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.GetByIdAsync));
        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.GetByPublicIdAsync));
        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.FindByProviderRefAsync));
        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.GetProviderRefsAsync));
        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.GetAliasesAsync));
        Assert.Contains(methods, method => method.Name == nameof(ICanonicalCatalogStore.GetRelationsFromAsync));
        Assert.DoesNotContain(methods, method => method.Name.StartsWith("Insert", StringComparison.Ordinal));
        Assert.DoesNotContain(methods, method => method.Name.StartsWith("Update", StringComparison.Ordinal));
        Assert.DoesNotContain(methods, method => method.Name.StartsWith("Delete", StringComparison.Ordinal));
        Assert.DoesNotContain(methods, method => method.Name.StartsWith("Upsert", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("007 First Light", "007 first light")]
    [InlineData("Dune: Awakening™", "dune awakening")]
    [InlineData("  Café  Racer  ", "cafe racer")]
    public void Manual_title_matching_uses_conservative_normalization(string title, string expected)
    {
        Assert.Equal(expected, CatalogTitleNormalizer.Normalize(title));
    }
}
