using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

/// <summary>
/// Bounded, evidence-backed rules for installed games. Rules are keyed by provider identity,
/// never by display title or executable name.
/// </summary>
public static class LocalArtifactRuleCatalog
{
    public static IReadOnlyList<GameLocalArtifactRule> Rules { get; } =
    [
        new(
            GameId: null,
            GameLocalArtifactKind.Configuration,
            @"%APPDATA%\Guild Wars 2",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "1284210"),
        new(
            GameId: null,
            GameLocalArtifactKind.Configuration,
            @"%USERPROFILE%\Documents\Guild Wars 2",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "1284210"),
        new(
            GameId: null,
            GameLocalArtifactKind.SaveData,
            @"%USERPROFILE%\Saved Games\Enshrouded",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "1203620")
    ];
}
