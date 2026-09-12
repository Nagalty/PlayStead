using Microsoft.Data.Sqlite;

namespace PlayStead.Data.Database;

public sealed record DatabaseHealthResult(
    bool IsHealthy,
    string Detail);

public sealed class DatabaseHealthChecker
{
    private readonly DatabaseOptions _options;

    public DatabaseHealthChecker(DatabaseOptions options)
    {
        _options = options;
    }

    public async Task<DatabaseHealthResult> QuickCheckAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";

        var detail =
            Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))
            ?? "unknown";

        return new DatabaseHealthResult(
            string.Equals(detail, "ok", StringComparison.OrdinalIgnoreCase),
            detail);
    }
}
