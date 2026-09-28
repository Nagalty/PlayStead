using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.ProviderActivity;
using PlayStead.Data.Database;

namespace PlayStead.Data.ProviderActivity;

public sealed class SqliteProviderObservedSessionStore : IProviderObservedSessionStore
{
    private readonly DatabaseOptions _options;
    public SqliteProviderObservedSessionStore(DatabaseOptions options) => _options = options;

    public async Task UpsertAsync(ProviderObservedSession session, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO provider_observed_sessions(session_id, game_id, provider, provider_game_id, started_at_utc, ended_at_utc, source, completeness)
            VALUES($id,$game,$provider,$external,$started,$ended,$source,$complete)
            ON CONFLICT(provider, provider_game_id, started_at_utc, source) DO UPDATE SET
                session_id=excluded.session_id,
                game_id=excluded.game_id,
                ended_at_utc=excluded.ended_at_utc,
                completeness=excluded.completeness;
            """;
        command.Parameters.AddWithValue("$id", session.SessionId.ToString("D"));
        command.Parameters.AddWithValue("$game", session.GameId.ToString());
        command.Parameters.AddWithValue("$provider", (int)session.Provider);
        command.Parameters.AddWithValue("$external", session.ProviderGameId);
        command.Parameters.AddWithValue("$started", session.StartedAtUtc is null ? DBNull.Value : Format(session.StartedAtUtc.Value));
        command.Parameters.AddWithValue("$ended", session.EndedAtUtc is null ? DBNull.Value : Format(session.EndedAtUtc.Value));
        command.Parameters.AddWithValue("$source", session.Source);
        command.Parameters.AddWithValue("$complete", (int)session.Completeness);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT session_id, game_id, provider, provider_game_id, started_at_utc, ended_at_utc, source, completeness
            FROM provider_observed_sessions
            WHERE (started_at_utc IS NULL OR started_at_utc <= $end)
              AND (ended_at_utc IS NULL OR ended_at_utc >= $start)
            ORDER BY started_at_utc;
            """;
        command.Parameters.AddWithValue("$start", Format(startUtc));
        command.Parameters.AddWithValue("$end", Format(endUtc));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ProviderObservedSession>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProviderObservedSession(
                Guid.Parse(reader.GetString(0)),
                new(Guid.Parse(reader.GetString(1))),
                (PlayStead.Core.Library.ProviderKind)reader.GetInt32(2),
                reader.GetString(3),
                ReadDate(reader, 4), ReadDate(reader, 5), reader.GetString(6),
                (ProviderObservedSessionCompleteness)reader.GetInt32(7)));
        }
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _options.DatabasePath }.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? null : DateTimeOffset.Parse(reader.GetString(index), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
