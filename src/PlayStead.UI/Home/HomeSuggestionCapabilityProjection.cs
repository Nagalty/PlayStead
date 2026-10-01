using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.UI.Home;

public enum HomeSuggestionCapabilityKind
{
    None,
    Solo,
    Group
}

internal sealed record HomeSuggestionCapabilityProjection(
    HomeSuggestionCapabilityKind Kind,
    string? Text)
{
    public static HomeSuggestionCapabilityProjection Create(ProviderGameMetadata? metadata)
    {
        if (metadata is null)
        {
            return new(HomeSuggestionCapabilityKind.None, null);
        }

        var single = metadata.SinglePlayer is true;
        var online = metadata.OnlineCoop is true;
        var multiplayer = metadata.MultiPlayer is true;
        var local = metadata.LocalCoop is true;

        if (single && online)
        {
            return new(HomeSuggestionCapabilityKind.Group, "Solo · Coop en ligne");
        }

        if (single)
        {
            return new(HomeSuggestionCapabilityKind.Solo, "Solo");
        }

        if (online)
        {
            return new(HomeSuggestionCapabilityKind.Group, "Coop en ligne");
        }

        if (multiplayer)
        {
            return new(HomeSuggestionCapabilityKind.Group, "Multijoueur");
        }

        if (local)
        {
            return new(HomeSuggestionCapabilityKind.Group, "Coop locale");
        }

        return new(HomeSuggestionCapabilityKind.None, null);
    }
}
