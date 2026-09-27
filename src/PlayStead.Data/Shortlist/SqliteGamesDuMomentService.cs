using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Shortlist;
using PlayStead.Data.Database;

namespace PlayStead.Data.Shortlist;

public sealed class SqliteGamesDuMomentService : IGamesDuMomentService
{
    private const int MaxEntries = 5;
    private readonly DatabaseOptions _options;

    public SqliteGamesDuMomentService(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<IReadOnlyList<GamesDuMomentEntry>> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, position, added_at_utc FROM games_du_moment ORDER BY position;";
        var result = new List<GamesDuMomentEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new GamesDuMomentEntry(new GameId(Guid.Parse(reader.GetString(0))), reader.GetInt32(1), DateTimeOffset.Parse(reader.GetString(2))));
        }
        return result;
    }

    public async Task<GamesDuMomentAddResult> AddAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = connection.CreateCommand(); existing.Transaction = (SqliteTransaction)transaction;
        existing.CommandText = "SELECT COUNT(*) FROM games_du_moment WHERE game_id=$id;"; existing.Parameters.AddWithValue("$id", gameId.ToString());
        if (Convert.ToInt32(await existing.ExecuteScalarAsync(cancellationToken)) > 0) { await transaction.CommitAsync(cancellationToken); return GamesDuMomentAddResult.AlreadyMember; }
        var count = connection.CreateCommand(); count.Transaction = (SqliteTransaction)transaction; count.CommandText = "SELECT COUNT(*) FROM games_du_moment;";
        if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) >= MaxEntries) { await transaction.CommitAsync(cancellationToken); return GamesDuMomentAddResult.Full; }
        var insert = connection.CreateCommand(); insert.Transaction = (SqliteTransaction)transaction; insert.CommandText = "INSERT OR IGNORE INTO games_du_moment(game_id,position,added_at_utc) VALUES($id,$position,$added);";
        insert.Parameters.AddWithValue("$id", gameId.ToString()); insert.Parameters.AddWithValue("$position", Convert.ToInt32(await NextPositionAsync(connection, (SqliteTransaction)transaction, cancellationToken))); insert.Parameters.AddWithValue("$added", DateTimeOffset.UtcNow.ToString("O"));
        var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return inserted > 0 ? GamesDuMomentAddResult.Added : GamesDuMomentAddResult.AlreadyMember;
    }

    public async Task<bool> RemoveAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var position = connection.CreateCommand(); position.Transaction = (SqliteTransaction)transaction; position.CommandText = "SELECT position FROM games_du_moment WHERE game_id=$id;"; position.Parameters.AddWithValue("$id", gameId.ToString());
        var positionValue = await position.ExecuteScalarAsync(cancellationToken);
        var delete = connection.CreateCommand(); delete.Transaction = (SqliteTransaction)transaction; delete.CommandText = "DELETE FROM games_du_moment WHERE game_id=$id;"; delete.Parameters.AddWithValue("$id", gameId.ToString());
        var removed = await delete.ExecuteNonQueryAsync(cancellationToken);
        if (removed > 0) { var compact = connection.CreateCommand(); compact.Transaction = (SqliteTransaction)transaction; compact.CommandText = "UPDATE games_du_moment SET position=position-1 WHERE position > $position;"; compact.Parameters.AddWithValue("$position", Convert.ToInt32(positionValue)); await compact.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken); return removed > 0;
    }

    public async Task ReorderAsync(IReadOnlyList<GameId> orderedGameIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedGameIds); if (orderedGameIds.Count > MaxEntries || orderedGameIds.Distinct().Count() != orderedGameIds.Count) throw new ArgumentException("Order must contain unique shortlist IDs.", nameof(orderedGameIds));
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = connection.CreateCommand(); current.Transaction = (SqliteTransaction)transaction; current.CommandText = "SELECT game_id FROM games_du_moment ORDER BY position;"; var ids = new List<GameId>(); await using var reader = await current.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) ids.Add(new GameId(Guid.Parse(reader.GetString(0))));
        await reader.DisposeAsync();
        if (!ids.OrderBy(x=>x.Value).SequenceEqual(orderedGameIds.OrderBy(x=>x.Value))) throw new ArgumentException("Order must contain the current shortlist IDs.", nameof(orderedGameIds));
        var clear = connection.CreateCommand(); clear.Transaction = (SqliteTransaction)transaction; clear.CommandText = "UPDATE games_du_moment SET position = -position - 1;"; await clear.ExecuteNonQueryAsync(cancellationToken);
        for (var i=0;i<orderedGameIds.Count;i++) { var update=connection.CreateCommand(); update.Transaction=(SqliteTransaction)transaction; update.CommandText="UPDATE games_du_moment SET position=$position WHERE game_id=$id;"; update.Parameters.AddWithValue("$position", i); update.Parameters.AddWithValue("$id", orderedGameIds[i].ToString()); await update.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<long> NextPositionAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    { var command=connection.CreateCommand(); command.Transaction=transaction; command.CommandText="SELECT COALESCE(MAX(position)+1,0) FROM games_du_moment;"; return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)); }
    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken) { var connection=new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False"); await connection.OpenAsync(cancellationToken); return connection; }
}
