using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.ManualMetadata;

public sealed class SqliteManualMetadataLinkStore : IManualMetadataLinkStore
{
    private readonly DatabaseOptions _options;
    private readonly Dictionary<GameId, ManualMetadataLink> _cache = [];
    private readonly object _gate = new();

    public SqliteManualMetadataLinkStore(DatabaseOptions options) => _options = options;

    public ManualMetadataLink? TryGetCached(GameId gameId)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(gameId, out var value))
                return value;
        }

        if (!File.Exists(_options.DatabasePath))
            return null;

        using var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT canonical_content_id, media_provider, media_external_id, matched_at_utc FROM manual_metadata_links WHERE game_id=$game;";
        command.Parameters.AddWithValue("$game", gameId.ToString());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        var loaded = Read(gameId, reader);
        lock (_gate) _cache[gameId] = loaded;
        return loaded;
    }

    public async Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var cached = TryGetCached(gameId);
        if (cached is not null) return cached;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT canonical_content_id, media_provider, media_external_id, matched_at_utc FROM manual_metadata_links WHERE game_id=$game;";
        command.Parameters.AddWithValue("$game", gameId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var link = Read(gameId, reader);
        lock (_gate) _cache[gameId] = link;
        return link;
    }

    public async Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO manual_metadata_links(game_id, canonical_content_id, media_provider, media_external_id, matched_at_utc)
            VALUES($game,$canonical,$provider,$external,$matched)
            ON CONFLICT(game_id) DO UPDATE SET canonical_content_id=excluded.canonical_content_id,
                media_provider=excluded.media_provider, media_external_id=excluded.media_external_id,
                matched_at_utc=excluded.matched_at_utc;
            """;
        command.Parameters.AddWithValue("$game", link.ManualGameId.ToString());
        command.Parameters.AddWithValue("$canonical", link.CanonicalCatalogId.ToString());
        command.Parameters.AddWithValue("$provider", link.MediaSource is null ? DBNull.Value : (object)(int)link.MediaSource.Provider);
        command.Parameters.AddWithValue("$external", (object?)link.MediaSource?.ExternalId ?? DBNull.Value);
        command.Parameters.AddWithValue("$matched", link.MatchedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate) _cache[link.ManualGameId] = link;
    }

    public async Task RemoveAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM manual_metadata_links WHERE game_id=$game;";
        command.Parameters.AddWithValue("$game", gameId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate) _cache.Remove(gameId);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static ManualMetadataLink Read(GameId gameId, SqliteDataReader reader)
    {
        MediaSourceIdentity? media = null;
        if (!reader.IsDBNull(1) && !reader.IsDBNull(2))
            media = new MediaSourceIdentity((ProviderKind)reader.GetInt32(1), reader.GetString(2));
        return new ManualMetadataLink(
            gameId,
            new CatalogContentId(Guid.Parse(reader.GetString(0))),
            media,
            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture));
    }
}
