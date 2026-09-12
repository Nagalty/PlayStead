using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;

namespace PlayStead.Data.Sessions;

public sealed class SqliteSessionStore : ISessionStore
{
    private readonly DatabaseOptions _options;

    public SqliteSessionStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertAsync(
        GameSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        await EnableForeignKeysAsync(
            connection,
            cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_sessions(
                session_id,
                game_id,
                observed_started_at_utc,
                last_seen_at_utc,
                observed_ended_at_utc,
                state,
                end_reason,
                detection_source,
                created_at_utc,
                updated_at_utc)
            VALUES(
                $sessionId,
                $gameId,
                $observedStartedAtUtc,
                $lastSeenAtUtc,
                $observedEndedAtUtc,
                $state,
                $endReason,
                $detectionSource,
                $createdAtUtc,
                $updatedAtUtc)
            ON CONFLICT(session_id)
            DO UPDATE SET
                game_id = excluded.game_id,
                observed_started_at_utc = excluded.observed_started_at_utc,
                last_seen_at_utc = excluded.last_seen_at_utc,
                observed_ended_at_utc = excluded.observed_ended_at_utc,
                state = excluded.state,
                end_reason = excluded.end_reason,
                detection_source = excluded.detection_source,
                created_at_utc = excluded.created_at_utc,
                updated_at_utc = excluded.updated_at_utc;
            """;

        command.Parameters.AddWithValue(
            "$sessionId",
            session.SessionId.ToString());

        command.Parameters.AddWithValue(
            "$gameId",
            session.GameId.ToString());

        command.Parameters.AddWithValue(
            "$observedStartedAtUtc",
            FormatUtc(session.ObservedStartedAtUtc));

        command.Parameters.AddWithValue(
            "$lastSeenAtUtc",
            FormatUtc(session.LastSeenAtUtc));

        command.Parameters.AddWithValue(
            "$observedEndedAtUtc",
            session.ObservedEndedAtUtc is null
                ? DBNull.Value
                : FormatUtc(session.ObservedEndedAtUtc.Value));

        command.Parameters.AddWithValue(
            "$state",
            (int)session.State);

        command.Parameters.AddWithValue(
            "$endReason",
            session.EndReason is null
                ? DBNull.Value
                : (int)session.EndReason.Value);

        command.Parameters.AddWithValue(
            "$detectionSource",
            (int)session.DetectionSource);

        command.Parameters.AddWithValue(
            "$createdAtUtc",
            FormatUtc(session.CreatedAtUtc));

        command.Parameters.AddWithValue(
            "$updatedAtUtc",
            FormatUtc(session.UpdatedAtUtc));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<GameSession?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                session_id,
                game_id,
                observed_started_at_utc,
                last_seen_at_utc,
                observed_ended_at_utc,
                state,
                end_reason,
                detection_source,
                created_at_utc,
                updated_at_utc
            FROM game_sessions
            WHERE session_id = $sessionId
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$sessionId",
            sessionId.ToString());

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadSession(reader);
    }

    public async Task<IReadOnlyList<GameSession>> GetActiveAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                session_id,
                game_id,
                observed_started_at_utc,
                last_seen_at_utc,
                observed_ended_at_utc,
                state,
                end_reason,
                detection_source,
                created_at_utc,
                updated_at_utc
            FROM game_sessions
            WHERE state = $activeState
            ORDER BY observed_started_at_utc, session_id;
            """;

        command.Parameters.AddWithValue(
            "$activeState",
            (int)SessionState.Active);

        return await ReadManyAsync(
            command,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GameSession>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Recent session limit must be greater than zero.");
        }

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                session_id,
                game_id,
                observed_started_at_utc,
                last_seen_at_utc,
                observed_ended_at_utc,
                state,
                end_reason,
                detection_source,
                created_at_utc,
                updated_at_utc
            FROM game_sessions
            ORDER BY observed_started_at_utc DESC, session_id DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        return await ReadManyAsync(
            command,
            cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private static async Task EnableForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "PRAGMA foreign_keys = ON;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<GameSession>> ReadManyAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var result = new List<GameSession>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadSession(reader));
        }

        return result;
    }

    private static GameSession ReadSession(
        SqliteDataReader reader)
    {
        var state = ReadEnum<SessionState>(
            reader.GetInt32(5),
            "session state");

        SessionEndReason? endReason = null;

        if (!reader.IsDBNull(6))
        {
            endReason = ReadEnum<SessionEndReason>(
                reader.GetInt32(6),
                "session end reason");
        }

        var detectionSource =
            ReadEnum<SessionDetectionSource>(
                reader.GetInt32(7),
                "session detection source");

        return new GameSession(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            ParseUtc(reader.GetString(2)),
            ParseUtc(reader.GetString(3)),
            reader.IsDBNull(4)
                ? null
                : ParseUtc(reader.GetString(4)),
            state,
            endReason,
            detectionSource,
            ParseUtc(reader.GetString(8)),
            ParseUtc(reader.GetString(9)));
    }

    private static TEnum ReadEnum<TEnum>(
        int value,
        string label)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            throw new FormatException(
                $"Unknown {label} value '{value}'.");
        }

        return (TEnum)Enum.ToObject(
            typeof(TEnum),
            value);
    }

    private static string FormatUtc(
        DateTimeOffset value)
        => value
            .ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseUtc(
        string value)
        => DateTimeOffset.ParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
}
