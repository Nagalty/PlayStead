using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using Metadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Tests.ProviderGameMetadata;

public sealed class ProviderGameMetadataReconciliationTests
{
    [Fact]
    public async Task Explicit_unknown_collection_is_persisted_as_null_and_not_empty()
    {
        var game = GameId.New();
        var old = Metadata.Create(game, ProviderKind.Steam, "1", DateTimeOffset.UtcNow, genres: ["RPG"]);
        var store = new Store(old);
        var patch = Patch(game, ProviderField<IReadOnlyList<string>>.ExplicitUnknown);
        var sut = new ProviderGameMetadataReconciliationService(store, [new Source(patch)]);

        await sut.RefreshAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.Null(store.Value!.Genres);
    }

    [Fact]
    public async Task Not_reported_preserves_collection_and_empty_value_clears_it()
    {
        var game = GameId.New();
        var old = Metadata.Create(game, ProviderKind.Steam, "1", DateTimeOffset.UtcNow, genres: ["RPG"]);
        var store = new Store(old);
        var patch = Patch(game, ProviderField<IReadOnlyList<string>>.NotReported);
        var sut = new ProviderGameMetadataReconciliationService(store, [new Source(patch)]);
        await sut.RefreshAsync(new LibrarySnapshot([], []), CancellationToken.None);
        Assert.Equal(["RPG"], store.Value!.Genres);

        store.Value = old;
        sut = new ProviderGameMetadataReconciliationService(store, [new Source(Patch(game, ProviderField<IReadOnlyList<string>>.FromValue([])))]);
        await sut.RefreshAsync(new LibrarySnapshot([], []), CancellationToken.None);
        Assert.Empty(store.Value!.Genres!);
    }

    [Fact]
    public async Task Short_description_is_merged_and_network_style_not_reported_preserves_it()
    {
        var game = GameId.New();
        var old = Metadata.Create(game, ProviderKind.Steam, "1", DateTimeOffset.UtcNow,
            shortDescription: "Known description");
        var store = new Store(old);
        var patch = Patch(game, ProviderField<IReadOnlyList<string>>.NotReported) with
        {
            ShortDescription = ProviderField<string>.NotReported
        };
        var sut = new ProviderGameMetadataReconciliationService(store, [new Source(patch)]);

        await sut.RefreshAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.Equal("Known description", store.Value!.ShortDescription);
    }

    [Fact]
    public async Task Provider_failure_isolated_and_later_source_still_reconciles()
    {
        var game = GameId.New();
        var store = new Store(null);
        var patch = Patch(game, ProviderField<IReadOnlyList<string>>.FromValue(["Action"]));
        var sut = new ProviderGameMetadataReconciliationService(
            store,
            [new ThrowingSource(), new Source(patch)]);

        await sut.RefreshAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.NotNull(store.Value);
        Assert.Equal("1", store.Value!.ProviderGameId);
        Assert.Equal(["Action"], store.Value.Genres);
    }

    private static ProviderGameMetadataPatch Patch(GameId game, ProviderField<IReadOnlyList<string>> genres) =>
        new(game, ProviderKind.Steam, "1", genres, ProviderField<IReadOnlyList<string>>.NotReported,
            ProviderField<IReadOnlyList<string>>.NotReported, ProviderField<IReadOnlyList<string>>.NotReported,
            ProviderField<DateOnly>.NotReported, ProviderField<bool>.NotReported, ProviderField<bool>.NotReported,
            ProviderField<bool>.NotReported, ProviderField<bool>.NotReported, ProviderField<bool>.NotReported,
            ProviderGameMetadataAvailability.Partial);

    private sealed class Source(ProviderGameMetadataPatch patch) : IProviderGameMetadataSource
    {
        public ProviderKind Provider => ProviderKind.Steam;
        public Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(LibrarySnapshot _, CancellationToken __) => Task.FromResult<IReadOnlyList<ProviderGameMetadataPatch>>([patch]);
    }

    private sealed class ThrowingSource : IProviderGameMetadataSource
    {
        public ProviderKind Provider => ProviderKind.Steam;

        public Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(
            LibrarySnapshot _,
            CancellationToken __) =>
            throw new InvalidOperationException("provider fixture failure");
    }

    private sealed class Store(Metadata? value) : IProviderGameMetadataStore
    {
        public Metadata? Value { get; set; } = value;
        public Task<IReadOnlyList<Metadata>> GetAllAsync(CancellationToken _) => Task.FromResult<IReadOnlyList<Metadata>>(Value is null ? [] : [Value]);
        public Task<Metadata?> GetAsync(GameId _, ProviderKind __, CancellationToken ___) => Task.FromResult(Value);
        public Task UpsertAsync(Metadata metadata, CancellationToken _) { Value = metadata; return Task.CompletedTask; }
    }
}
