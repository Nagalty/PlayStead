using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalEnvironment;

public sealed class SqliteLocalEnvironmentStore(DatabaseOptions options) : ILocalEnvironmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<LocalEnvironmentEntry>> GetEntriesAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT entry_id, installation_id, kind, path, path_kind, confidence, source, exists_flag, last_observed_utc, last_modified_utc, metadata_json, is_user_override FROM local_environment_entries WHERE installation_id=$installation ORDER BY path;";
        command.Parameters.AddWithValue("$installation", installationId.Value.ToString());
        return await ReadAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceDetectedAsync(InstallationId installationId, IReadOnlyCollection<LocalEnvironmentEntry> entries, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var delete = connection.CreateCommand();
        delete.Transaction = (SqliteTransaction)transaction;
        delete.CommandText = "DELETE FROM local_environment_entries WHERE installation_id=$installation AND is_user_override=0;";
        delete.Parameters.AddWithValue("$installation", installationId.Value.ToString());
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            await InsertAsync(connection, (SqliteTransaction)transaction, entry with { InstallationId = installationId, IsUserOverride = false }, cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertUserOverrideAsync(LocalEnvironmentEntry entry, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var delete = connection.CreateCommand();
        delete.Transaction = (SqliteTransaction)transaction;
        delete.CommandText = "DELETE FROM local_environment_entries WHERE installation_id=$installation AND kind=$kind AND is_user_override=1;";
        delete.Parameters.AddWithValue("$installation", entry.InstallationId.Value.ToString());
        delete.Parameters.AddWithValue("$kind", (int)entry.Kind);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await InsertAsync(connection, (SqliteTransaction)transaction, entry with { IsUserOverride = true, Source = LocalEnvironmentSource.User }, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task InsertAsync(SqliteConnection connection, SqliteTransaction transaction, LocalEnvironmentEntry entry, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO local_environment_entries(entry_id, installation_id, kind, path, path_kind, confidence, source, exists_flag, last_observed_utc, last_modified_utc, metadata_json, is_user_override) VALUES($id,$installation,$kind,$path,$pathKind,$confidence,$source,$exists,$observed,$modified,$metadata,$override);";
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$installation", entry.InstallationId.Value.ToString());
        command.Parameters.AddWithValue("$kind", (int)entry.Kind);
        command.Parameters.AddWithValue("$path", entry.Path);
        command.Parameters.AddWithValue("$pathKind", (int)entry.PathKind);
        command.Parameters.AddWithValue("$confidence", (int)entry.Confidence);
        command.Parameters.AddWithValue("$source", (int)entry.Source);
        command.Parameters.AddWithValue("$exists", entry.Exists ? 1 : 0);
        command.Parameters.AddWithValue("$observed", entry.LastObservedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$modified", (object?)entry.LastModifiedUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$metadata", entry.Metadata is null ? DBNull.Value : JsonSerializer.Serialize(entry.Metadata, JsonOptions));
        command.Parameters.AddWithValue("$override", entry.IsUserOverride ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveUserOverrideAsync(
        InstallationId installationId,
        LocalEnvironmentEntryKind kind,
        string path,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM local_environment_entries
            WHERE installation_id = $installation_id
              AND kind = $kind
              AND path = $path
              AND is_user_override = 1;
            """;
        command.Parameters.AddWithValue("$installation_id", installationId.Value);
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$path", path);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<LocalEnvironmentEntry>> ReadAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<LocalEnvironmentEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            IReadOnlyDictionary<string, string>? metadata = reader.IsDBNull(10) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(10), JsonOptions);
            result.Add(new LocalEnvironmentEntry(new Guid(reader.GetString(0)), new InstallationId(new Guid(reader.GetString(1))), (LocalEnvironmentEntryKind)reader.GetInt32(2), reader.GetString(3), (LocalEnvironmentPathKind)reader.GetInt32(4), (LocalEnvironmentConfidence)reader.GetInt32(5), (LocalEnvironmentSource)reader.GetInt32(6), reader.GetInt32(7) != 0, DateTimeOffset.Parse(reader.GetString(8)), reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)), metadata, reader.GetInt32(11) != 0));
        }
        return result;
    }
}
