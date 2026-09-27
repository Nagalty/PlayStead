using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests;

public sealed class SteamGameMetadataMapperTests
{
    [Fact]
    public void Observed_category_descriptions_map_to_capabilities_without_invented_ids()
    {
        var entry = new SteamAppInfoEntry(123, "Dev", "Pub", ["RPG"],
            ["Single-player", "Online Co-op", "Shared/Split Screen Co-op"], "2024-01-02", true, true);

        var patch = SteamGameMetadataMapper.Map(entry, GameId.New(), ProviderKind.Steam, DateTimeOffset.UtcNow);

        Assert.Equal(ProviderFieldState.Value, patch.SinglePlayer.State);
        Assert.Equal(ProviderFieldState.Value, patch.OnlineCoop.State);
        Assert.Equal(ProviderFieldState.Value, patch.LocalCoop.State);
        Assert.Equal(ProviderFieldState.Value, patch.ReleaseDate.State);
        Assert.Equal(new DateOnly(2024, 1, 2), patch.ReleaseDate.Value);
    }

    [Fact]
    public void Malformed_release_date_is_not_reported()
    {
        var entry = new SteamAppInfoEntry(123, null, null, ReleaseDateText: "not-a-date", ReleaseDateReported: true);

        var patch = SteamGameMetadataMapper.Map(entry, GameId.New(), ProviderKind.Steam, DateTimeOffset.UtcNow);

        Assert.Equal(ProviderFieldState.NotReported, patch.ReleaseDate.State);
    }
}
