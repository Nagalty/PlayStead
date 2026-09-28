using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Modding;
using PlayStead.Data.Database;

namespace PlayStead.Data.Modding;

public sealed class SqliteModEvidenceStore(DatabaseOptions options) : IModEvidenceStore
{
    public async Task<IReadOnlyList<ModEvidence>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, provider, detector_id, evidence_kind, state, observed_at_utc, detail FROM mod_evidence WHERE game_id=$game ORDER BY observed_at_utc DESC;";
        command.Parameters.AddWithValue("$game", gameId.Value.ToString("D"));
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ModEvidence>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, provider, detector_id, evidence_kind, state, observed_at_utc, detail FROM mod_evidence ORDER BY observed_at_utc DESC;";
        return await ReadAsync(command, cancellationToken);
    }

    public async Task UpsertAsync(ModEvidence evidence, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO mod_evidence(game_id, provider, detector_id, evidence_kind, state, observed_at_utc, detail)
            VALUES($game, $provider, $detector, $kind, $state, $observed, $detail)
            ON CONFLICT(game_id, detector_id, evidence_kind) DO UPDATE SET provider=excluded.provider, state=excluded.state, observed_at_utc=excluded.observed_at_utc, detail=excluded.detail;
            """;
        AddParameters(command, evidence);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(GameId gameId, string detectorId, ModEvidenceKind evidenceKind, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM mod_evidence WHERE game_id=$game AND detector_id=$detector AND evidence_kind=$kind;";
        command.Parameters.AddWithValue("$game", gameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$detector", detectorId);
        command.Parameters.AddWithValue("$kind", (int)evidenceKind);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand command, ModEvidence evidence)
    {
        command.Parameters.AddWithValue("$game", evidence.GameId.Value.ToString("D"));
        command.Parameters.AddWithValue("$provider", (int)evidence.Provider);
        command.Parameters.AddWithValue("$detector", evidence.DetectorId);
        command.Parameters.AddWithValue("$kind", (int)evidence.EvidenceKind);
        command.Parameters.AddWithValue("$state", (int)evidence.State);
        command.Parameters.AddWithValue("$observed", evidence.ObservedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$detail", (object?)evidence.Detail ?? DBNull.Value);
    }

    private static async Task<IReadOnlyList<ModEvidence>> ReadAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var result = new List<ModEvidence>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ModEvidence(
                new GameId(Guid.Parse(reader.GetString(0))),
                (ProviderKind)reader.GetInt32(1),
                reader.GetString(2),
                (ModEvidenceKind)reader.GetInt32(3),
                (ModDetectionState)reader.GetInt32(4),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
