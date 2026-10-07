using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.Identity;

public sealed class SqliteProviderIdentityStore : IProviderIdentityStore
{
    private readonly DatabaseOptions _options;

    public SqliteProviderIdentityStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<IReadOnlyList<GameProviderIdentity>> GetByGameIdAsync(
        GameId gameId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider, external_id, source, confidence, created_utc, updated_utc
            FROM provider_game_refs
            WHERE game_id = $gameId
            ORDER BY provider, external_id;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString());

        var result = new List<GameProviderIdentity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new GameProviderIdentity(
                gameId,
                (ProviderKind)reader.GetInt32(0),
                reader.GetString(1),
                (ProviderIdentitySource)reader.GetInt32(2),
                (CatalogConfidence)reader.GetInt32(3),
                ParseUtc(reader.GetString(4)),
                ParseUtc(reader.GetString(5))));
        }

        return result;
    }

    public async Task AssociateAsync(
        GameProviderIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await EnableForeignKeysAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = """
            SELECT game_id, source
            FROM provider_game_refs
            WHERE provider = $provider AND external_id = $externalId;
            """;
        existing.Parameters.AddWithValue("$provider", (int)identity.Provider);
        existing.Parameters.AddWithValue("$externalId", identity.ExternalId);
        string? existingGame = null;
        var existingSource = (ProviderIdentitySource?)null;
        await using (var existingReader = await existing.ExecuteReaderAsync(cancellationToken))
        {
            if (await existingReader.ReadAsync(cancellationToken))
            {
                existingGame = existingReader.GetString(0);
                existingSource = (ProviderIdentitySource)existingReader.GetInt32(1);
            }
        }

        if (!string.IsNullOrWhiteSpace(existingGame) &&
            !string.Equals(existingGame, identity.GameId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provider identity {identity.Provider}:{identity.ExternalId} is already linked to GameId {existingGame}.");
        }

        var now = identity.UpdatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(existingGame))
        {
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO provider_game_refs(
                    provider, external_id, game_id, source, confidence, created_utc, updated_utc)
                VALUES($provider, $externalId, $gameId, $source, $confidence, $created, $updated);
                """;
            insert.Parameters.AddWithValue("$provider", (int)identity.Provider);
            insert.Parameters.AddWithValue("$externalId", identity.ExternalId);
            insert.Parameters.AddWithValue("$gameId", identity.GameId.ToString());
            insert.Parameters.AddWithValue("$source", (int)identity.Source);
            insert.Parameters.AddWithValue("$confidence", (int)identity.Confidence);
            insert.Parameters.AddWithValue("$created", identity.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$updated", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE provider_game_refs
                SET source = $source, confidence = $confidence, updated_utc = $updated
                WHERE provider = $provider AND external_id = $externalId;
                """;
            update.Parameters.AddWithValue("$provider", (int)identity.Provider);
            update.Parameters.AddWithValue("$externalId", identity.ExternalId);
            update.Parameters.AddWithValue(
                "$source",
                (int)(existingSource == ProviderIdentitySource.UserConfirmed
                    ? ProviderIdentitySource.UserConfirmed
                    : identity.Source));
            update.Parameters.AddWithValue("$confidence", (int)identity.Confidence);
            update.Parameters.AddWithValue("$updated", now);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        GameId gameId,
        ProviderKind provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM provider_game_refs
            WHERE game_id = $gameId AND provider = $provider AND external_id = $externalId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        command.Parameters.AddWithValue("$provider", (int)provider);
        command.Parameters.AddWithValue("$externalId", externalId.Trim());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task EnableForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
