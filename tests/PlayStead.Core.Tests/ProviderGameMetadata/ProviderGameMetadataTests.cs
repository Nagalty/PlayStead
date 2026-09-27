using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Core.Tests.ProviderGameMetadata;

public sealed class ProviderGameMetadataTests
{
    [Fact]
    public void Provider_field_states_are_distinguishable()
    {
        Assert.Equal(ProviderFieldState.NotReported, ProviderField<string>.NotReported.State);
        Assert.Equal(ProviderFieldState.Value, ProviderField<string>.FromValue("value").State);
        Assert.Equal("value", ProviderField<string>.FromValue("value").Value);
        Assert.Equal(ProviderFieldState.ExplicitUnknown, ProviderField<string>.ExplicitUnknown.State);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, null, true)]
    [InlineData(false, true, true)]
    [InlineData(null, true, true)]
    [InlineData(false, false, false)]
    [InlineData(null, null, null)]
    [InlineData(false, null, null)]
    [InlineData(null, false, null)]
    public void Supports_coop_derives_nullable_value(bool? online, bool? local, bool? expected)
    {
        var metadata = global::PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata.Create(
            GameId.New(), ProviderKind.Steam, "123", DateTimeOffset.UtcNow,
            onlineCoop: online, localCoop: local);

        Assert.Equal(expected, metadata.SupportsCoop);
    }

    [Fact]
    public void Collections_are_normalized_deterministically()
    {
        var metadata = global::PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata.Create(
            GameId.New(), ProviderKind.Steam, "123", DateTimeOffset.UtcNow,
            genres: [" RPG ", "Action", "action"]);

        Assert.Equal(["Action", "RPG"], metadata.Genres);
    }
}
