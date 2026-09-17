using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Data.Database;

namespace PlayStead.Data.Identity;

public sealed class SqliteIdentityDecisionService : IIdentityDecisionService
{
    private readonly DatabaseOptions _options;

    public SqliteIdentityDecisionService(DatabaseOptions options) { ArgumentNullException.ThrowIfNull(options); _options = options; }

    public Task<GameIdentityDecision> ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, DateTimeOffset decidedUtc, CancellationToken cancellationToken) =>
        ExecuteAsync(gameId, catalogContentId, IdentityDecisionType.UserConfirmed, decidedUtc, cancellationToken);

    public Task<GameIdentityDecision> RejectAsync(GameId gameId, CatalogContentId catalogContentId, DateTimeOffset decidedUtc, CancellationToken cancellationToken) =>
        ExecuteAsync(gameId, catalogContentId, IdentityDecisionType.UserRejected, decidedUtc, cancellationToken);

    public async Task<GameIdentityDecision?> RevokeConfirmedAsync(GameId gameId, DateTimeOffset revokedUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var existing = await ReadAsync(connection, transaction, gameId, 1, cancellationToken);
        await EnsureGameAsync(connection, transaction, gameId, cancellationToken);
        if (existing is null) { await transaction.CommitAsync(cancellationToken); return null; }
        await RevokeAsync(connection, transaction, existing.DecisionId, revokedUtc, cancellationToken);
        await SetCanonicalAsync(connection, transaction, gameId, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return existing with { UpdatedUtc = revokedUtc, RevokedUtc = revokedUtc };
    }

    private async Task<GameIdentityDecision> ExecuteAsync(GameId gameId, CatalogContentId contentId, IdentityDecisionType type, DateTimeOffset at, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await EnsureGameAsync(connection, transaction, gameId, cancellationToken);
        if (type == IdentityDecisionType.UserConfirmed)
        {
            var old = await ReadAsync(connection, transaction, gameId, 1, cancellationToken);
            if (old is not null) await RevokeAsync(connection, transaction, old.DecisionId, at, cancellationToken);
            var rejected = await ReadAsync(connection, transaction, gameId, 2, contentId, cancellationToken);
            if (rejected is not null) await RevokeAsync(connection, transaction, rejected.DecisionId, at, cancellationToken);
            await SetCanonicalAsync(connection, transaction, gameId, contentId, cancellationToken);
        }
        else
        {
            var confirmed = await ReadAsync(connection, transaction, gameId, 1, contentId, cancellationToken);
            if (confirmed is not null) { await RevokeAsync(connection, transaction, confirmed.DecisionId, at, cancellationToken); await SetCanonicalAsync(connection, transaction, gameId, null, cancellationToken); }
            var existingReject = await ReadAsync(connection, transaction, gameId, 2, contentId, cancellationToken);
            if (existingReject is not null) { await transaction.CommitAsync(cancellationToken); return existingReject; }
        }
        var decision = new GameIdentityDecision(IdentityDecisionId.New(), gameId, contentId, type, at, at, null);
        await InsertAsync(connection, transaction, decision, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return decision;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken token) { var c = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False"); await c.OpenAsync(token); return c; }
    private static async Task EnsureGameAsync(SqliteConnection c, SqliteTransaction t, GameId id, CancellationToken token) { using var cmd=c.CreateCommand(); cmd.Transaction=t; cmd.CommandText="SELECT COUNT(*) FROM games WHERE game_id=$g;"; cmd.Parameters.AddWithValue("$g", id.ToString()); if(Convert.ToInt32(await cmd.ExecuteScalarAsync(token))==0) throw new InvalidOperationException($"Game '{id}' does not exist."); }
    private static async Task SetCanonicalAsync(SqliteConnection c, SqliteTransaction t, GameId id, CatalogContentId? content, CancellationToken token) { using var cmd=c.CreateCommand(); cmd.Transaction=t; cmd.CommandText="UPDATE games SET canonical_content_id=$c WHERE game_id=$g;"; cmd.Parameters.AddWithValue("$g",id.ToString()); cmd.Parameters.AddWithValue("$c",content is { } x ? x.ToString() : DBNull.Value); await cmd.ExecuteNonQueryAsync(token); }
    private static async Task RevokeAsync(SqliteConnection c, SqliteTransaction t, IdentityDecisionId id, DateTimeOffset at, CancellationToken token) { using var cmd=c.CreateCommand(); cmd.Transaction=t; cmd.CommandText="UPDATE game_identity_decisions SET revoked_utc=$r, updated_utc=$u WHERE decision_id=$id;"; cmd.Parameters.AddWithValue("$id",id.ToString()); cmd.Parameters.AddWithValue("$r",at.ToString("O")); cmd.Parameters.AddWithValue("$u",at.ToString("O")); await cmd.ExecuteNonQueryAsync(token); }
    private static async Task InsertAsync(SqliteConnection c, SqliteTransaction t, GameIdentityDecision d, CancellationToken token) { using var cmd=c.CreateCommand(); cmd.Transaction=t; cmd.CommandText="INSERT INTO game_identity_decisions VALUES($id,$g,$c,$type,$created,$updated,NULL);"; cmd.Parameters.AddWithValue("$id",d.DecisionId.ToString()); cmd.Parameters.AddWithValue("$g",d.GameId.ToString()); cmd.Parameters.AddWithValue("$c",d.CatalogContentId.ToString()); cmd.Parameters.AddWithValue("$type",(int)d.DecisionType); cmd.Parameters.AddWithValue("$created",d.CreatedUtc.ToString("O")); cmd.Parameters.AddWithValue("$updated",d.UpdatedUtc.ToString("O")); await cmd.ExecuteNonQueryAsync(token); }
    private static Task<GameIdentityDecision?> ReadAsync(SqliteConnection c, SqliteTransaction t, GameId g, int type, CancellationToken token) => ReadAsync(c,t,g,type,null,token);
    private static async Task<GameIdentityDecision?> ReadAsync(SqliteConnection c, SqliteTransaction t, GameId g, int type, CatalogContentId? content, CancellationToken token) { using var cmd=c.CreateCommand(); cmd.Transaction=t; cmd.CommandText="SELECT decision_id,game_id,catalog_content_id,decision_type,created_utc,updated_utc,revoked_utc FROM game_identity_decisions WHERE game_id=$g AND decision_type=$type AND revoked_utc IS NULL"+(content is null?"":" AND catalog_content_id=$c")+" LIMIT 1;"; cmd.Parameters.AddWithValue("$g",g.ToString()); cmd.Parameters.AddWithValue("$type",type); if(content is { } x) cmd.Parameters.AddWithValue("$c",x.ToString()); await using var r=await cmd.ExecuteReaderAsync(token); if(!await r.ReadAsync(token)) return null; return new GameIdentityDecision(new IdentityDecisionId(Guid.Parse(r.GetString(0))),new GameId(Guid.Parse(r.GetString(1))),new CatalogContentId(Guid.Parse(r.GetString(2))),(IdentityDecisionType)r.GetInt32(3),DateTimeOffset.Parse(r.GetString(4)),DateTimeOffset.Parse(r.GetString(5)),r.IsDBNull(6)?null:DateTimeOffset.Parse(r.GetString(6))); }
}
