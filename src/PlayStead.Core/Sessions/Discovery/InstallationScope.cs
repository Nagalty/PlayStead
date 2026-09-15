using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions.Discovery;

public sealed record InstallationScope
{
    public InstallationScope(GameId gameId, InstallationId installationId, string rootPath, Guid generationId, bool isPresent)
    {
        if (gameId.Value == Guid.Empty)
            throw new ArgumentException("Game identity must not be empty.", nameof(gameId));
        if (installationId.Value == Guid.Empty)
            throw new ArgumentException("Installation identity must not be empty.", nameof(installationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (generationId == Guid.Empty)
            throw new ArgumentException("Inventory generation must not be empty.", nameof(generationId));

        GameId = gameId;
        InstallationId = installationId;
        RootPath = rootPath;
        GenerationId = generationId;
        IsPresent = isPresent;
    }

    public GameId GameId { get; }
    public InstallationId InstallationId { get; }
    public string RootPath { get; }
    public Guid GenerationId { get; }
    public bool IsPresent { get; }
}
