using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.Notifications;

public sealed class SqliteNotificationStore : INotificationStore
{
    private readonly DatabaseOptions _options;

    public SqliteNotificationStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<NotificationRecord?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenReadOnlyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM notifications WHERE notification_id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<NotificationRecord?> GetByDeduplicationKeyAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenReadOnlyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM notifications WHERE deduplication_key = $key;";
        command.Parameters.AddWithValue("$key", deduplicationKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task InsertAsync(NotificationRecord notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO notifications(notification_id,producer,subject_id,reason,deduplication_key,priority,state,title,message,payload_json,created_utc,updated_utc,read_utc,resolved_utc)
            VALUES($id,$producer,$subject,$reason,$dedup,$priority,$state,$title,$message,$payload,$created,$updated,$read,$resolved);
            """;
        AddParameters(command, notification);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationRecord notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE notifications SET producer=$producer, subject_id=$subject, reason=$reason, deduplication_key=$dedup,
                priority=$priority, state=$state, title=$title, message=$message, payload_json=$payload,
                created_utc=$created, updated_utc=$updated, read_utc=$read, resolved_utc=$resolved
            WHERE notification_id=$id;
            """;
        AddParameters(command, notification);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<IReadOnlyList<NotificationRecord>> ListActiveAsync(CancellationToken cancellationToken) =>
        ListAsync("state IN (1,2) ORDER BY priority DESC, updated_utc DESC, notification_id", cancellationToken);

    public Task<IReadOnlyList<NotificationRecord>> ListResolvedAsync(CancellationToken cancellationToken) =>
        ListAsync("state = 3 ORDER BY updated_utc DESC, notification_id", cancellationToken);

    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenReadOnlyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM notifications WHERE state IN (1,2);";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public async Task<int> DeleteResolvedOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM notifications WHERE state = 3 AND resolved_utc < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoffUtc.ToString("O", CultureInfo.InvariantCulture));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<NotificationRecord>> ListAsync(string suffix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenReadOnlyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM notifications WHERE " + suffix + ";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<NotificationRecord>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(Read(reader));
        return results;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(token);
        return connection;
    }

    private async Task<SqliteConnection> OpenReadOnlyAsync(CancellationToken token)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(token);
        return connection;
    }

    private static void AddParameters(SqliteCommand command, NotificationRecord n)
    {
        command.Parameters.AddWithValue("$id", n.NotificationId.ToString());
        command.Parameters.AddWithValue("$producer", (int)n.Producer);
        command.Parameters.AddWithValue("$subject", n.SubjectId);
        command.Parameters.AddWithValue("$reason", n.Reason);
        command.Parameters.AddWithValue("$dedup", n.DeduplicationKey);
        command.Parameters.AddWithValue("$priority", (int)n.Priority);
        command.Parameters.AddWithValue("$state", (int)n.State);
        command.Parameters.AddWithValue("$title", n.Title);
        command.Parameters.AddWithValue("$message", n.Message);
        command.Parameters.AddWithValue("$payload", (object?)n.PayloadJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", n.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", n.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$read", n.ReadUtc?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$resolved", n.ResolvedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
    }

    private static NotificationRecord Read(SqliteDataReader reader)
    {
        static DateTimeOffset? Optional(object value) => value is DBNull ? null : DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new(
            NotificationId.Parse(reader.GetString(0)),
            (NotificationProducer)reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            (NotificationPriority)reader.GetInt32(5),
            (NotificationState)reader.GetInt32(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Optional(reader.GetValue(12)),
            Optional(reader.GetValue(13)));
    }
}
