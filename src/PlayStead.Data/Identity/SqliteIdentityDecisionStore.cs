using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.Identity;

public sealed class SqliteIdentityDecisionStore : IIdentityDecisionStore
{
    private readonly DatabaseOptions _options;

    public SqliteIdentityDecisionStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(gameId, "decision_type = $type AND revoked_utc IS NULL", 1, cancellationToken);
        return rows.SingleOrDefault();
    }

    public Task<IReadOnlyList<GameIdentityDecision>> ListActiveRejectedAsync(GameId gameId, CancellationToken cancellationToken) =>
        QueryAsync(gameId, "decision_type = $type AND revoked_utc IS NULL", 2, cancellationToken);

    public Task<IReadOnlyList<GameIdentityDecision>> ListActiveAsync(GameId gameId, CancellationToken cancellationToken) =>
        QueryAsync(gameId, "revoked_utc IS NULL", null, cancellationToken);

    public async Task InsertAsync(GameIdentityDecision decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(false, cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_identity_decisions(decision_id,game_id,catalog_content_id,decision_type,created_utc,updated_utc,revoked_utc)
            VALUES($id,$game,$content,$type,$created,$updated,$revoked);
            """;
        command.Parameters.AddWithValue("$id", decision.DecisionId.ToString());
        command.Parameters.AddWithValue("$game", decision.GameId.ToString());
        command.Parameters.AddWithValue("$content", decision.CatalogContentId.ToString());
        command.Parameters.AddWithValue("$type", (int)decision.DecisionType);
        command.Parameters.AddWithValue("$created", decision.CreatedUtc.ToString("O"));
        command.Parameters.AddWithValue("$updated", decision.UpdatedUtc.ToString("O"));
        command.Parameters.AddWithValue("$revoked", decision.RevokedUtc is { } revoked ? revoked.ToString("O") : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeAsync(IdentityDecisionId decisionId, DateTimeOffset revokedUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(false, cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE game_identity_decisions SET revoked_utc=$revoked, updated_utc=$updated WHERE decision_id=$id;";
        command.Parameters.AddWithValue("$id", decisionId.ToString());
        command.Parameters.AddWithValue("$revoked", revokedUtc.ToString("O"));
        command.Parameters.AddWithValue("$updated", revokedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<GameIdentityDecision>> QueryAsync(GameId gameId, string predicate, int? type, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(true, cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT decision_id,game_id,catalog_content_id,decision_type,created_utc,updated_utc,revoked_utc FROM game_identity_decisions WHERE game_id=$game AND {predicate} ORDER BY created_utc, decision_id;";
        command.Parameters.AddWithValue("$game", gameId.ToString());
        if (type is not null) command.Parameters.AddWithValue("$type", type.Value);
        var result = new List<GameIdentityDecision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(Read(reader));
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(bool readOnly, CancellationToken cancellationToken)
    {
        var mode = readOnly ? "Mode=ReadOnly;" : string.Empty;
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};{mode}Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static GameIdentityDecision Read(SqliteDataReader reader) => new(
        new IdentityDecisionId(Guid.Parse(reader.GetString(0))),
        new GameId(Guid.Parse(reader.GetString(1))),
        new CatalogContentId(Guid.Parse(reader.GetString(2))),
        (IdentityDecisionType)reader.GetInt32(3),
        DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
