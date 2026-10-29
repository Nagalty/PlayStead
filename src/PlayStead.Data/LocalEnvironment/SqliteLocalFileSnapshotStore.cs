using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalEnvironment;

public sealed class SqliteLocalFileSnapshotStore(DatabaseOptions options) : ILocalFileSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LocalFileSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE snapshot_id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<LocalFileSnapshot>> GetAsync(InstallationId installationId, CancellationToken cancellationToken)
        => await QueryAsync(" WHERE installation_id=$installation ORDER BY created_utc DESC;", "$installation", installationId.Value.ToString("D"), cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LocalFileSnapshot>> GetForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken)
        => await QueryAsync(" WHERE environment_entry_id=$entry ORDER BY created_utc DESC;", "$entry", environmentEntryId.ToString("D"), cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(LocalFileSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO local_file_snapshots(snapshot_id,installation_id,environment_entry_id,original_path,created_utc,reason,source_last_modified_utc,source_size_bytes,sha256,snapshot_relative_path,is_pre_restore_backup,metadata_json) VALUES($id,$installation,$entry,$path,$created,$reason,$modified,$size,$sha,$relative,$pre,$metadata);";
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
        command.Parameters.AddWithValue("$installation", snapshot.InstallationId.Value.ToString("D"));
        command.Parameters.AddWithValue("$entry", snapshot.EnvironmentEntryId.ToString("D"));
        command.Parameters.AddWithValue("$path", snapshot.OriginalPath);
        command.Parameters.AddWithValue("$created", snapshot.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$reason", (int)snapshot.Reason);
        command.Parameters.AddWithValue("$modified", (object?)snapshot.SourceLastModifiedUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$size", snapshot.SourceSizeBytes);
        command.Parameters.AddWithValue("$sha", snapshot.Sha256);
        command.Parameters.AddWithValue("$relative", snapshot.SnapshotRelativePath);
        command.Parameters.AddWithValue("$pre", snapshot.IsPreRestoreBackup ? 1 : 0);
        command.Parameters.AddWithValue("$metadata", snapshot.Metadata is null ? DBNull.Value : JsonSerializer.Serialize(snapshot.Metadata, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM local_file_snapshots WHERE snapshot_id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<LocalFileSnapshot>> QueryAsync(string suffix, string parameter, string value, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + suffix;
        command.Parameters.AddWithValue(parameter, value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<LocalFileSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    private static LocalFileSnapshot Read(SqliteDataReader reader)
    {
        var metadata = reader.IsDBNull(11) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(11), JsonOptions);
        return new LocalFileSnapshot(Guid.Parse(reader.GetString(0)), new InstallationId(Guid.Parse(reader.GetString(1))), Guid.Parse(reader.GetString(2)), reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture), (LocalFileSnapshotReason)reader.GetInt32(5), reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture), reader.GetInt64(7), reader.GetString(8), reader.GetString(9), reader.GetInt32(10) != 0, metadata);
    }

    private const string SelectSql = "SELECT snapshot_id,installation_id,environment_entry_id,original_path,created_utc,reason,source_last_modified_utc,source_size_bytes,sha256,snapshot_relative_path,is_pre_restore_backup,metadata_json FROM local_file_snapshots";

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
