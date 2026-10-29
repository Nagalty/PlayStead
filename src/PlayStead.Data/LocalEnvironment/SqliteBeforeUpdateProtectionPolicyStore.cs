using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;

namespace PlayStead.Data.LocalEnvironment;

public sealed class SqliteBeforeUpdateProtectionPolicyStore(DatabaseOptions options) : IBeforeUpdateProtectionPolicyStore
{
    public async Task<BeforeUpdateProtectionPolicy> GetAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT policy FROM local_environment_update_policies WHERE installation_id=$id;";
        command.Parameters.AddWithValue("$id", installationId.Value.ToString("D"));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? BeforeUpdateProtectionPolicy.Ask : (BeforeUpdateProtectionPolicy)Convert.ToInt32(value);
    }

    public async Task SetAsync(InstallationId installationId, BeforeUpdateProtectionPolicy policy, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO local_environment_update_policies(installation_id,policy,updated_utc) VALUES($id,$policy,$updated) ON CONFLICT(installation_id) DO UPDATE SET policy=excluded.policy, updated_utc=excluded.updated_utc;";
        command.Parameters.AddWithValue("$id", installationId.Value.ToString("D"));
        command.Parameters.AddWithValue("$policy", (int)policy);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
