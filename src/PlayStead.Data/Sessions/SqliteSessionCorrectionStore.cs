using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;

namespace PlayStead.Data.Sessions;

public sealed class SqliteSessionCorrectionStore :
    ISessionCorrectionStore
{
    private readonly DatabaseOptions _options;

    public SqliteSessionCorrectionStore(
        DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertAsync(
        SessionCorrection correction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correction);

        if (correction.CorrectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A correction identity is required.",
                nameof(correction));
        }

        if (correction.CorrectedStartedAtUtc is null &&
            correction.CorrectedEndedAtUtc is null)
        {
            throw new ArgumentException(
                "At least one corrected session timestamp is required.",
                nameof(correction));
        }

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await EnableForeignKeysAsync(
            connection,
            cancellationToken);

        var command =
            connection.CreateCommand();

        command.CommandText = """
            INSERT INTO session_corrections(
                correction_id,
                session_id,
                corrected_started_at_utc,
                corrected_ended_at_utc,
                reason,
                created_at_utc)
            VALUES(
                $correctionId,
                $sessionId,
                $correctedStartedAtUtc,
                $correctedEndedAtUtc,
                $reason,
                $createdAtUtc)
            ON CONFLICT(session_id)
            DO UPDATE SET
                correction_id =
                    excluded.correction_id,
                corrected_started_at_utc =
                    excluded.corrected_started_at_utc,
                corrected_ended_at_utc =
                    excluded.corrected_ended_at_utc,
                reason =
                    excluded.reason,
                created_at_utc =
                    excluded.created_at_utc;
            """;

        command.Parameters.AddWithValue(
            "$correctionId",
            correction.CorrectionId.ToString());

        command.Parameters.AddWithValue(
            "$sessionId",
            correction.SessionId.ToString());

        command.Parameters.AddWithValue(
            "$correctedStartedAtUtc",
            correction.CorrectedStartedAtUtc is null
                ? DBNull.Value
                : FormatUtc(
                    correction.CorrectedStartedAtUtc.Value));

        command.Parameters.AddWithValue(
            "$correctedEndedAtUtc",
            correction.CorrectedEndedAtUtc is null
                ? DBNull.Value
                : FormatUtc(
                    correction.CorrectedEndedAtUtc.Value));

        command.Parameters.AddWithValue(
            "$reason",
            correction.Reason is null
                ? DBNull.Value
                : correction.Reason);

        command.Parameters.AddWithValue(
            "$createdAtUtc",
            FormatUtc(
                correction.CreatedAtUtc));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task<SessionCorrection?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        var command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT
                correction_id,
                session_id,
                corrected_started_at_utc,
                corrected_ended_at_utc,
                reason,
                created_at_utc
            FROM session_corrections
            WHERE session_id = $sessionId
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$sessionId",
            sessionId.ToString());

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        return new SessionCorrection(
            Guid.Parse(
                reader.GetString(0)),
            Guid.Parse(
                reader.GetString(1)),
            reader.IsDBNull(2)
                ? null
                : ParseUtc(
                    reader.GetString(2)),
            reader.IsDBNull(3)
                ? null
                : ParseUtc(
                    reader.GetString(3)),
            reader.IsDBNull(4)
                ? null
                : reader.GetString(4),
            ParseUtc(
                reader.GetString(5)));
    }

    private async Task<SqliteConnection>
        OpenConnectionAsync(
            CancellationToken cancellationToken)
    {
        var connection =
            new SqliteConnection(
                $"Data Source={_options.DatabasePath};Pooling=False");

        await connection.OpenAsync(
            cancellationToken);

        return connection;
    }

    private static async Task EnableForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command =
            connection.CreateCommand();

        command.CommandText =
            "PRAGMA foreign_keys = ON;";

        await command.ExecuteNonQueryAsync(
            cancellationToken);
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
