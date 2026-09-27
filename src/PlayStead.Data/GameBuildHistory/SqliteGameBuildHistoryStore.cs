using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.GameBuildHistory;
using PlayStead.Core.Library;
using PlayStead.Data.Database;

namespace PlayStead.Data.GameBuildHistory;

public sealed class SqliteGameBuildHistoryStore : IGameBuildHistoryStore
{
    private readonly DatabaseOptions _options;
    public SqliteGameBuildHistoryStore(DatabaseOptions options) => _options = options;

    public async Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
        (await ReadAsync(gameId, provider, true, cancellationToken)).FirstOrDefault();

    public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
        ReadAsync(gameId, provider, false, cancellationToken);

    public async Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(
        IReadOnlyCollection<GameId> gameIds,
        ProviderKind provider,
        CancellationToken cancellationToken)
    {
        if (gameIds.Count == 0) return [];
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        var parameters = gameIds.Select((gameId, index) =>
        {
            var name = "$game_id" + index;
            command.Parameters.AddWithValue(name, gameId.Value.ToString("D"));
            return name;
        }).ToArray();
        command.Parameters.AddWithValue("$provider", (int)provider);
        command.CommandText = $"SELECT game_id, provider, provider_game_id, build_id, observed_at_utc, source FROM game_build_history WHERE provider=$provider AND game_id IN ({string.Join(',', parameters)}) ORDER BY observed_at_utc, id;";
        return await ReadRowsAsync(command, cancellationToken);
    }

    public async Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(observation.BuildId)) return false;
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var latest = connection.CreateCommand();
        latest.Transaction = (SqliteTransaction)transaction;
        latest.CommandText = "SELECT build_id FROM game_build_history WHERE game_id=$game_id AND provider=$provider ORDER BY observed_at_utc DESC, id DESC LIMIT 1;";
        latest.Parameters.AddWithValue("$game_id", observation.GameId.Value.ToString("D"));
        latest.Parameters.AddWithValue("$provider", (int)observation.Provider);
        var current = await latest.ExecuteScalarAsync(cancellationToken);
        if (current is string build && string.Equals(build, observation.BuildId, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = "INSERT INTO game_build_history(game_id, provider, provider_game_id, build_id, observed_at_utc, source) VALUES($game_id,$provider,$provider_game_id,$build_id,$observed_at_utc,$source);";
        insert.Parameters.AddWithValue("$game_id", observation.GameId.Value.ToString("D"));
        insert.Parameters.AddWithValue("$provider", (int)observation.Provider);
        insert.Parameters.AddWithValue("$provider_game_id", observation.ProviderGameId);
        insert.Parameters.AddWithValue("$build_id", observation.BuildId);
        insert.Parameters.AddWithValue("$observed_at_utc", observation.ObservedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$source", (int)observation.Source);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<GameBuildObservation>> ReadAsync(GameId gameId, ProviderKind provider, bool latestOnly, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, provider, provider_game_id, build_id, observed_at_utc, source FROM game_build_history WHERE game_id=$game_id AND provider=$provider ORDER BY observed_at_utc " + (latestOnly ? "DESC, id DESC LIMIT 1" : "ASC, id ASC") + ";";
        command.Parameters.AddWithValue("$game_id", gameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$provider", (int)provider);
        return await ReadRowsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<GameBuildObservation>> ReadRowsAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var result = new List<GameBuildObservation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new GameBuildObservation(new GameId(Guid.Parse(reader.GetString(0))), (ProviderKind)reader.GetInt32(1), reader.GetString(2), reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture), (GameBuildObservationSource)reader.GetInt32(5)));
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
