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
                session_id,
                corrected_started_at_utc,
                corrected_ended_at_utc,
                corrected_at_utc)
            VALUES(
                $sessionId,
                $correctedStartedAtUtc,
                $correctedEndedAtUtc,
                $correctedAtUtc)
            ON CONFLICT(session_id)
            DO UPDATE SET
                corrected_started_at_utc =
                    excluded.corrected_started_at_utc,
                corrected_ended_at_utc =
                    excluded.corrected_ended_at_utc,
                corrected_at_utc =
                    excluded.corrected_at_utc;
            """;

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
            "$correctedAtUtc",
            FormatUtc(
                correction.CorrectedAtUtc));

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
                session_id,
                corrected_started_at_utc,
                corrected_ended_at_utc,
                corrected_at_utc
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
            reader.IsDBNull(1)
                ? null
                : ParseUtc(
                    reader.GetString(1)),
            reader.IsDBNull(2)
                ? null
                : ParseUtc(
                    reader.GetString(2)),
            ParseUtc(
                reader.GetString(3)));
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
