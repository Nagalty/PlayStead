using Microsoft.Data.Sqlite;
using PlayStead.Core.Collections;
using PlayStead.Core.Library;
using PlayStead.Data.Database;

namespace PlayStead.Data.Collections;

public sealed class SqliteGameCollectionStore : IGameCollectionStore
{
    private readonly DatabaseOptions _options;

    public SqliteGameCollectionStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<IReadOnlyList<GameCollection>> GetCollectionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT collection_id, name, created_utc, updated_utc FROM game_collections ORDER BY name COLLATE NOCASE;";
        var result = new List<GameCollection>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadCollection(reader));
        return result;
    }

    public async Task<GameCollection> CreateCollectionAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        var now = DateTimeOffset.UtcNow;
        var collection = new GameCollection(Guid.NewGuid(), normalized, now, now);
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO game_collections(collection_id,name,created_utc,updated_utc) VALUES($id,$name,$created,$updated);";
        AddCollectionParameters(command, collection);
        try { await command.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { throw new InvalidOperationException("A collection with this name already exists.", ex); }
        return collection;
    }

    public async Task<GameCollection> RenameCollectionAsync(Guid collectionId, string name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE game_collections SET name=$name, updated_utc=$updated WHERE collection_id=$id RETURNING collection_id, name, created_utc, updated_utc;";
        command.Parameters.AddWithValue("$id", collectionId.ToString("D"));
        command.Parameters.AddWithValue("$name", normalized);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException($"Collection '{collectionId}' was not found.");
            return ReadCollection(reader);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("A collection with this name already exists.", ex);
        }
    }

    public async Task<bool> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM game_collections WHERE collection_id=$id;";
        command.Parameters.AddWithValue("$id", collectionId.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<GameCollectionMembership>> GetMembershipsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT collection_id, game_id FROM game_collection_memberships;";
        var result = new List<GameCollectionMembership>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new GameCollectionMembership(Guid.Parse(reader.GetString(0)), new GameId(Guid.Parse(reader.GetString(1)))));
        return result;
    }

    public async Task<IReadOnlySet<Guid>> GetMembershipsAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var memberships = await GetMembershipsAsync(cancellationToken);
        return memberships.Where(x => x.GameId == gameId).Select(x => x.CollectionId).ToHashSet();
    }

    public async Task SetMembershipAsync(Guid collectionId, GameId gameId, bool enabled, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = enabled
            ? "INSERT OR IGNORE INTO game_collection_memberships(collection_id,game_id) VALUES($collection,$game);"
            : "DELETE FROM game_collection_memberships WHERE collection_id=$collection AND game_id=$game;";
        command.Parameters.AddWithValue("$collection", collectionId.ToString("D"));
        command.Parameters.AddWithValue("$game", gameId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static GameCollection ReadCollection(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2)), DateTimeOffset.Parse(reader.GetString(3)));

    private static void AddCollectionParameters(SqliteCommand command, GameCollection collection)
    {
        command.Parameters.AddWithValue("$id", collection.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", collection.Name);
        command.Parameters.AddWithValue("$created", collection.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updated", collection.UpdatedAtUtc.ToString("O"));
    }

    private static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.Trim();
        if (normalized.Length is 0 or > 60)
            throw new ArgumentException("Collection name must contain between 1 and 60 characters.", nameof(name));
        return normalized;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
