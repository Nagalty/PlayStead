using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Providers.Igdb;

namespace PlayStead.Providers.Tests;

public sealed class IgdbGameMetadataMapperTests
{
    [Theory]
    [InlineData("helldivers2.json", "553850", 4)]
    [InlineData("enshrouded.json", "1203620", 16)]
    public void Captured_fixture_maps_structured_online_coop_capacity(string file, string steamId, int capacity)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine("Fixtures", "Igdb", file)));
        var ok = IgdbGameMetadataMapper.TryMap(document.RootElement, new GameId(Guid.NewGuid()), ProviderKind.Steam, DateTimeOffset.UtcNow, out var patch);

        Assert.True(ok);
        Assert.Equal(steamId, patch.ProviderGameId);
        Assert.Equal(capacity, patch.OnlineCoopMaxPlayers.Value);
        Assert.True(patch.OnlineCoop.Value);
    }

    [Fact]
    public void Mapping_requires_authoritative_Steam_external_identity_and_never_uses_title()
    {
        using var document = JsonDocument.Parse("{\"name\":\"Helldivers 2\",\"multiplayer_modes\":[{\"onlinecoopmax\":4}]}" );
        Assert.False(IgdbGameMetadataMapper.TryMap(document.RootElement, new GameId(Guid.NewGuid()), ProviderKind.Steam, DateTimeOffset.UtcNow, out _));
    }
}
