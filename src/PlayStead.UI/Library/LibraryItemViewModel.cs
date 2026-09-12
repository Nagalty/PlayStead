using System.Globalization;
using PlayStead.Core.Library;

namespace PlayStead.UI.Library;

public sealed record LibraryItemViewModel(
    GameId GameId,
    string Title,
    ProviderKind Provider,
    string ProviderLabel,
    string InstallPath,
    long? InstalledSizeBytes)
{
    private static readonly CultureInfo DisplayCulture =
        CultureInfo.GetCultureInfo("fr-FR");

    public string InstalledSizeLabel =>
        InstalledSizeBytes is long bytes
            ? $"{bytes / 1_000_000_000d:N1} Go".Replace(
                '\u00A0',
                ' ')
            : "Taille inconnue";
}
