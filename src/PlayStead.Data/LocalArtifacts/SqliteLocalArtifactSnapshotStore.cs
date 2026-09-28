using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalArtifacts;

public sealed class SqliteLocalArtifactSnapshotStore(DatabaseOptions options) : ILocalArtifactSnapshotStore
{
    public async Task<IReadOnlyList<LocalArtifactSnapshot>> GetAsync(GameId gameId, GameLocalArtifactKind kind, string ruleIdentity, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT snapshot_id, archive_path, fingerprint_algorithm, fingerprint_hash, file_count, total_size_bytes, created_at_utc, reason FROM local_artifact_snapshots WHERE game_id=$game_id AND artifact_kind=$kind AND rule_identity=$rule_identity ORDER BY created_at_utc DESC;";
        command.Parameters.AddWithValue("$game_id", gameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$rule_identity", ruleIdentity);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LocalArtifactSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new LocalArtifactSnapshot(Guid.Parse(reader.GetString(0)), gameId, kind, ruleIdentity, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt64(5), DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture), true, (SnapshotReason)reader.GetInt32(7)));
        return result;
    }

    public async Task UpsertAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO local_artifact_snapshots(snapshot_id,game_id,artifact_kind,rule_identity,archive_path,fingerprint_algorithm,fingerprint_hash,file_count,total_size_bytes,created_at_utc,reason) VALUES($id,$game,$kind,$rule,$path,$algorithm,$hash,$count,$size,$created,$reason);";
        command.Parameters.AddWithValue("$id", snapshot.SnapshotId.ToString("D"));
        command.Parameters.AddWithValue("$game", snapshot.GameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)snapshot.ArtifactKind);
        command.Parameters.AddWithValue("$rule", snapshot.RuleIdentity);
        command.Parameters.AddWithValue("$path", snapshot.ArchivePath);
        command.Parameters.AddWithValue("$algorithm", snapshot.FingerprintAlgorithm);
        command.Parameters.AddWithValue("$hash", snapshot.FingerprintHash);
        command.Parameters.AddWithValue("$count", snapshot.FileCount);
        command.Parameters.AddWithValue("$size", snapshot.TotalSizeBytes);
        command.Parameters.AddWithValue("$created", snapshot.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$reason", (int)snapshot.Reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM local_artifact_snapshots WHERE snapshot_id=$id;";
        command.Parameters.AddWithValue("$id", snapshotId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
