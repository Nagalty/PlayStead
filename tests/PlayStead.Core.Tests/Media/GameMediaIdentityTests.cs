using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Core.Tests.Media;

public sealed class GameMediaIdentityTests
{
    [Fact]
    public void Steam_identity_requires_a_numeric_provider_id()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameMediaIdentity(
                ProviderKind.Steam,
                "PROJECT QUARANTINE",
                "Incursion Red River"));
    }

    [Fact]
    public void Steam_identity_keeps_exact_AppId_and_title()
    {
        var value = new GameMediaIdentity(
            ProviderKind.Steam,
            "1874880",
            "Arma Reforger");

        Assert.Equal(ProviderKind.Steam, value.Provider);
        Assert.Equal("1874880", value.ProviderGameId);
        Assert.Equal("Arma Reforger", value.CanonicalTitle);
        Assert.Empty(value.ExternalIds);
    }

    [Fact]
    public void External_ids_are_unchanged_when_the_caller_dictionary_changes()
    {
        var externalIds = new Dictionary<string, string>
        {
            ["igdb"] = "1234"
        };
        var value = new GameMediaIdentity(
            ProviderKind.Gog,
            "gog-42",
            "Example Game",
            externalIds);

        externalIds["igdb"] = "changed";
        externalIds["steam"] = "1874880";

        Assert.Equal("1234", value.ExternalIds["igdb"]);
        Assert.False(value.ExternalIds.ContainsKey("steam"));
    }
}
