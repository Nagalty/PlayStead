using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;

namespace PlayStead.Data.Library;

public sealed class SqliteLibraryStore : ILibraryStore
{
    private readonly DatabaseOptions _options;

    public SqliteLibraryStore(DatabaseOptions options)
    {
        _options = options;
    }

    public async Task ApplySourceScanAsync(
        SourceScanResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");

        await connection.OpenAsync(cancellationToken);
        await EnableForeignKeysAsync(connection, cancellationToken);

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        foreach (var discovered in result.Installations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gameId = await GetOrCreateGameAsync(
                connection,
                transaction,
                discovered,
                cancellationToken);

            await UpsertInstallationAsync(
                connection,
                transaction,
                gameId,
                discovered,
                cancellationToken);
        }

        if (result.IsComplete)
        {
            await MarkMissingInstallationsAbsentAsync(
                connection,
                transaction,
                result.Provider,
                result.ObservedAtUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<LibrarySnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync(cancellationToken);

        var games = await LoadGamesAsync(
            connection,
            cancellationToken);

        var installations = await LoadInstallationsAsync(
            connection,
            cancellationToken);

        return new LibrarySnapshot(
            games,
            installations);
    }

    private static async Task EnableForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<GameId> GetOrCreateGameAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DiscoveredInstallation discovered,
        CancellationToken cancellationToken)
    {
        var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = """
            SELECT game_id
            FROM provider_game_refs
            WHERE provider = $provider
              AND external_id = $externalId;
            """;
        lookup.Parameters.AddWithValue(
            "$provider",
            (int)discovered.Provider);
        lookup.Parameters.AddWithValue(
            "$externalId",
            discovered.ExternalId);

        var existingId = Convert.ToString(
            await lookup.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            var gameId = new GameId(Guid.Parse(existingId));

            await UpdateGameTitleAsync(
                connection,
                transaction,
                gameId,
                discovered.Title,
                discovered.ObservedAtUtc,
                cancellationToken);

            return gameId;
        }

        var newGameId = GameId.New();
        var observed = FormatUtc(discovered.ObservedAtUtc);

        var insertGame = connection.CreateCommand();
        insertGame.Transaction = transaction;
        insertGame.CommandText = """
            INSERT INTO games(
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                $title,
                0,
                $createdUtc,
                $updatedUtc);
            """;
        insertGame.Parameters.AddWithValue(
            "$gameId",
            newGameId.ToString());
        insertGame.Parameters.AddWithValue(
            "$title",
            discovered.Title);
        insertGame.Parameters.AddWithValue(
            "$createdUtc",
            observed);
        insertGame.Parameters.AddWithValue(
            "$updatedUtc",
            observed);

        await insertGame.ExecuteNonQueryAsync(
            cancellationToken);

        var insertReference = connection.CreateCommand();
        insertReference.Transaction = transaction;
        insertReference.CommandText = """
            INSERT INTO provider_game_refs(
                provider,
                external_id,
                game_id)
            VALUES(
                $provider,
                $externalId,
                $gameId);
            """;
        insertReference.Parameters.AddWithValue(
            "$provider",
            (int)discovered.Provider);
        insertReference.Parameters.AddWithValue(
            "$externalId",
            discovered.ExternalId);
        insertReference.Parameters.AddWithValue(
            "$gameId",
            newGameId.ToString());

        await insertReference.ExecuteNonQueryAsync(
            cancellationToken);

        return newGameId;
    }

    private static async Task UpdateGameTitleAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        GameId gameId,
        string title,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE games
            SET title = $title,
                updated_utc = $updatedUtc
            WHERE game_id = $gameId
              AND title <> $title;
            """;
        command.Parameters.AddWithValue(
            "$title",
            title);
        command.Parameters.AddWithValue(
            "$updatedUtc",
            FormatUtc(observedAtUtc));
        command.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task UpsertInstallationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        GameId gameId,
        DiscoveredInstallation discovered,
        CancellationToken cancellationToken)
    {
        var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = """
            SELECT installation_id
            FROM installations
            WHERE provider = $provider
              AND external_id = $externalId
              AND install_path = $installPath;
            """;
        lookup.Parameters.AddWithValue(
            "$provider",
            (int)discovered.Provider);
        lookup.Parameters.AddWithValue(
            "$externalId",
            discovered.ExternalId);
        lookup.Parameters.AddWithValue(
            "$installPath",
            discovered.InstallPath);

        var existingId = Convert.ToString(
            await lookup.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE installations
                SET installed_size_bytes = $size,
                    is_present = 1,
                    last_seen_utc = $lastSeenUtc
                WHERE installation_id = $installationId;
                """;
            update.Parameters.AddWithValue(
                "$size",
                (object?)discovered.InstalledSizeBytes ?? DBNull.Value);
            update.Parameters.AddWithValue(
                "$lastSeenUtc",
                FormatUtc(discovered.ObservedAtUtc));
            update.Parameters.AddWithValue(
                "$installationId",
                existingId);

            await update.ExecuteNonQueryAsync(
                cancellationToken);

            return;
        }

        var installationId = InstallationId.New();

        var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO installations(
                installation_id,
                game_id,
                provider,
                external_id,
                install_path,
                installed_size_bytes,
                is_preferred,
                is_present,
                last_seen_utc)
            VALUES(
                $installationId,
                $gameId,
                $provider,
                $externalId,
                $installPath,
                $size,
                0,
                1,
                $lastSeenUtc);
            """;
        insert.Parameters.AddWithValue(
            "$installationId",
            installationId.ToString());
        insert.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());
        insert.Parameters.AddWithValue(
            "$provider",
            (int)discovered.Provider);
        insert.Parameters.AddWithValue(
            "$externalId",
            discovered.ExternalId);
        insert.Parameters.AddWithValue(
            "$installPath",
            discovered.InstallPath);
        insert.Parameters.AddWithValue(
            "$size",
            (object?)discovered.InstalledSizeBytes ?? DBNull.Value);
        insert.Parameters.AddWithValue(
            "$lastSeenUtc",
            FormatUtc(discovered.ObservedAtUtc));

        await insert.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task MarkMissingInstallationsAbsentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProviderKind provider,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE installations
            SET is_present = 0
            WHERE provider = $provider
              AND last_seen_utc < $observedAtUtc;
            """;
        command.Parameters.AddWithValue(
            "$provider",
            (int)provider);
        command.Parameters.AddWithValue(
            "$observedAtUtc",
            FormatUtc(observedAtUtc));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task<IReadOnlyList<LogicalGame>> LoadGamesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc
            FROM games
            ORDER BY title COLLATE NOCASE, game_id;
            """;

        var result = new List<LogicalGame>();

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new LogicalGame(
                    new GameId(Guid.Parse(reader.GetString(0))),
                    reader.GetString(1),
                    reader.GetInt64(2) != 0,
                    ParseUtc(reader.GetString(3)),
                    ParseUtc(reader.GetString(4))));
        }

        return result;
    }

    private static async Task<IReadOnlyList<GameInstallation>> LoadInstallationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                installation_id,
                game_id,
                provider,
                external_id,
                install_path,
                installed_size_bytes,
                is_preferred,
                is_present,
                last_seen_utc
            FROM installations
            ORDER BY game_id, installation_id;
            """;

        var result = new List<GameInstallation>();

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new GameInstallation(
                    new InstallationId(
                        Guid.Parse(reader.GetString(0))),
                    new GameId(
                        Guid.Parse(reader.GetString(1))),
                    (ProviderKind)reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5)
                        ? null
                        : reader.GetInt64(5),
                    reader.GetInt64(6) != 0,
                    reader.GetInt64(7) != 0,
                    ParseUtc(reader.GetString(8))));
        }

        return result;
    }

    private static string FormatUtc(
        DateTimeOffset value) =>
        value.ToUniversalTime().ToString(
            "O",
            CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseUtc(
        string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
}
