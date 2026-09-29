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
            "1203620"),
        new(
            GameId: null,
            GameLocalArtifactKind.SaveData,
            @"%USERPROFILE%\Documents\My Games\Fallout4\Saves",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "377160"),
        new(
            GameId: null,
            GameLocalArtifactKind.SaveData,
            @"%LOCALAPPDATA%\DuneSandbox\Saved",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "1172710"),
        new(
            GameId: null,
            GameLocalArtifactKind.SaveData,
            @"%LOCALAPPDATA%\ReadyOrNot\Saved\SaveGames",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "1144200"),
        new(
            GameId: null,
            GameLocalArtifactKind.SaveData,
            @"%LOCALAPPDATA%\Stalker2\Saved\SaveGames",
            GameLocalArtifactSource.KnownConvention,
            ProviderKind.Steam,
            "1643320")
    ];
}
