using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Database;

namespace PlayStead.Data.Sessions;

public sealed class SqliteProcessSignatureLearningStore : IProcessSignatureLearningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { RespectRequiredConstructorParameters = true };
    private readonly DatabaseOptions _options;

    public SqliteProcessSignatureLearningStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<ProcessSignatureLearningState?> LoadAsync(InstallationId installationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await LoadAsync(connection, null, installationId, cancellationToken);
    }

    internal static async Task<ProcessSignatureLearningState?> LoadAsync(SqliteConnection connection,
        SqliteTransaction? transaction, InstallationId installationId, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT game_id, root_path, generation_id, policy_version, concurrency_token,
                   last_sequence_number, has_ambiguous_installation,
                   inventory_json, reference_json, confirmation_json, reasons_json
            FROM process_signature_learning WHERE installation_id=$installation;
            """;
        command.Parameters.AddWithValue("$installation", installationId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var found = await reader.ReadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!found) return null;
        try
        {
            if (reader.GetInt64(6) is not (0 or 1))
                throw new InvalidDataException("Stored ambiguity flag is invalid.");
            var state = new ProcessSignatureLearningState(
                Deserialize<ExecutableInventory>(reader.GetString(7)),
                reader.GetInt32(3), Guid.ParseExact(reader.GetString(4), "N"), reader.GetInt64(5), reader.GetBoolean(6),
                reader.IsDBNull(8) ? null : Deserialize<LearningEpisodeSummary>(reader.GetString(8)),
                reader.IsDBNull(9) ? null : Deserialize<LearningEpisodeSummary>(reader.GetString(9)),
                Deserialize<DiscoveryReason[]>(reader.GetString(10)));
            var scope = state.Inventory.Scope;
            if (scope.InstallationId != installationId || scope.GameId.Value != Guid.Parse(reader.GetString(0)) ||
                !PathEquals(scope.RootPath, reader.GetString(1)) || scope.GenerationId != Guid.Parse(reader.GetString(2)))
                throw new InvalidDataException("Stored learning identity disagrees with its inventory.");
            Validate(state);
            return state;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidDataException("Stored learning state is invalid.", exception);
        }
    }

    public async Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expectedConcurrencyToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        var scope = state.Inventory.Scope;
        var existing = await LoadAsync(connection, transaction, scope.InstallationId, cancellationToken);
        if (existing is null ? expectedConcurrencyToken is not null :
            expectedConcurrencyToken != existing.ConcurrencyToken || state.ConcurrencyToken == existing.ConcurrencyToken)
            return false;
        if (!await InstallationMatchesAsync(connection, transaction, scope, cancellationToken)) return false;
        var invalidated = false;
        if (existing is not null)
        {
            var inventoryChanged = !InventoryEquals(existing.Inventory, state.Inventory);
            var generationChanged = existing.Inventory.Scope.GenerationId != scope.GenerationId;
            if (inventoryChanged && !generationChanged)
                throw new ArgumentException("Changed inventory requires a new generation.", nameof(state));
            invalidated = inventoryChanged || generationChanged || existing.PolicyVersion != state.PolicyVersion ||
                existing.HasAmbiguousInstallation != state.HasAmbiguousInstallation;
            if (invalidated)
                state = new ProcessSignatureLearningState(state.Inventory, state.PolicyVersion, state.ConcurrencyToken,
                    state.LastSequenceNumber, state.HasAmbiguousInstallation, null, null, state.Reasons);
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = existing is null ? """
            INSERT INTO process_signature_learning(installation_id, game_id, root_path, generation_id,
                policy_version, concurrency_token, last_sequence_number, has_ambiguous_installation,
                inventory_json, reference_json, confirmation_json, reasons_json)
            VALUES($installation, $gameId, $root, $generation, $policy, $newToken,
                $sequence, $ambiguous, $inventory, $reference, $confirmation, $reasons)
            ON CONFLICT(installation_id) DO NOTHING;
            """ : """
            UPDATE process_signature_learning
            SET game_id=$gameId, root_path=$root, generation_id=$generation,
                policy_version=$policy, concurrency_token=$newToken,
                last_sequence_number=$sequence, has_ambiguous_installation=$ambiguous,
                inventory_json=$inventory, reference_json=$reference,
                confirmation_json=$confirmation, reasons_json=$reasons
            WHERE installation_id=$installation AND concurrency_token=$expectedToken;
            """;
        if (existing is not null)
            command.Parameters.AddWithValue("$expectedToken", expectedConcurrencyToken!.Value.ToString("N"));
        command.Parameters.AddWithValue("$installation", scope.InstallationId.ToString());
        command.Parameters.AddWithValue("$gameId", scope.GameId.ToString());
        command.Parameters.AddWithValue("$root", scope.RootPath);
        command.Parameters.AddWithValue("$generation", scope.GenerationId.ToString("D"));
        command.Parameters.AddWithValue("$policy", state.PolicyVersion);
        command.Parameters.AddWithValue("$newToken", state.ConcurrencyToken.ToString("N"));
        command.Parameters.AddWithValue("$sequence", state.LastSequenceNumber);
        command.Parameters.AddWithValue("$ambiguous", state.HasAmbiguousInstallation);
        command.Parameters.AddWithValue("$inventory", JsonSerializer.Serialize(state.Inventory));
        command.Parameters.AddWithValue("$reference", state.Reference is null ? DBNull.Value : JsonSerializer.Serialize(state.Reference));
        command.Parameters.AddWithValue("$confirmation", state.Confirmation is null ? DBNull.Value : JsonSerializer.Serialize(state.Confirmation));
        command.Parameters.AddWithValue("$reasons", JsonSerializer.Serialize(state.Reasons));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        if (invalidated)
        {
            using var invalidate = connection.CreateCommand();
            invalidate.Transaction = transaction;
            invalidate.CommandText = """
                UPDATE process_signature_validation
                SET validation_state=0, concurrency_token=$token
                WHERE game_id=$gameId AND EXISTS (
                    SELECT 1 FROM process_signatures WHERE game_id=$gameId AND origin=0);
                """;
            invalidate.Parameters.AddWithValue("$gameId", scope.GameId.ToString());
            invalidate.Parameters.AddWithValue("$token", Guid.NewGuid().ToString("N"));
            await invalidate.ExecuteNonQueryAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> InstallationMatchesAsync(SqliteConnection connection,
        SqliteTransaction transaction, InstallationScope scope, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT game_id, install_path, is_present FROM installations WHERE installation_id=$installation;";
        command.Parameters.AddWithValue("$installation", scope.InstallationId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var found = await reader.ReadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return found && reader.GetString(0) == scope.GameId.ToString() && !reader.IsDBNull(1) &&
            PathEquals(reader.GetString(1), scope.RootPath) && reader.GetBoolean(2) == scope.IsPresent;
    }

    private static T Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException("Stored learning value is null.");

    private static bool PathEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool ScopeEquals(InstallationScope left, InstallationScope right) =>
        left.GameId == right.GameId && left.InstallationId == right.InstallationId &&
        PathEquals(left.RootPath, right.RootPath) && left.GenerationId == right.GenerationId &&
        left.IsPresent == right.IsPresent;

    private static bool InventoryEquals(ExecutableInventory left, ExecutableInventory right) =>
        left.Scope.GameId == right.Scope.GameId && left.Scope.InstallationId == right.Scope.InstallationId &&
        PathEquals(left.Scope.RootPath, right.Scope.RootPath) && left.Scope.IsPresent == right.Scope.IsPresent &&
        left.Completeness == right.Completeness && left.Candidates.Count == right.Candidates.Count &&
        left.Candidates.Zip(right.Candidates).All(pair =>
            PathEquals(pair.First.ExecutablePath, pair.Second.ExecutablePath) &&
            PathEquals(pair.First.ExecutableName, pair.Second.ExecutableName) && pair.First.Revision == pair.Second.Revision) &&
        left.Issues.Count == right.Issues.Count && left.Issues.Zip(right.Issues).All(pair =>
            pair.First.Kind == pair.Second.Kind && PathEquals(pair.First.Path, pair.Second.Path));

    private static void Validate(ProcessSignatureLearningState state)
    {
        if (state.Reasons.Any(reason => !Enum.IsDefined(reason)))
            throw new ArgumentException("Unknown discovery reason.", nameof(state));
        foreach (var summary in new[] { state.Reference, state.Confirmation })
        {
            if (summary is null) continue;
            if (!ScopeEquals(summary.Scope, state.Inventory.Scope) || summary.PolicyVersion != state.PolicyVersion)
                throw new ArgumentException("Summary scope and policy must match the inventory.", nameof(state));
            foreach (var evidence in summary.Candidates)
            {
                var candidate = state.Inventory.Candidates.FirstOrDefault(candidate =>
                    PathEquals(candidate.ExecutablePath, evidence.ExecutablePath));
                if (candidate is null || candidate.Revision != evidence.Revision)
                    throw new ArgumentException("Summary candidates and revisions must match the inventory.", nameof(state));
            }
        }
        var last = state.Confirmation ?? state.Reference;
        if (last is not null && last.SequenceNumber != state.LastSequenceNumber)
            throw new ArgumentException("Last sequence must match the last completed summary.", nameof(state));
        if (state.Confirmation is { } confirmation && state.Reference is { } reference &&
            (confirmation.EpisodeId == reference.EpisodeId || confirmation.SequenceNumber <= reference.SequenceNumber ||
             confirmation.SequenceNumber - reference.SequenceNumber != 1 || confirmation.StartedAtUtc <= reference.EndedAtUtc))
            throw new ArgumentException("Confirmation must be a distinct, adjacent, later episode.", nameof(state));
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath, Pooling = false, ForeignKeys = true
        }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
