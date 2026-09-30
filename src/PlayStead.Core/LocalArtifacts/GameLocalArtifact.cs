using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public enum GameLocalArtifactKind
{
    Configuration,
    SaveData,
    Log,
    Other
}

public enum GameLocalArtifactStatus
{
    KnownAndExists,
    KnownButMissing
}

public enum GameLocalArtifactSource
{
    ExplicitRule,
    ProviderMetadata,
    KnownConvention,
    UserDefined
}

public sealed record GameLocalArtifactRule(
    GameId? GameId,
    GameLocalArtifactKind Kind,
    string PathTemplate,
    GameLocalArtifactSource Source = GameLocalArtifactSource.ExplicitRule,
    ProviderKind? Provider = null,
    string? ProviderGameId = null);

public sealed record GameLocalArtifact(
    GameId GameId,
    GameLocalArtifactKind Kind,
    string Path,
    GameLocalArtifactSource Source,
    GameLocalArtifactStatus Status,
    string? RuleIdentity = null,
    LocalArtifactBaselineStatus BaselineStatus = LocalArtifactBaselineStatus.NoBaseline,
    int SnapshotCount = 0,
    DateTimeOffset? LastSnapshotAtUtc = null,
    LocalArtifactDetails? Details = null)
{
    public bool Exists => Status == GameLocalArtifactStatus.KnownAndExists;
    public bool IsUserDefined => Source == GameLocalArtifactSource.UserDefined;

    public string KindLabel => Kind switch
    {
        GameLocalArtifactKind.Configuration => "Configuration",
        GameLocalArtifactKind.SaveData => "Sauvegardes",
        GameLocalArtifactKind.Log => "Logs",
        GameLocalArtifactKind.Other => "Autre",
        _ => Kind.ToString()
    };

    public string BaselineStatusLabel => BaselineStatus switch
    {
        LocalArtifactBaselineStatus.Unchanged => "Rien n’a bougé depuis mon point de repère.",
        LocalArtifactBaselineStatus.Changed => "Ça a bougé depuis mon point de repère.",
        LocalArtifactBaselineStatus.Missing => "Introuvable",
        LocalArtifactBaselineStatus.Unavailable => "Indisponible",
        _ => "J’ai encore aucun point de repère pour ce dossier."
    };

    public bool CanCaptureBaseline => Exists && BaselineStatus != LocalArtifactBaselineStatus.Unavailable;
    public bool HasBaseline => BaselineStatus is LocalArtifactBaselineStatus.Unchanged or LocalArtifactBaselineStatus.Changed;
    public bool IsBaselineVisible => Kind == GameLocalArtifactKind.Configuration;
    public bool IsSnapshotVisible => Kind is GameLocalArtifactKind.SaveData or GameLocalArtifactKind.Configuration;
    public bool IsReadOnlyMetadata => Kind is GameLocalArtifactKind.Log;
    public bool IsProtected => Exists && HasBaseline && (Kind != GameLocalArtifactKind.SaveData || SnapshotCount > 0);
    public bool CanProtect => Kind is (GameLocalArtifactKind.Configuration or GameLocalArtifactKind.SaveData)
        && Exists
        && !string.IsNullOrWhiteSpace(RuleIdentity)
        && !IsProtected;
    public string ProtectionSummaryLabel => Kind == GameLocalArtifactKind.SaveData
        ? IsProtected ? "Protégées par PlayStead" : "PlayStead peut garder des copies de sécurité de tes sauvegardes."
        : IsProtected ? "Protégée par PlayStead" : "PlayStead peut garder un œil sur cette configuration.";
    public string LastCopyLabel => LastSnapshotAtUtc is null ? string.Empty : $"Dernière copie : {LastSnapshotAtUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}";
    public string CopyCountLabel => SnapshotCount switch
    {
        1 => "1 copie conservée",
        > 1 => $"{SnapshotCount} copies conservées",
        _ => string.Empty
    };
    public string BaselineActionLabel => HasBaseline ? "Mettre à jour mon point de repère" : "Prendre cet état comme point de repère";
    public string ProtectionStatusLabel => BaselineStatus switch
    {
        LocalArtifactBaselineStatus.NoBaseline => "Pas encore protégé",
        _ when Kind == GameLocalArtifactKind.SaveData && SnapshotCount == 0 => "Point de repère uniquement",
        _ => "Protégé"
    };
    public string LastSnapshotLabel => LastSnapshotAtUtc is null ? string.Empty : $"Dernière sauvegarde · {LastSnapshotAtUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}";
    public string SnapshotCountLabel => SnapshotCount switch
    {
        1 => "1 sauvegarde conservée",
        > 1 => $"{SnapshotCount} sauvegardes conservées",
        _ => string.Empty
    };
    public bool HasSnapshots => SnapshotCount > 0;
    public bool HasDetails => Details is not null;
    public bool IsFile => Details?.IsFile == true;
    public bool CanCompare => Kind == GameLocalArtifactKind.Configuration && IsFile && Exists && BaselineStatus == LocalArtifactBaselineStatus.Changed && SnapshotCount > 0;
    public string OpenActionLabel => IsFile ? "Ouvrir le fichier" : "Ouvrir le dossier";
    public string DetailsLabel
    {
        get
        {
            if (Details is null) return "Informations indisponibles.";
            var parts = new List<string>();
            if (Details.FileCount is int count)
                parts.Add($"{count} fichier{(count == 1 ? string.Empty : "s")}");
            if (Details.TotalBytes is long bytes)
                parts.Add(FormatBytes(bytes));
            if (Details.LastModifiedUtc is DateTimeOffset modified)
                parts.Add($"modifié {FormatDate(modified)}");
            return parts.Count == 0 ? "Informations indisponibles." : string.Join(" · ", parts);
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824d:0.#} Go",
        >= 1_048_576 => $"{bytes / 1_048_576d:0.#} Mo",
        >= 1024 => $"{bytes / 1024d:0.#} Ko",
        _ => $"{bytes} octets"
    };

    private static string FormatDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date
            ? $"aujourd’hui à {local:HH:mm}"
            : local.Date == DateTimeOffset.Now.Date.AddDays(-1)
                ? $"hier à {local:HH:mm}"
                : $"le {local:dd/MM/yyyy} à {local:HH:mm}";
    }
}

public interface IGameLocalArtifactDiscoveryService
{
    Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(
        GameId gameId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(
        GameId gameId,
        ProviderKind? provider,
        string? providerGameId,
        CancellationToken cancellationToken)
        => DiscoverAsync(gameId, cancellationToken);
}
