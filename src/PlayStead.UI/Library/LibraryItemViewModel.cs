using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Core.Steam;

namespace PlayStead.UI.Library;

public sealed record LibraryItemViewModel(
    GameId GameId,
    string Title,
    ProviderKind Provider,
    string ProviderLabel,
    string InstallPath,
    long? InstalledSizeBytes,
    SteamUpdateState? SteamState = null)
{
    private static readonly CultureInfo DisplayCulture =
        CultureInfo.GetCultureInfo("fr-FR");

    public string InstalledSizeLabel =>
        InstalledSizeBytes is long bytes
            ? $"{bytes / 1_000_000_000d:N1} Go".Replace(
                '\u00A0',
                ' ')
            : "Taille inconnue";

    public bool HasSteamStatus =>
        SteamState is not null;

    public string SteamStatusLabel =>
        SteamState switch
        {
            SteamUpdateState.UpToDate =>
                "À jour",

            SteamUpdateState.UpdateAvailable =>
                "Mise à jour disponible",

            SteamUpdateState.NewVersionDetected =>
                "Nouvelle version détectée",

            SteamUpdateState.Unknown =>
                "État inconnu",

            SteamUpdateState.Checking =>
                "Vérification…",

            _ =>
                string.Empty
        };
}
