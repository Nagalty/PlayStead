using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;

namespace PlayStead.Data.Library;

public sealed class SqliteLibraryStore : ILibraryStore, IManualGameStore
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
                    last_seen_utc = $lastSeenUtc,
                    executable_path = $executablePath,
                    working_directory = $workingDirectory,
                    launch_arguments = $launchArguments
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
            update.Parameters.AddWithValue(
                "$executablePath",
                (object?)discovered.ExecutablePath ?? DBNull.Value);
            update.Parameters.AddWithValue(
                "$workingDirectory",
                (object?)discovered.WorkingDirectory ?? DBNull.Value);
            update.Parameters.AddWithValue(
                "$launchArguments",
                (object?)discovered.LaunchArguments ?? DBNull.Value);

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
                last_seen_utc,
                executable_path,
                working_directory,
                launch_arguments)
            VALUES(
                $installationId,
                $gameId,
                $provider,
                $externalId,
                $installPath,
                $size,
                0,
                1,
                $lastSeenUtc,
                $executablePath,
                $workingDirectory,
                $launchArguments);
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
        insert.Parameters.AddWithValue(
            "$executablePath",
            (object?)discovered.ExecutablePath ?? DBNull.Value);
        insert.Parameters.AddWithValue(
            "$workingDirectory",
            (object?)discovered.WorkingDirectory ?? DBNull.Value);
        insert.Parameters.AddWithValue(
            "$launchArguments",
            (object?)discovered.LaunchArguments ?? DBNull.Value);

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
                updated_utc,
                canonical_content_id
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
                    ParseUtc(reader.GetString(4)),
                    reader.IsDBNull(5)
                        ? null
                        : new CatalogContentId(
                            Guid.Parse(reader.GetString(5)))));
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
                last_seen_utc,
                executable_path,
                working_directory,
                launch_arguments
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
                    ParseUtc(reader.GetString(8)),
                    InstallationContentKind.Unknown,
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11)));
        }

        return result;
    }

    public async Task<GameInstallation> CreateAsync(
        ManualGameDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var now = DateTimeOffset.UtcNow;
        var gameId = GameId.New();
        var externalId = $"manual:{Guid.NewGuid():D}";
        var installationId = InstallationId.New();
        var stamp = FormatUtc(now);

        await using var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await EnableForeignKeysAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var game = connection.CreateCommand();
        game.Transaction = transaction;
        game.CommandText = """
            INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc)
            VALUES($gameId,$title,0,$created,$updated);
            """;
        game.Parameters.AddWithValue("$gameId", gameId.ToString());
        game.Parameters.AddWithValue("$title", definition.Title);
        game.Parameters.AddWithValue("$created", stamp);
        game.Parameters.AddWithValue("$updated", stamp);
        await game.ExecuteNonQueryAsync(cancellationToken);

        var installation = connection.CreateCommand();
        installation.Transaction = transaction;
        installation.CommandText = """
            INSERT INTO installations(
                installation_id,game_id,provider,external_id,install_path,
                installed_size_bytes,is_preferred,is_present,last_seen_utc,
                executable_path,working_directory,launch_arguments)
            VALUES($installationId,$gameId,$provider,$externalId,$installPath,
                NULL,1,1,$lastSeen,$executablePath,$workingDirectory,$launchArguments);
            """;
        installation.Parameters.AddWithValue("$installationId", installationId.ToString());
        installation.Parameters.AddWithValue("$gameId", gameId.ToString());
        installation.Parameters.AddWithValue("$provider", (int)ProviderKind.Manual);
        installation.Parameters.AddWithValue("$externalId", externalId);
        installation.Parameters.AddWithValue("$installPath", definition.WorkingDirectory);
        installation.Parameters.AddWithValue("$lastSeen", stamp);
        installation.Parameters.AddWithValue("$executablePath", definition.ExecutablePath);
        installation.Parameters.AddWithValue("$workingDirectory", definition.WorkingDirectory);
        installation.Parameters.AddWithValue("$launchArguments", (object?)definition.LaunchArguments ?? DBNull.Value);
        await installation.ExecuteNonQueryAsync(cancellationToken);

        var reference = connection.CreateCommand();
        reference.Transaction = transaction;
        reference.CommandText = "INSERT INTO provider_game_refs(provider,external_id,game_id) VALUES($provider,$externalId,$gameId);";
        reference.Parameters.AddWithValue("$provider", (int)ProviderKind.Manual);
        reference.Parameters.AddWithValue("$externalId", externalId);
        reference.Parameters.AddWithValue("$gameId", gameId.ToString());
        await reference.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GameInstallation(installationId, gameId, ProviderKind.Manual, externalId,
            definition.WorkingDirectory, null, true, true, now, InstallationContentKind.Game,
            definition.ExecutablePath, definition.WorkingDirectory, definition.LaunchArguments);
    }

    public async Task<GameInstallation?> UpdateAsync(
        GameId gameId,
        ManualGameDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var now = DateTimeOffset.UtcNow;
        await using var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await EnableForeignKeysAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE games SET title=$title, updated_utc=$updated WHERE game_id=$gameId;
            UPDATE installations SET install_path=$workingDirectory, executable_path=$executablePath,
                working_directory=$workingDirectory, launch_arguments=$launchArguments,
                is_present=1, last_seen_utc=$updated
            WHERE game_id=$gameId AND provider=$provider;
            """;
        update.Parameters.AddWithValue("$title", definition.Title);
        update.Parameters.AddWithValue("$updated", FormatUtc(now));
        update.Parameters.AddWithValue("$gameId", gameId.ToString());
        update.Parameters.AddWithValue("$provider", (int)ProviderKind.Manual);
        update.Parameters.AddWithValue("$workingDirectory", definition.WorkingDirectory);
        update.Parameters.AddWithValue("$executablePath", definition.ExecutablePath);
        update.Parameters.AddWithValue("$launchArguments", (object?)definition.LaunchArguments ?? DBNull.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);

        var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = """
            SELECT installation_id, external_id, install_path, installed_size_bytes,
                   is_preferred, is_present, last_seen_utc, executable_path,
                   working_directory, launch_arguments
            FROM installations
            WHERE game_id=$gameId AND provider=$provider
            LIMIT 1;
            """;
        select.Parameters.AddWithValue("$gameId", gameId.ToString());
        select.Parameters.AddWithValue("$provider", (int)ProviderKind.Manual);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var installation = new GameInstallation(
            new InstallationId(Guid.Parse(reader.GetString(0))), gameId, ProviderKind.Manual,
            reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt64(3),
            reader.GetInt64(4) != 0, reader.GetInt64(5) != 0, ParseUtc(reader.GetString(6)),
            InstallationContentKind.Game, reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9));
        await transaction.CommitAsync(cancellationToken);
        return installation;
    }

    public async Task<bool> RemoveAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE installations SET is_present=0 WHERE game_id=$gameId AND provider=$provider;";
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        command.Parameters.AddWithValue("$provider", (int)ProviderKind.Manual);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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
