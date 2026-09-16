using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Identity;

public sealed class GameIdentityResolverTests
{
    private static readonly CatalogContent KnownContent = new(
        new CatalogContentId(
            Guid.Parse("11111111-1111-1111-1111-111111111111")),
        PlaySteadPublicId.Parse("PlayStead-000001"),
        CatalogContentKind.Game,
        "Arma Reforger",
        "arma reforger",
        new DateOnly(2022, 11, 17),
        "Bohemia Interactive",
        "Bohemia Interactive",
        CatalogContentStatus.Active,
        RedirectTargetId: null);

    [Fact]
    public async Task Exact_Steam_provider_ref_returns_match_confirmed()
    {
        var store = new ExactCatalogStore(
            CatalogProviderKind.Steam,
            "1874880",
            KnownContent);
        var resolver = new GameIdentityResolver(store);
        var observation = new GameIdentityObservation(
            CatalogProviderKind.Steam,
            "1874880",
            "Arma Reforger",
            DateTimeOffset.Parse("2026-09-16T18:00:00Z"));

        var result = await resolver.ResolveAsync(
            observation,
            CancellationToken.None);

        Assert.Equal(
            IdentityResolutionState.MatchConfirmed,
            result.State);
        Assert.Equal(KnownContent.Id, result.CandidateContentId);
        Assert.Equal(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            result.Evidence.Kind);
        Assert.Equal(
            CatalogProviderKind.Steam,
            result.Evidence.Provider);
        Assert.Equal("1874880", result.Evidence.ExternalId);
        Assert.Equal(
            KnownContent.Id,
            result.Evidence.MatchedContentId);
    }

    [Fact]
    public async Task Unknown_Steam_provider_ref_returns_new()
    {
        var resolver = new GameIdentityResolver(
            new ExactCatalogStore(
                CatalogProviderKind.Steam,
                "1874880",
                KnownContent));
        var observation = new GameIdentityObservation(
            CatalogProviderKind.Steam,
            "9999999",
            "Unknown Game",
            DateTimeOffset.Parse("2026-09-16T18:00:00Z"));

        var result = await resolver.ResolveAsync(
            observation,
            CancellationToken.None);

        Assert.Equal(IdentityResolutionState.New, result.State);
        Assert.Null(result.CandidateContentId);
        Assert.Equal(
            IdentityResolutionEvidenceKind.NoExactProviderRefMatch,
            result.Evidence.Kind);
        Assert.Equal(
            CatalogProviderKind.Steam,
            result.Evidence.Provider);
        Assert.Equal("9999999", result.Evidence.ExternalId);
        Assert.Null(result.Evidence.MatchedContentId);
    }

    [Fact]
    public async Task Same_title_without_provider_ref_never_confirms()
    {
        var resolver = new GameIdentityResolver(
            new ExactCatalogStore(
                CatalogProviderKind.Steam,
                "1874880",
                KnownContent));
        var observation = new GameIdentityObservation(
            CatalogProviderKind.Steam,
            "not-the-known-reference",
            KnownContent.CanonicalTitle,
            DateTimeOffset.Parse("2026-09-16T18:00:00Z"));

        var result = await resolver.ResolveAsync(
            observation,
            CancellationToken.None);

        Assert.Equal(IdentityResolutionState.New, result.State);
        Assert.Null(result.CandidateContentId);
    }

    [Fact]
    public void Steam_library_provider_is_mapped_explicitly()
    {
        var mapped = ProviderKindMapping.TryMap(
            ProviderKind.Steam,
            out var catalogProvider);

        Assert.True(mapped);
        Assert.Equal(CatalogProviderKind.Steam, catalogProvider);
    }

    [Theory]
    [InlineData(ProviderKind.Epic)]
    [InlineData(ProviderKind.Gog)]
    [InlineData(ProviderKind.Manual)]
    public void Unsupported_library_provider_is_not_mapped(
        ProviderKind provider)
    {
        Assert.False(
            ProviderKindMapping.TryMap(
                provider,
                out _));
    }

    [Fact]
    public async Task CancellationToken_is_propagated_to_catalog_store()
    {
        var store = new ExactCatalogStore(
            CatalogProviderKind.Steam,
            "1874880",
            KnownContent);
        var resolver = new GameIdentityResolver(store);
        using var cancellation = new CancellationTokenSource();

        await resolver.ResolveAsync(
            new GameIdentityObservation(
                CatalogProviderKind.Steam,
                "1874880",
                "Arma Reforger",
                DateTimeOffset.Parse("2026-09-16T18:00:00Z")),
            cancellation.Token);

        Assert.Equal(
            cancellation.Token,
            store.ReceivedCancellationToken);
    }

    private sealed class ExactCatalogStore : ICanonicalCatalogStore
    {
        private readonly CatalogProviderKind _provider;
        private readonly string _externalId;
        private readonly CatalogContent _content;

        public ExactCatalogStore(
            CatalogProviderKind provider,
            string externalId,
            CatalogContent content)
        {
            _provider = provider;
            _externalId = externalId;
            _content = content;
        }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<CatalogContent?> FindByProviderRefAsync(
            CatalogProviderKind provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            ReceivedCancellationToken = cancellationToken;

            CatalogContent? result =
                provider == _provider && externalId == _externalId
                    ? _content
                    : null;

            return Task.FromResult(result);
        }

        public Task<CatalogMetadata> GetMetadataAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CatalogContent?> GetByIdAsync(
            CatalogContentId contentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CatalogContent?> GetByPublicIdAsync(
            PlaySteadPublicId publicId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(
            CatalogContentId contentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(
            CatalogContentId contentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(
            CatalogContentId sourceContentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
