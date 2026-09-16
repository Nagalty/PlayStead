using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.Identity;

public sealed class SqliteIdentityResolutionStore : IIdentityResolutionStore
{
    private readonly DatabaseOptions _options;

    public SqliteIdentityResolutionStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<GameIdentityResolution?> GetAsync(
        GameId gameId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(cancellationToken);

        return await ReadAsync(
            connection,
            gameId,
            cancellationToken);
    }

    public async Task<GameIdentityResolution> GetOrCreateProvisionalAsync(
        GameId gameId,
        IdentityResolutionEvidence evidence,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);

        var existing = await ReadAsync(
            connection,
            gameId,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var provisionalId = ProvisionalIdentityId.New();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO game_identity_resolutions(
                game_id,
                provisional_id,
                state,
                candidate_content_id,
                evidence_json,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                $provisionalId,
                $state,
                NULL,
                $evidenceJson,
                $createdUtc,
                $updatedUtc);
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        command.Parameters.AddWithValue(
            "$provisionalId",
            provisionalId.ToString());
        command.Parameters.AddWithValue(
            "$state",
            (int)IdentityResolutionState.New);
        command.Parameters.AddWithValue(
            "$evidenceJson",
            JsonSerializer.Serialize(evidence));
        command.Parameters.AddWithValue(
            "$createdUtc",
            observedAtUtc.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedUtc",
            observedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);

        return await ReadAsync(
                connection,
                gameId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Identity resolution for game '{gameId}' was not persisted.");
    }

    public async Task UpsertAsync(
        GameIdentityResolution resolution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_identity_resolutions(
                game_id,
                provisional_id,
                state,
                candidate_content_id,
                evidence_json,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                $provisionalId,
                $state,
                $candidateContentId,
                $evidenceJson,
                $createdUtc,
                $updatedUtc)
            ON CONFLICT(game_id) DO UPDATE SET
                provisional_id = COALESCE(
                    game_identity_resolutions.provisional_id,
                    excluded.provisional_id),
                state = excluded.state,
                candidate_content_id = excluded.candidate_content_id,
                evidence_json = excluded.evidence_json,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue(
            "$gameId",
            resolution.GameId.ToString());
        command.Parameters.AddWithValue(
            "$provisionalId",
            resolution.ProvisionalIdentityId is { } provisionalId
                ? provisionalId.ToString()
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$state",
            (int)resolution.State);
        command.Parameters.AddWithValue(
            "$candidateContentId",
            resolution.CandidateContentId is { } candidateContentId
                ? candidateContentId.ToString()
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$evidenceJson",
            JsonSerializer.Serialize(resolution.Evidence));
        command.Parameters.AddWithValue(
            "$createdUtc",
            resolution.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedUtc",
            resolution.UpdatedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameIdentityResolution>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                game_id,
                provisional_id,
                state,
                candidate_content_id,
                evidence_json,
                created_utc,
                updated_utc
            FROM game_identity_resolutions
            ORDER BY game_id;
            """;

        var result = new List<GameIdentityResolution>();
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }

        return result;
    }

    private static async Task<GameIdentityResolution?> ReadAsync(
        SqliteConnection connection,
        GameId gameId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                game_id,
                provisional_id,
                state,
                candidate_content_id,
                evidence_json,
                created_utc,
                updated_utc
            FROM game_identity_resolutions
            WHERE game_id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString());

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? Read(reader)
            : null;
    }

    private static GameIdentityResolution Read(
        SqliteDataReader reader)
    {
        var evidence =
            JsonSerializer.Deserialize<IdentityResolutionEvidence>(
                reader.GetString(4))
            ?? throw new InvalidOperationException(
                "Identity resolution evidence is invalid.");

        return new GameIdentityResolution(
            new GameId(Guid.Parse(reader.GetString(0))),
            reader.IsDBNull(1)
                ? null
                : ProvisionalIdentityId.Parse(reader.GetString(1)),
            (IdentityResolutionState)reader.GetInt32(2),
            reader.IsDBNull(3)
                ? null
                : new CatalogContentId(
                    Guid.Parse(reader.GetString(3))),
            evidence,
            DateTimeOffset.Parse(
                reader.GetString(5),
                CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(
                reader.GetString(6),
                CultureInfo.InvariantCulture));
    }
}
