using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Data.Database;
using Metadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Data.ProviderGameMetadata;

public sealed class SqliteProviderGameMetadataStore : IProviderGameMetadataStore
{
    private readonly DatabaseOptions _options;
    public SqliteProviderGameMetadataStore(DatabaseOptions options) => _options = options;

    public Task<IReadOnlyList<Metadata>> GetAllAsync(CancellationToken cancellationToken) =>
        ReadAsync(null, null, cancellationToken);

    public async Task<Metadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken)
    {
        var values = await ReadAsync(gameId, provider, cancellationToken).ConfigureAwait(false);
        return values.FirstOrDefault();
    }

    public async Task UpsertAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO provider_game_metadata(
                game_id, provider, provider_game_id, genres_json, categories_json,
                developers_json, publishers_json, release_date, is_free,
                single_player, multi_player, online_coop, local_coop, refreshed_utc, availability, short_description,
                online_coop_max_players, online_multiplayer_max_players, offline_coop_max_players, offline_multiplayer_max_players)
            VALUES($game_id, $provider, $provider_game_id, $genres, $categories,
                $developers, $publishers, $release_date, $is_free,
                $single_player, $multi_player, $online_coop, $local_coop, $refreshed, $availability, $short_description,
                $online_coop_max_players, $online_multiplayer_max_players, $offline_coop_max_players, $offline_multiplayer_max_players)
            ON CONFLICT(game_id, provider) DO UPDATE SET
                provider_game_id=excluded.provider_game_id, genres_json=excluded.genres_json,
                categories_json=excluded.categories_json, developers_json=excluded.developers_json,
                publishers_json=excluded.publishers_json, release_date=excluded.release_date,
                is_free=excluded.is_free, single_player=excluded.single_player,
                multi_player=excluded.multi_player, online_coop=excluded.online_coop,
                local_coop=excluded.local_coop, refreshed_utc=excluded.refreshed_utc,
                availability=excluded.availability, short_description=excluded.short_description,
                online_coop_max_players=excluded.online_coop_max_players,
                online_multiplayer_max_players=excluded.online_multiplayer_max_players,
                offline_coop_max_players=excluded.offline_coop_max_players,
                offline_multiplayer_max_players=excluded.offline_multiplayer_max_players;
            """;
        command.Parameters.AddWithValue("$game_id", metadata.GameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$provider", (int)metadata.Provider);
        command.Parameters.AddWithValue("$provider_game_id", metadata.ProviderGameId);
        command.Parameters.AddWithValue("$genres", Json(metadata.Genres));
        command.Parameters.AddWithValue("$categories", Json(metadata.Categories));
        command.Parameters.AddWithValue("$developers", Json(metadata.Developers));
        command.Parameters.AddWithValue("$publishers", Json(metadata.Publishers));
        command.Parameters.AddWithValue("$release_date", metadata.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$is_free", Bool(metadata.IsFree));
        command.Parameters.AddWithValue("$single_player", Bool(metadata.SinglePlayer));
        command.Parameters.AddWithValue("$multi_player", Bool(metadata.MultiPlayer));
        command.Parameters.AddWithValue("$online_coop", Bool(metadata.OnlineCoop));
        command.Parameters.AddWithValue("$local_coop", Bool(metadata.LocalCoop));
        command.Parameters.AddWithValue("$refreshed", metadata.RefreshedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$availability", (int)metadata.Availability);
        command.Parameters.AddWithValue("$short_description", (object?)metadata.ShortDescription ?? DBNull.Value);
        command.Parameters.AddWithValue("$online_coop_max_players", (object?)metadata.OnlineCoopMaxPlayers ?? DBNull.Value);
        command.Parameters.AddWithValue("$online_multiplayer_max_players", (object?)metadata.OnlineMultiplayerMaxPlayers ?? DBNull.Value);
        command.Parameters.AddWithValue("$offline_coop_max_players", (object?)metadata.OfflineCoopMaxPlayers ?? DBNull.Value);
        command.Parameters.AddWithValue("$offline_multiplayer_max_players", (object?)metadata.OfflineMultiplayerMaxPlayers ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "DELETE FROM provider_game_metadata WHERE game_id=$game_id AND provider=$provider;";
        command.Parameters.AddWithValue("$game_id", gameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$provider", (int)provider);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Metadata>> ReadAsync(GameId? gameId, ProviderKind? provider, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, provider, provider_game_id, genres_json, categories_json, developers_json, publishers_json, release_date, is_free, single_player, multi_player, online_coop, local_coop, refreshed_utc, availability, short_description, online_coop_max_players, online_multiplayer_max_players, offline_coop_max_players, offline_multiplayer_max_players FROM provider_game_metadata";
        if (gameId is not null && provider is not null)
        {
            command.CommandText += " WHERE game_id=$game_id AND provider=$provider";
            command.Parameters.AddWithValue("$game_id", gameId.Value.Value.ToString("D"));
            command.Parameters.AddWithValue("$provider", (int)provider.Value);
        }
        command.CommandText += " ORDER BY game_id, provider;";
        var result = new List<Metadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(Metadata.Create(
                new GameId(Guid.Parse(reader.GetString(0))), (ProviderKind)reader.GetInt32(1), reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(13), CultureInfo.InvariantCulture),
                ReadJson(reader, 3), ReadJson(reader, 4), ReadJson(reader, 5), ReadJson(reader, 6),
                reader.IsDBNull(7) ? null : DateOnly.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                ReadBool(reader, 8), ReadBool(reader, 9), ReadBool(reader, 10), ReadBool(reader, 11), ReadBool(reader, 12),
                (ProviderGameMetadataAvailability)reader.GetInt32(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetInt32(16),
                reader.IsDBNull(17) ? null : reader.GetInt32(17),
                reader.IsDBNull(18) ? null : reader.GetInt32(18),
                reader.IsDBNull(19) ? null : reader.GetInt32(19)));
        }
        return result;
    }

    private static object Json(IReadOnlyList<string>? values) => values is null ? DBNull.Value : JsonSerializer.Serialize(values);
    private static object Bool(bool? value) => value.HasValue ? value.Value ? 1 : 0 : DBNull.Value;
    private static bool? ReadBool(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal) != 0;
    private static IReadOnlyList<string>? ReadJson(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : JsonSerializer.Deserialize<string[]>(reader.GetString(ordinal)) ?? [];

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
