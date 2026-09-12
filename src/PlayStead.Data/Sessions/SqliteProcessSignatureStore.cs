using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;

namespace PlayStead.Data.Sessions;

public sealed class SqliteProcessSignatureStore :
    IProcessSignatureStore
{
    private readonly DatabaseOptions _options;

    public SqliteProcessSignatureStore(
        DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertAsync(
        ProcessSignature signature,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(signature.Entries);

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        await EnableForeignKeysAsync(
            connection,
            cancellationToken);

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        var upsertSignature = connection.CreateCommand();
        upsertSignature.Transaction = transaction;
        upsertSignature.CommandText = """
            INSERT INTO process_signatures(
                game_id,
                origin,
                updated_at_utc)
            VALUES(
                $gameId,
                $origin,
                $updatedAtUtc)
            ON CONFLICT(game_id)
            DO UPDATE SET
                origin = excluded.origin,
                updated_at_utc = excluded.updated_at_utc;
            """;

        upsertSignature.Parameters.AddWithValue(
            "$gameId",
            signature.GameId.ToString());

        upsertSignature.Parameters.AddWithValue(
            "$origin",
            (int)signature.Origin);

        upsertSignature.Parameters.AddWithValue(
            "$updatedAtUtc",
            FormatUtc(signature.UpdatedAtUtc));

        await upsertSignature.ExecuteNonQueryAsync(
            cancellationToken);

        var deleteEntries = connection.CreateCommand();
        deleteEntries.Transaction = transaction;
        deleteEntries.CommandText = """
            DELETE FROM process_signature_entries
            WHERE game_id = $gameId;
            """;

        deleteEntries.Parameters.AddWithValue(
            "$gameId",
            signature.GameId.ToString());

        await deleteEntries.ExecuteNonQueryAsync(
            cancellationToken);

        for (var index = 0;
             index < signature.Entries.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = signature.Entries[index];

            if (string.IsNullOrWhiteSpace(entry.ExecutableName))
            {
                throw new ArgumentException(
                    "Process signature executable names cannot be empty.",
                    nameof(signature));
            }

            var insertEntry = connection.CreateCommand();
            insertEntry.Transaction = transaction;
            insertEntry.CommandText = """
                INSERT INTO process_signature_entries(
                    game_id,
                    ordinal,
                    executable_name,
                    kind)
                VALUES(
                    $gameId,
                    $ordinal,
                    $executableName,
                    $kind);
                """;

            insertEntry.Parameters.AddWithValue(
                "$gameId",
                signature.GameId.ToString());

            insertEntry.Parameters.AddWithValue(
                "$ordinal",
                index);

            insertEntry.Parameters.AddWithValue(
                "$executableName",
                entry.ExecutableName);

            insertEntry.Parameters.AddWithValue(
                "$kind",
                (int)entry.Kind);

            await insertEntry.ExecuteNonQueryAsync(
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProcessSignature?> GetAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                game_id,
                origin,
                updated_at_utc
            FROM process_signatures
            WHERE game_id = $gameId
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var origin = ReadEnum<ProcessSignatureOrigin>(
            reader.GetInt32(1),
            "process signature origin");

        var updatedAtUtc =
            ParseUtc(reader.GetString(2));

        await reader.DisposeAsync();

        var entries = await LoadEntriesAsync(
            connection,
            gameId,
            cancellationToken);

        return new ProcessSignature(
            gameId,
            entries,
            origin,
            updatedAtUtc);
    }

    public async Task<IReadOnlyList<ProcessSignature>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                game_id,
                origin,
                updated_at_utc
            FROM process_signatures
            ORDER BY game_id COLLATE BINARY;
            """;

        var parents = new List<SignatureHeader>();

        await using (var reader =
                     await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                parents.Add(
                    new SignatureHeader(
                        Guid.Parse(reader.GetString(0)),
                        ReadEnum<ProcessSignatureOrigin>(
                            reader.GetInt32(1),
                            "process signature origin"),
                        ParseUtc(reader.GetString(2))));
            }
        }

        var result =
            new List<ProcessSignature>(parents.Count);

        foreach (var parent in parents)
        {
            var entries = await LoadEntriesAsync(
                connection,
                parent.GameId,
                cancellationToken);

            result.Add(
                new ProcessSignature(
                    parent.GameId,
                    entries,
                    parent.Origin,
                    parent.UpdatedAtUtc));
        }

        return result;
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

    private static async Task<IReadOnlyList<ProcessSignatureEntry>> LoadEntriesAsync(
        SqliteConnection connection,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                executable_name,
                kind
            FROM process_signature_entries
            WHERE game_id = $gameId
            ORDER BY ordinal;
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

        var result =
            new List<ProcessSignatureEntry>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new ProcessSignatureEntry(
                    reader.GetString(0),
                    ReadEnum<ProcessSignatureEntryKind>(
                        reader.GetInt32(1),
                        "process signature entry kind")));
        }

        return result;
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

    private sealed record SignatureHeader(
        Guid GameId,
        ProcessSignatureOrigin Origin,
        DateTimeOffset UpdatedAtUtc);
}
