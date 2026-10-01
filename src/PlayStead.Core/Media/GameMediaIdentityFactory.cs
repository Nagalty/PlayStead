using PlayStead.Core.Library;

namespace PlayStead.Core.Media;

public static class GameMediaIdentityFactory
{
    public static GameMediaIdentity Create(GameId gameId, GameInstallation installation, string title)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var providerGameId = installation.Provider == ProviderKind.Manual
            ? $"manual:{gameId.Value:D}"
            : installation.ExternalId;
        return new GameMediaIdentity(installation.Provider, providerGameId, title);
    }
}
