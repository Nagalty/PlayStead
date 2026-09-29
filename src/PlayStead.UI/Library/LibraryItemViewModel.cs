using System.ComponentModel;
using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Core.Catalog;
using PlayStead.Core.Steam;

namespace PlayStead.UI.Library;

public sealed record LibraryItemViewModel(
    GameId GameId,
    string Title,
    ProviderKind Provider,
    string ProviderLabel,
    string InstallPath,
    long? InstalledSizeBytes,
    SteamUpdateState? SteamState = null,
    bool IsSessionActive = false,
    CatalogContentId? CanonicalContentId = null,
    string? ProviderGameId = null,
    bool IsLocallyProtected = false)
    : INotifyPropertyChanged
{
    private string? _coverPath;
    private string? _logoPath;
    public string? CoverPath =>
        _coverPath;

    public bool HasCover =>
        !string.IsNullOrWhiteSpace(_coverPath);

    public string? LogoPath =>
        _logoPath;

    public bool HasLogo =>
        !string.IsNullOrWhiteSpace(_logoPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetCoverPath(string? path)
    {
        if (string.Equals(
            _coverPath,
            path,
            StringComparison.Ordinal))
        {
            return;
        }

        _coverPath = path;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(CoverPath)));

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(HasCover)));
    }

    public void SetLogoPath(string? path)
    {
        if (string.Equals(
            _logoPath,
            path,
            StringComparison.Ordinal))
        {
            return;
        }

        _logoPath = path;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(LogoPath)));

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(HasLogo)));
    }

    public string InstalledSizeLabel =>
        InstalledSizeBytes is long bytes
            ? $"{(bytes / 1_000_000_000d).ToString("N1", UiDisplayCulture.Current)} Go".Replace(
                '\u00A0',
                ' ')
            : "Taille inconnue";

    public bool HasSteamStatus =>
        SteamState is not null;

    public string ProtectionBadgeTooltip => "Protégé par PlayStead";

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

    public string? SessionStatusLabel =>
        IsSessionActive
            ? "En cours"
            : null;
}
