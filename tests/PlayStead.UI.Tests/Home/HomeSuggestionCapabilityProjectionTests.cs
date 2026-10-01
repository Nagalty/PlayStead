using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.UI.Home;

namespace PlayStead.UI.Tests.Home;

public sealed class HomeSuggestionCapabilityProjectionTests
{
    [Fact]
    public void Solo_only_uses_solo_icon_and_text()
    {
        var projection = HomeSuggestionCapabilityProjection.Create(Metadata(single: true));

        Assert.Equal(HomeSuggestionCapabilityKind.Solo, projection.Kind);
        Assert.Equal("Solo", projection.Text);
    }

    [Fact]
    public void Solo_plus_online_coop_uses_group_icon_and_combined_text()
    {
        var projection = HomeSuggestionCapabilityProjection.Create(Metadata(single: true, online: true));

        Assert.Equal(HomeSuggestionCapabilityKind.Group, projection.Kind);
        Assert.Equal("Solo · Coop en ligne", projection.Text);
    }

    [Fact]
    public void Online_coop_only_uses_group_icon_and_text()
    {
        var projection = HomeSuggestionCapabilityProjection.Create(Metadata(online: true));

        Assert.Equal(HomeSuggestionCapabilityKind.Group, projection.Kind);
        Assert.Equal("Coop en ligne", projection.Text);
    }

    [Fact]
    public void Multiplayer_only_uses_group_icon_and_text()
    {
        var projection = HomeSuggestionCapabilityProjection.Create(Metadata(multi: true));

        Assert.Equal(HomeSuggestionCapabilityKind.Group, projection.Kind);
        Assert.Equal("Multijoueur", projection.Text);
    }

    [Fact]
    public void Unknown_capabilities_hide_icon_and_text()
    {
        var projection = HomeSuggestionCapabilityProjection.Create(Metadata());

        Assert.Equal(HomeSuggestionCapabilityKind.None, projection.Kind);
        Assert.Null(projection.Text);
    }

    private static ProviderGameMetadata Metadata(bool? single = null, bool? online = null, bool? multi = null) =>
        ProviderGameMetadata.Create(
            GameId.New(),
            ProviderKind.Steam,
            "1",
            DateTimeOffset.UtcNow,
            singlePlayer: single,
            multiPlayer: multi,
            onlineCoop: online,
            availability: ProviderGameMetadataAvailability.Complete);
}
