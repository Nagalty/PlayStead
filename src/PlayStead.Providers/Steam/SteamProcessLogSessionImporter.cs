using System.Security.Cryptography;
using System.Text;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Providers.Steam;

public sealed class SteamProcessLogSessionImporter
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamProcessLogSessionParser _parser;
    private readonly IProviderObservedSessionStore _store;

    public SteamProcessLogSessionImporter(
        WindowsSteamRootLocator rootLocator,
        SteamProcessLogSessionParser parser,
        IProviderObservedSessionStore store)
    {
        _rootLocator = rootLocator;
        _parser = parser;
        _store = store;
    }

    public async Task ImportAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        var root = _rootLocator.TryLocate();
        if (root is null)
            return;
        var path = Path.Combine(root, "logs", "gameprocess_log.txt");
        if (!File.Exists(path))
            return;

        var byExternal = installations.ToDictionary(value => value.ExternalId, StringComparer.Ordinal);
        var result = _parser.Parse(File.ReadLines(path));
        foreach (var session in result.Sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byExternal.TryGetValue(session.AppId, out var installation))
                continue;

            await _store.UpsertAsync(
                new ProviderObservedSession(
                    StableId(session), installation.GameId, ProviderKind.Steam, session.AppId,
                    session.StartedAtUtc, session.EndedAtUtc, session.Source,
                    session.IsComplete ? ProviderObservedSessionCompleteness.Complete : ProviderObservedSessionCompleteness.Incomplete),
                cancellationToken);
        }
    }

    private static Guid StableId(SteamProcessLogSession session)
    {
        var key = $"Steam|{session.AppId}|{session.StartedAtUtc:O}|{session.EndedAtUtc:O}|{session.Source}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
    }
}
