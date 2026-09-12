using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Steam;
using PlayStead.Data.Database;

namespace PlayStead.Data.Steam;

public sealed class SqliteSteamEvidenceStore :
    ISteamEvidenceStore
{
    private readonly DatabaseOptions _options;
    private readonly SteamDepotManifestJsonSerializer _serializer;

    public SqliteSteamEvidenceStore(
        DatabaseOptions options)
        : this(
            options,
            new SteamDepotManifestJsonSerializer())
    {
    }

    public SqliteSteamEvidenceStore(
        DatabaseOptions options,
        SteamDepotManifestJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializer);

        _options = options;
        _serializer = serializer;
    }

    public async Task ReplaceLocalAsync(
        IReadOnlyCollection<SteamLocalEvidence> evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var delete = connection.CreateCommand();
        delete.Transaction = (SqliteTransaction)transaction;
        delete.CommandText =
            "DELETE FROM steam_local_evidence;";

        await delete.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in evidence
                     .OrderBy(
                         x => x.AppId,
                         StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidateLocal(item);

            var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO steam_local_evidence(
                    app_id,
                    build_id,
                    branch_name,
                    depot_manifests_json,
                    observed_at_utc)
                VALUES (
                    $app_id,
                    $build_id,
                    $branch_name,
                    $depots,
                    $observed_at_utc);
                """;

            insert.Parameters.AddWithValue(
                "$app_id",
                item.AppId);

            insert.Parameters.AddWithValue(
                "$build_id",
                DbValue(item.BuildId));

            insert.Parameters.AddWithValue(
                "$branch_name",
                DbValue(item.BranchName));

            insert.Parameters.AddWithValue(
                "$depots",
                _serializer.Serialize(item.DepotManifestIds));

            insert.Parameters.AddWithValue(
                "$observed_at_utc",
                item.ObservedAtUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture));

            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SteamLocalEvidence>> GetLocalAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                app_id,
                build_id,
                branch_name,
                depot_manifests_json,
                observed_at_utc
            FROM steam_local_evidence
            ORDER BY app_id COLLATE BINARY;
            """;

        var result = new List<SteamLocalEvidence>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new SteamLocalEvidence(
                    reader.GetString(0),
                    ReadNullableString(reader, 1),
                    ReadNullableString(reader, 2),
                    _serializer.Deserialize(
                        reader.GetString(3)),
                    ParseTimestamp(
                        reader.GetString(4))));
        }

        return result;
    }

    public async Task<SteamRemoteEvidence?> GetRemoteAsync(
        string appId,
        string branchName,
        CancellationToken cancellationToken)
    {
        ValidateKey(appId, branchName);

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                app_id,
                branch_name,
                build_id,
                depot_manifests_json,
                observed_at_utc,
                source
            FROM steam_remote_evidence
            WHERE app_id = $app_id
              AND branch_name = $branch_name
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$app_id",
            appId);

        command.Parameters.AddWithValue(
            "$branch_name",
            branchName);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRemote(reader);
    }

    public async Task UpsertRemoteAsync(
        SteamRemoteEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        ValidateRemote(evidence);

        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO steam_remote_evidence(
                app_id,
                branch_name,
                build_id,
                depot_manifests_json,
                observed_at_utc,
                source,
                schema_version)
            VALUES (
                $app_id,
                $branch_name,
                $build_id,
                $depots,
                $observed_at_utc,
                $source,
                1)
            ON CONFLICT(app_id, branch_name)
            DO UPDATE SET
                build_id = excluded.build_id,
                depot_manifests_json = excluded.depot_manifests_json,
                observed_at_utc = excluded.observed_at_utc,
                source = excluded.source,
                schema_version = excluded.schema_version;
            """;

        command.Parameters.AddWithValue(
            "$app_id",
            evidence.AppId);

        command.Parameters.AddWithValue(
            "$branch_name",
            evidence.BranchName);

        command.Parameters.AddWithValue(
            "$build_id",
            DbValue(evidence.BuildId));

        command.Parameters.AddWithValue(
            "$depots",
            _serializer.Serialize(evidence.DepotManifestIds));

        command.Parameters.AddWithValue(
            "$observed_at_utc",
            evidence.ObservedAtUtc
                .ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));

        command.Parameters.AddWithValue(
            "$source",
            (int)evidence.Source);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SteamRemoteEvidence>> GetAllRemoteAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                app_id,
                branch_name,
                build_id,
                depot_manifests_json,
                observed_at_utc,
                source
            FROM steam_remote_evidence
            ORDER BY
                app_id COLLATE BINARY,
                branch_name COLLATE BINARY;
            """;

        var result = new List<SteamRemoteEvidence>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                ReadRemote(reader));
        }

        return result;
    }

    public async Task ClearRemoteAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM steam_remote_evidence;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private SteamRemoteEvidence ReadRemote(
        SqliteDataReader reader)
    {
        var sourceValue = reader.GetInt32(5);

        if (!Enum.IsDefined(
                typeof(SteamRemoteEvidenceSource),
                sourceValue))
        {
            throw new FormatException(
                $"Unknown Steam remote evidence source value '{sourceValue}'.");
        }

        return new SteamRemoteEvidence(
            reader.GetString(0),
            reader.GetString(1),
            ReadNullableString(reader, 2),
            _serializer.Deserialize(
                reader.GetString(3)),
            ParseTimestamp(
                reader.GetString(4)),
            (SteamRemoteEvidenceSource)sourceValue);
    }

    private static string? ReadNullableString(
        SqliteDataReader reader,
        int ordinal)
        => reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);

    private static DateTimeOffset ParseTimestamp(
        string value)
        => DateTimeOffset.ParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private static object DbValue(
        string? value)
        => value is null
            ? DBNull.Value
            : value;

    private static void ValidateLocal(
        SteamLocalEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            evidence.AppId);

        ArgumentNullException.ThrowIfNull(
            evidence.DepotManifestIds);
    }

    private static void ValidateRemote(
        SteamRemoteEvidence evidence)
    {
        ValidateKey(
            evidence.AppId,
            evidence.BranchName);

        ArgumentNullException.ThrowIfNull(
            evidence.DepotManifestIds);
    }

    private static void ValidateKey(
        string appId,
        string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
    }
}
