using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Data.Database;

namespace PlayStead.Data.Identity;

public sealed class SqliteLocalIdentityReconciler : ILocalIdentityReconciler
{
    private readonly DatabaseOptions _options;

    public SqliteLocalIdentityReconciler(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task ReconcileAsync(
        GameId localGameId,
        CatalogContentId canonicalContentId,
        IdentityResolutionEvidence evidence,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE games
            SET canonical_content_id = $canonicalContentId
            WHERE game_id = $gameId;
            """;
        update.Parameters.AddWithValue(
            "$canonicalContentId",
            canonicalContentId.ToString());
        update.Parameters.AddWithValue("$gameId", localGameId.ToString());

        var updated = await update.ExecuteNonQueryAsync(
            cancellationToken);

        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Local game '{localGameId}' was not found.");
        }

        var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO game_identity_resolutions(
                game_id,
                provisional_id,
                state,
                candidate_content_id,
                evidence_json,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                NULL,
                $state,
                $candidateContentId,
                $evidenceJson,
                $createdUtc,
                $updatedUtc)
            ON CONFLICT(game_id) DO UPDATE SET
                provisional_id =
                    game_identity_resolutions.provisional_id,
                state = $state,
                candidate_content_id = excluded.candidate_content_id,
                evidence_json = excluded.evidence_json,
                updated_utc = excluded.updated_utc;
            """;
        upsert.Parameters.AddWithValue("$gameId", localGameId.ToString());
        upsert.Parameters.AddWithValue(
            "$state",
            (int)IdentityResolutionState.MatchConfirmed);
        upsert.Parameters.AddWithValue(
            "$candidateContentId",
            canonicalContentId.ToString());
        upsert.Parameters.AddWithValue(
            "$evidenceJson",
            JsonSerializer.Serialize(evidence));
        upsert.Parameters.AddWithValue(
            "$createdUtc",
            observedAtUtc.ToString("O"));
        upsert.Parameters.AddWithValue(
            "$updatedUtc",
            observedAtUtc.ToString("O"));

        await upsert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
