using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalEnvironment;

public sealed class SqliteLocalEnvironmentBaselineStore(DatabaseOptions options) : ILocalEnvironmentBaselineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LocalEnvironmentBaseline?> GetAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var header = connection.CreateCommand();
        header.CommandText = "SELECT created_utc, updated_utc FROM local_environment_baselines WHERE installation_id=$id;";
        header.Parameters.AddWithValue("$id", installationId.Value.ToString());
        await using var reader = await header.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var created = DateTimeOffset.Parse(reader.GetString(0));
        var updated = DateTimeOffset.Parse(reader.GetString(1));
        await reader.DisposeAsync().ConfigureAwait(false);

        await using var entries = connection.CreateCommand();
        entries.CommandText = "SELECT kind, normalized_path, path_kind, exists_flag, last_modified_utc, metadata_json FROM local_environment_baseline_entries WHERE installation_id=$id ORDER BY normalized_path;";
        entries.Parameters.AddWithValue("$id", installationId.Value.ToString());
        await using var entryReader = await entries.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<LocalEnvironmentBaselineEntry>();
        while (await entryReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var metadata = entryReader.IsDBNull(5) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(entryReader.GetString(5), JsonOptions);
            result.Add(new LocalEnvironmentBaselineEntry((LocalEnvironmentEntryKind)entryReader.GetInt32(0), entryReader.GetString(1), (LocalEnvironmentPathKind)entryReader.GetInt32(2), entryReader.GetInt32(3) != 0, entryReader.IsDBNull(4) ? null : DateTimeOffset.Parse(entryReader.GetString(4)), metadata));
        }
        return new LocalEnvironmentBaseline(installationId, created, updated, result);
    }

    public async Task ReplaceAsync(LocalEnvironmentBaseline baseline, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var deleteEntries = connection.CreateCommand();
        deleteEntries.Transaction = (SqliteTransaction)transaction;
        deleteEntries.CommandText = "DELETE FROM local_environment_baseline_entries WHERE installation_id=$id;";
        deleteEntries.Parameters.AddWithValue("$id", baseline.InstallationId.Value.ToString());
        await deleteEntries.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var deleteHeader = connection.CreateCommand();
        deleteHeader.Transaction = (SqliteTransaction)transaction;
        deleteHeader.CommandText = "DELETE FROM local_environment_baselines WHERE installation_id=$id;";
        deleteHeader.Parameters.AddWithValue("$id", baseline.InstallationId.Value.ToString());
        await deleteHeader.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var header = connection.CreateCommand();
        header.Transaction = (SqliteTransaction)transaction;
        header.CommandText = "INSERT INTO local_environment_baselines(installation_id,created_utc,updated_utc) VALUES($id,$created,$updated);";
        header.Parameters.AddWithValue("$id", baseline.InstallationId.Value.ToString());
        header.Parameters.AddWithValue("$created", baseline.CreatedAtUtc.ToString("O"));
        header.Parameters.AddWithValue("$updated", baseline.UpdatedAtUtc.ToString("O"));
        await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entry in baseline.Entries)
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "INSERT INTO local_environment_baseline_entries(installation_id,kind,normalized_path,component_kind,component_name,path_kind,exists_flag,last_modified_utc,metadata_json) VALUES($id,$kind,$path,$componentKind,$componentName,$pathKind,$exists,$modified,$metadata);";
            command.Parameters.AddWithValue("$id", baseline.InstallationId.Value.ToString());
            command.Parameters.AddWithValue("$kind", (int)entry.Kind);
            command.Parameters.AddWithValue("$path", entry.NormalizedPath);
            command.Parameters.AddWithValue("$componentKind", entry.Metadata?.GetValueOrDefault("ComponentKind") ?? string.Empty);
            command.Parameters.AddWithValue("$componentName", entry.Metadata?.GetValueOrDefault("ComponentName") ?? string.Empty);
            command.Parameters.AddWithValue("$pathKind", (int)entry.PathKind);
            command.Parameters.AddWithValue("$exists", entry.Exists ? 1 : 0);
            command.Parameters.AddWithValue("$modified", (object?)entry.LastModifiedUtc?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$metadata", entry.Metadata is null ? DBNull.Value : JsonSerializer.Serialize(entry.Metadata, JsonOptions));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
