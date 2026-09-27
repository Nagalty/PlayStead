using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Data.Database;

namespace PlayStead.Data.ProviderActivity;

public sealed class SqliteProviderActivityMetadataStore : IProviderActivityMetadataStore
{
    private readonly DatabaseOptions _options;
    public SqliteProviderActivityMetadataStore(DatabaseOptions options) => _options = options;

    public async Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, provider, provider_game_id, total_playtime_seconds, last_played_utc, refreshed_utc, availability FROM provider_activity_metadata ORDER BY game_id, provider;";
        var result = new List<ProviderActivityMetadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProviderActivityMetadata(
                new GameId(Guid.Parse(reader.GetString(0))),
                (ProviderKind)reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : TimeSpan.FromSeconds(reader.GetInt64(3)),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                (ProviderActivityAvailability)reader.GetInt32(6)));
        }
        return result;
    }

    public async Task UpsertAsync(ProviderActivityMetadata metadata, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO provider_activity_metadata(game_id, provider, provider_game_id, total_playtime_seconds, last_played_utc, refreshed_utc, availability)
            VALUES($game_id, $provider, $provider_game_id, $total, $last, $refreshed, $availability)
            ON CONFLICT(game_id, provider) DO UPDATE SET provider_game_id=excluded.provider_game_id, total_playtime_seconds=excluded.total_playtime_seconds, last_played_utc=excluded.last_played_utc, refreshed_utc=excluded.refreshed_utc, availability=excluded.availability;
            """;
        command.Parameters.AddWithValue("$game_id", metadata.GameId.Value.ToString());
        command.Parameters.AddWithValue("$provider", (int)metadata.Provider);
        command.Parameters.AddWithValue("$provider_game_id", metadata.ProviderGameId);
        command.Parameters.AddWithValue("$total", metadata.TotalPlaytime is { } total ? (object)(long)total.TotalSeconds : DBNull.Value);
        command.Parameters.AddWithValue("$last", metadata.LastPlayedAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$refreshed", metadata.RefreshedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$availability", (int)metadata.Availability);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
