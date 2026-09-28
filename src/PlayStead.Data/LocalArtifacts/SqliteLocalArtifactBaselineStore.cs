using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalArtifacts;

public sealed class SqliteLocalArtifactBaselineStore(DatabaseOptions options) : ILocalArtifactBaselineStore
{
    public async Task<LocalArtifactBaseline?> GetAsync(GameId gameId, GameLocalArtifactKind kind, string artifactIdentity, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT algorithm, hash, file_count, total_size_bytes, captured_at_utc FROM local_artifact_baselines WHERE game_id=$game_id AND artifact_kind=$kind AND artifact_identity=$identity;";
        command.Parameters.AddWithValue("$game_id", gameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$identity", artifactIdentity);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LocalArtifactBaseline(gameId, kind, artifactIdentity, reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));
    }

    public async Task UpsertAsync(LocalArtifactBaseline baseline, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO local_artifact_baselines(game_id,artifact_kind,artifact_identity,algorithm,hash,file_count,total_size_bytes,captured_at_utc) VALUES($game_id,$kind,$identity,$algorithm,$hash,$count,$size,$captured) ON CONFLICT(game_id,artifact_kind,artifact_identity) DO UPDATE SET algorithm=excluded.algorithm, hash=excluded.hash, file_count=excluded.file_count, total_size_bytes=excluded.total_size_bytes, captured_at_utc=excluded.captured_at_utc;";
        command.Parameters.AddWithValue("$game_id", baseline.GameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)baseline.Kind);
        command.Parameters.AddWithValue("$identity", baseline.ArtifactIdentity);
        command.Parameters.AddWithValue("$algorithm", baseline.Algorithm);
        command.Parameters.AddWithValue("$hash", baseline.Hash);
        command.Parameters.AddWithValue("$count", baseline.FileCount);
        command.Parameters.AddWithValue("$size", baseline.TotalSizeBytes);
        command.Parameters.AddWithValue("$captured", baseline.CapturedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
