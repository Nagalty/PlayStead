using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
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
        {
            Trace.WriteLine("[STEAM-SESSION-IMPORT] LogPath=<unavailable> ParsedEpisodes=0 Imported=0 Skipped=0 Incomplete=0 Reason=steam-root-not-found");
            return;
        }
        var path = Path.Combine(root, "logs", "gameprocess_log.txt");
        if (!File.Exists(path))
        {
            Trace.WriteLine($"[STEAM-SESSION-IMPORT] LogPath=\"{path}\" ParsedEpisodes=0 Imported=0 Skipped=0 Incomplete=0 Reason=log-not-found");
            return;
        }

        var byExternal = installations.ToDictionary(value => value.ExternalId, StringComparer.Ordinal);
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var result = _parser.Parse(ReadLines(reader));
        var imported = 0;
        var skipped = 0;
        var incomplete = 0;
        try
        {
            foreach (var session in result.Sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!byExternal.TryGetValue(session.AppId, out var installation))
                {
                    skipped++;
                    continue;
                }

                if (!session.IsComplete)
                    incomplete++;

                await _store.UpsertAsync(
                    new ProviderObservedSession(
                        StableId(session), installation.GameId, ProviderKind.Steam, session.AppId,
                        session.StartedAtUtc, session.EndedAtUtc, session.Source,
                        session.IsComplete ? ProviderObservedSessionCompleteness.Complete : ProviderObservedSessionCompleteness.Incomplete),
                    cancellationToken);
                imported++;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Trace.WriteLine($"[STEAM-SESSION-IMPORT] LogPath=\"{path}\" ParsedEpisodes={result.Sessions.Count} Imported={imported} Skipped={skipped} Incomplete={incomplete} Error={exception.GetType().Name}");
            throw;
        }

        Trace.WriteLine($"[STEAM-SESSION-IMPORT] LogPath=\"{path}\" ParsedEpisodes={result.Sessions.Count} Imported={imported} Skipped={skipped} Incomplete={incomplete}");
    }

    private static IEnumerable<string> ReadLines(TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }

    private static Guid StableId(SteamProcessLogSession session)
    {
        var key = $"Steam|{session.AppId}|{session.StartedAtUtc:O}|{session.EndedAtUtc:O}|{session.Source}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
    }
}
