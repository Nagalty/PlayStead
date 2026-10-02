using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.ProviderGameMetadata;
using Metadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Tests.ProviderGameMetadata;

public sealed class ProviderGameMetadataComposerTests
{
    [Fact]
    public void Manual_base_wins_descriptive_fields_and_steam_fills_missing_values()
    {
        var game = GameId.New();
        var manual = CreateMetadata(game, ProviderKind.Manual, "manual:1", genres: ["Canonical"], developers: ["Canonical Studio"], singlePlayer: false);
        var steam = CreateMetadata(game, ProviderKind.Steam, "123", genres: ["Steam Genre"], developers: ["Steam Studio"], publishers: ["Steam Publisher"], releaseDate: new DateOnly(2020, 1, 2), singlePlayer: true, multiPlayer: true);

        var result = ProviderGameMetadataComposer.Compose(manual, steam, new MediaSourceIdentity(ProviderKind.Steam, "123"));

        Assert.Equal(["Canonical"], result.Genres);
        Assert.Equal(["Canonical Studio"], result.Developers);
        Assert.Equal(["Steam Publisher"], result.Publishers);
        Assert.Equal(new DateOnly(2020, 1, 2), result.ReleaseDate);
        Assert.False(result.SinglePlayer);
        Assert.True(result.MultiPlayer);
        Assert.Equal(ProviderKind.Manual, result.Provider);
    }

    [Fact]
    public void Unknown_capabilities_remain_unknown_and_stale_or_missing_links_are_ignored()
    {
        var game = GameId.New();
        var manual = CreateMetadata(game, ProviderKind.Manual, "manual:1", onlineCoop: null);
        var steam = CreateMetadata(game, ProviderKind.Steam, "123", onlineCoop: true);

        var noLink = ProviderGameMetadataComposer.Compose(manual, steam, null);
        var stale = ProviderGameMetadataComposer.Compose(manual, steam, new MediaSourceIdentity(ProviderKind.Steam, "999"));
        var current = ProviderGameMetadataComposer.Compose(manual, steam, new MediaSourceIdentity(ProviderKind.Steam, "123"));

        Assert.Null(noLink.OnlineCoop);
        Assert.Null(stale.OnlineCoop);
        Assert.True(current.OnlineCoop);
    }

    [Fact]
    public void Compose_does_not_mutate_source_rows()
    {
        var game = GameId.New();
        var manual = CreateMetadata(game, ProviderKind.Manual, "manual:1", genres: null);
        var steam = CreateMetadata(game, ProviderKind.Steam, "123", genres: ["Steam"]);

        _ = ProviderGameMetadataComposer.Compose(manual, steam, new MediaSourceIdentity(ProviderKind.Steam, "123"));

        Assert.Null(manual.Genres);
        Assert.Equal(["Steam"], steam.Genres);
    }

    private static Metadata CreateMetadata(
        GameId game,
        ProviderKind provider,
        string providerGameId,
        IReadOnlyCollection<string>? genres = null,
        IReadOnlyCollection<string>? developers = null,
        IReadOnlyCollection<string>? publishers = null,
        DateOnly? releaseDate = null,
        bool? singlePlayer = null,
        bool? multiPlayer = null,
        bool? onlineCoop = null) =>
        Metadata.Create(game, provider, providerGameId, DateTimeOffset.UtcNow,
            genres: genres, developers: developers, publishers: publishers, releaseDate: releaseDate,
            singlePlayer: singlePlayer, multiPlayer: multiPlayer, onlineCoop: onlineCoop);
}
