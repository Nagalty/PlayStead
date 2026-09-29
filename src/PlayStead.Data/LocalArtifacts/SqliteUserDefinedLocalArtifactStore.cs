using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalArtifacts;

public sealed class SqliteUserDefinedLocalArtifactStore(DatabaseOptions options) : IUserDefinedLocalArtifactStore
{
    public async Task<IReadOnlyList<UserDefinedLocalArtifact>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT artifact_id, artifact_kind, path, display_name, created_at_utc FROM user_defined_local_artifacts WHERE game_id=$game ORDER BY created_at_utc DESC;";
        command.Parameters.AddWithValue("$game", gameId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<UserDefinedLocalArtifact>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UserDefinedLocalArtifact(
                Guid.Parse(reader.GetString(0)), gameId, (GameLocalArtifactKind)reader.GetInt32(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
        }
        return result;
    }

    public async Task AddAsync(UserDefinedLocalArtifact artifact, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO user_defined_local_artifacts(artifact_id,game_id,artifact_kind,path,display_name,created_at_utc) VALUES($id,$game,$kind,$path,$name,$created);";
        command.Parameters.AddWithValue("$id", artifact.Id.ToString("D"));
        command.Parameters.AddWithValue("$game", artifact.GameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)artifact.Kind);
        command.Parameters.AddWithValue("$path", artifact.Path);
        command.Parameters.AddWithValue("$name", (object?)artifact.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", artifact.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM user_defined_local_artifacts WHERE artifact_id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
