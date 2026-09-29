using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public enum GameLocalArtifactKind
{
    Configuration,
    SaveData,
    Log
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
    LocalArtifactBaselineStatus BaselineStatus = LocalArtifactBaselineStatus.NoBaseline)
{
    public bool Exists => Status == GameLocalArtifactStatus.KnownAndExists;

    public string KindLabel => Kind switch
    {
        GameLocalArtifactKind.Configuration => "Configuration",
        GameLocalArtifactKind.SaveData => "Sauvegardes",
        GameLocalArtifactKind.Log => "Logs",
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
    public string BaselineActionLabel => HasBaseline ? "Mettre à jour mon point de repère" : "Prendre cet état comme point de repère";
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
