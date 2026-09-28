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
    GameLocalArtifactStatus Status)
{
    public bool Exists => Status == GameLocalArtifactStatus.KnownAndExists;

    public string KindLabel => Kind switch
    {
        GameLocalArtifactKind.Configuration => "Configuration",
        GameLocalArtifactKind.SaveData => "Sauvegardes",
        GameLocalArtifactKind.Log => "Logs",
        _ => Kind.ToString()
    };
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
