using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Database;

namespace PlayStead.Data.Sessions;

public sealed class SqliteProcessSignatureStore : IProcessSignatureStore, IProcessSignatureDiscoveryStore
{
    private readonly DatabaseOptions _options;

    public SqliteProcessSignatureStore(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public Task<bool> TryInsertDiscoveredIfAbsentAsync(DiscoveredSignatureWrite write, CancellationToken cancellationToken)
        => AcceptAsync(write, null, cancellationToken);
    public Task<bool> TryRevalidateDiscoveredAsync(DiscoveredSignatureWrite write, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken)
    {
        ValidateExpectation(expected);
        if (expected.ValidationState != ProcessSignatureValidationState.NeedsRevalidation)
            throw new ArgumentException("Revalidation requires a suspended signature.", nameof(expected));
        return AcceptAsync(write, expected, cancellationToken);
    }
    public async Task<bool> TryRestoreDiscoveredValidationAsync(Guid gameId,
        DiscoveredSignatureExpectation expected, CancellationToken cancellationToken)
    {
        ValidateExpectation(expected);
        if (gameId == Guid.Empty) throw new ArgumentException("Game identity must not be empty.", nameof(gameId));
        if (expected.ValidationState != ProcessSignatureValidationState.NeedsRevalidation)
            throw new ArgumentException("Structural recovery requires a suspended signature.", nameof(expected));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction, """
            UPDATE process_signature_validation SET validation_state=1,concurrency_token=$newToken
            WHERE game_id=$gameId AND concurrency_token=$expectedSignatureToken
                AND generation_id IS $expectedGeneration AND validation_state=$expectedState
                AND installation_id IS $expectedInstallation
                AND EXISTS (SELECT 1 FROM process_signatures p WHERE p.game_id=$gameId AND p.origin=0);
            """, ("$gameId", gameId.ToString("D")), ("$newToken", Guid.NewGuid().ToString("N")),
            ("$expectedSignatureToken", expected.ConcurrencyToken.ToString("N")),
            ("$expectedGeneration", expected.GenerationId?.ToString("D")),
            ("$expectedState", (int)expected.ValidationState),
            ("$expectedInstallation", expected.InstallationId?.ToString()));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryRefreshDiscoveredValidationAsync(ProcessSignature signature,
        DiscoveredSignatureExpectation expected, DiscoveryInventoryContext current,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(current);
        ValidateExpectation(expected);
        if (expected.ValidationState != ProcessSignatureValidationState.NeedsRevalidation ||
            signature.Origin != ProcessSignatureOrigin.Discovered || signature.Discovery is null)
            throw new ArgumentException("A stale discovered signature is required.", nameof(expected));

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        for (var ordinal = 0; ordinal < signature.Entries.Count; ordinal++)
        {
            var entry = signature.Entries[ordinal];
            var candidate = current.Inventory.Candidates.FirstOrDefault(item =>
                PathEquals(item.ExecutablePath, entry.ExecutablePath!) &&
                PathEquals(item.ExecutableName, entry.ExecutableName!));
            if (candidate is null) return false;
            using var updateEntry = Command(connection, transaction, """
                UPDATE process_signature_entries
                SET executable_name=$name,kind=$kind,executable_path=$path,
                    validated_size_bytes=$size,validated_last_write_utc=$date
                WHERE game_id=$gameId AND ordinal=$ordinal;
                """, ("$gameId", signature.GameId.ToString("D")), ("$ordinal", ordinal),
                ("$name", candidate.ExecutableName), ("$kind", (int)entry.Kind),
                ("$path", candidate.ExecutablePath), ("$size", candidate.Revision.SizeBytes),
                ("$date", FormatUtc(candidate.Revision.LastWriteTimeUtc)));
            if (await updateEntry.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        }
        using var update = Command(connection, transaction, """
            UPDATE process_signature_validation
            SET generation_id=$generation,validation_state=1,concurrency_token=$newToken
            WHERE game_id=$gameId AND concurrency_token=$expectedToken
                AND generation_id IS $expectedGeneration AND validation_state=$expectedState
                AND installation_id IS $expectedInstallation;
            """, ("$gameId", signature.GameId.ToString("D")),
            ("$generation", current.Inventory.Scope.GenerationId.ToString("D")),
            ("$newToken", Guid.NewGuid().ToString("N")),
            ("$expectedToken", expected.ConcurrencyToken.ToString("N")),
            ("$expectedGeneration", expected.GenerationId?.ToString("D")),
            ("$expectedState", (int)expected.ValidationState),
            ("$expectedInstallation", expected.InstallationId?.ToString()));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
    public async Task<bool> TryInvalidateDiscoveredAsync(Guid gameId, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken)
    {
        ValidateExpectation(expected);
        if (gameId == Guid.Empty) throw new ArgumentException("Game identity must not be empty.", nameof(gameId));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction, """
            UPDATE process_signature_validation SET validation_state=0,concurrency_token=$newToken
            WHERE game_id=$gameId AND concurrency_token=$expectedSignatureToken
                AND generation_id IS $expectedGeneration AND validation_state=$expectedState
                AND installation_id IS $expectedInstallation
                AND EXISTS (SELECT 1 FROM process_signatures p WHERE p.game_id=$gameId AND p.origin=0);
            """, ("$gameId", gameId.ToString("D")), ("$newToken", Guid.NewGuid().ToString("N")),
            ("$expectedSignatureToken", expected.ConcurrencyToken.ToString("N")),
            ("$expectedGeneration", expected.GenerationId?.ToString("D")), ("$expectedState", (int)expected.ValidationState),
            ("$expectedInstallation", expected.InstallationId?.ToString()));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        await ClearProofAsync(connection, transaction, gameId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> AcceptAsync(DiscoveredSignatureWrite write, DiscoveredSignatureExpectation? expected,
        CancellationToken cancellationToken)
    {
        ValidateWrite(write, expected);
        var signature = write.Signature;
        var metadata = signature.Discovery!;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        var state = await SqliteProcessSignatureLearningStore.LoadAsync(connection, transaction,
            metadata.InstallationId!.Value, cancellationToken);
        if (!ProofMatches(write, state) || !await InstallationMatchesAsync(connection, transaction, state!.Inventory.Scope, cancellationToken))
            return false;
        using var parent = expected is null ? Command(connection, transaction, """
            INSERT INTO process_signatures(game_id,origin,updated_at_utc) VALUES($gameId,0,$updatedUtc)
            ON CONFLICT(game_id) DO NOTHING;
            """, ("$gameId", signature.GameId.ToString("D")), ("$updatedUtc", FormatUtc(signature.UpdatedAtUtc)))
            : ExpectationCommand(connection, transaction, "UPDATE process_signatures SET updated_at_utc=$updatedUtc",
                signature.GameId, expected);
        if (expected is not null) parent.Parameters.AddWithValue("$updatedUtc", FormatUtc(signature.UpdatedAtUtc));
        if (await parent.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        await WriteContentsAsync(connection, transaction, signature, metadata, cancellationToken);
        using var consume = Command(connection, transaction, """
            UPDATE process_signature_learning SET reference_json=NULL,confirmation_json=NULL,reasons_json='[]',concurrency_token=$newToken
            WHERE installation_id=$installation AND concurrency_token=$expectedToken;
            """, ("$installation", metadata.InstallationId.Value.ToString()),
            ("$expectedToken", write.ExpectedLearningToken.ToString("N")), ("$newToken", Guid.NewGuid().ToString("N")));
        if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static void ValidateWrite(DiscoveredSignatureWrite write, DiscoveredSignatureExpectation? expected)
    {
        ArgumentNullException.ThrowIfNull(write);
        var signature = write.Signature;
        if (signature is null || signature.GameId == Guid.Empty || write.ExpectedLearningToken == Guid.Empty ||
            write.ReferenceEpisodeId == Guid.Empty || write.ConfirmationEpisodeId == Guid.Empty ||
            write.ReferenceEpisodeId == write.ConfirmationEpisodeId || signature.Origin != ProcessSignatureOrigin.Discovered ||
            signature.Discovery is not { InstallationId: not null, GenerationId: not null, PolicyVersion: > 0,
                ValidationState: ProcessSignatureValidationState.Valid } metadata ||
            metadata.InstallationId.Value.Value == Guid.Empty || metadata.GenerationId == Guid.Empty ||
            metadata.ConcurrencyToken == Guid.Empty || metadata.ConcurrencyToken == expected?.ConcurrencyToken ||
            signature.Entries is not { Count: 1 } || signature.Entries[0] is not { Kind: ProcessSignatureEntryKind.Main,
                ValidatedRevision: not null } entry || string.IsNullOrWhiteSpace(entry.ExecutablePath) || string.IsNullOrWhiteSpace(entry.ExecutableName))
            throw new ArgumentException("A discovered write requires one scoped, validated Main and complete proof identities.", nameof(write));
    }

    private static void ValidateExpectation(DiscoveredSignatureExpectation expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.ConcurrencyToken == Guid.Empty || expected.GenerationId == Guid.Empty ||
            expected.InstallationId?.Value == Guid.Empty || !Enum.IsDefined(expected.ValidationState))
            throw new ArgumentException("Invalid signature expectation.", nameof(expected));
    }

    private static SqliteCommand ExpectationCommand(SqliteConnection connection, SqliteTransaction transaction,
        string update, Guid gameId, DiscoveredSignatureExpectation expected) => Command(connection, transaction, update + """

            WHERE game_id=$gameId AND origin=0 AND EXISTS (
                SELECT 1 FROM process_signature_validation v WHERE v.game_id=process_signatures.game_id
                AND v.concurrency_token=$expectedSignatureToken AND v.generation_id IS $expectedGeneration
                AND v.validation_state=$expectedState AND v.installation_id IS $expectedInstallation);
            """, ("$gameId", gameId.ToString("D")), ("$expectedSignatureToken", expected.ConcurrencyToken.ToString("N")),
            ("$expectedGeneration", expected.GenerationId?.ToString("D")), ("$expectedState", (int)expected.ValidationState),
            ("$expectedInstallation", expected.InstallationId?.ToString()));

    private static bool ProofMatches(DiscoveredSignatureWrite write, ProcessSignatureLearningState? state)
    {
        var metadata = write.Signature.Discovery!;
        if (state is null || state.ConcurrencyToken != write.ExpectedLearningToken || state.HasAmbiguousInstallation ||
            state.Inventory.Completeness != InventoryCompleteness.Complete || !state.Inventory.Scope.IsPresent ||
            state.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion || metadata.PolicyVersion != state.PolicyVersion ||
            state.Inventory.Scope.GameId.Value != write.Signature.GameId || state.Inventory.Scope.GenerationId != metadata.GenerationId ||
            state.Inventory.Scope.InstallationId != metadata.InstallationId ||
            state.Reference is not { Quality: EpisodeQuality.Complete } reference ||
            state.Confirmation is not { Quality: EpisodeQuality.Complete } confirmation ||
            reference.EpisodeId != write.ReferenceEpisodeId || confirmation.EpisodeId != write.ConfirmationEpisodeId)
            return false;
        // The shared loader checks both complete summaries' scope, policy, revisions and adjacent episode identity.
        // Main selection remains the Core policy's responsibility; this check only binds the proposal to its proof.
        var main = write.Signature.Entries[0];
        return state.Inventory.Candidates.Any(candidate => PathEquals(candidate.ExecutablePath, main.ExecutablePath!) &&
                PathEquals(candidate.ExecutableName, main.ExecutableName) && candidate.Revision == main.ValidatedRevision) &&
            new[] { reference, confirmation }.All(episode => episode.Candidates.Any(candidate =>
                PathEquals(candidate.ExecutablePath, main.ExecutablePath!) && candidate.Revision == main.ValidatedRevision &&
                candidate.PresenceRanges.Count > 0));
    }

    private static async Task<bool> InstallationMatchesAsync(SqliteConnection connection, SqliteTransaction transaction,
        InstallationScope scope, CancellationToken cancellationToken)
    {
        using (var command = Command(connection, transaction,
            "SELECT game_id,install_path,is_present FROM installations WHERE installation_id=$installation",
            ("$installation", scope.InstallationId.ToString())))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != scope.GameId.ToString() ||
                reader.IsDBNull(1) || !PathEquals(reader.GetString(1), scope.RootPath) || !reader.GetBoolean(2)) return false;
        }
        using var roots = Command(connection, transaction,
            "SELECT install_path FROM installations WHERE is_present=1 AND installation_id<>$installation AND install_path IS NOT NULL",
            ("$installation", scope.InstallationId.ToString()));
        await using var rootReader = await roots.ExecuteReaderAsync(cancellationToken);
        while (await rootReader.ReadAsync(cancellationToken))
            if (RootsOverlap(scope.RootPath, rootReader.GetString(0))) return false;
        return true;
    }

    private static bool PathEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool RootsOverlap(string left, string right)
    {
        left = left.Replace('/', '\\').TrimEnd('\\');
        right = right.Replace('/', '\\').TrimEnd('\\');
        return PathEquals(left, right) || left.StartsWith(right + "\\", StringComparison.OrdinalIgnoreCase) ||
            right.StartsWith(left + "\\", StringComparison.OrdinalIgnoreCase);
    }

    public async Task UpsertAsync(ProcessSignature signature, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(signature.Entries);
        if (signature.Origin == ProcessSignatureOrigin.Discovered && signature.Discovery is not null)
            throw new ArgumentException("Validated discovery requires a conditional write.", nameof(signature));
        if (!Enum.IsDefined(signature.Origin)) throw new ArgumentException("Unknown origin.", nameof(signature));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction, """
            INSERT INTO process_signatures(game_id,origin,updated_at_utc) VALUES($gameId,$origin,$updatedUtc)
            ON CONFLICT(game_id) DO UPDATE SET origin=excluded.origin,updated_at_utc=excluded.updated_at_utc
            WHERE $origin=1 OR ($origin=2 AND process_signatures.origin<>1);
            """, ("$gameId", signature.GameId.ToString("D")), ("$origin", (int)signature.Origin),
            ("$updatedUtc", FormatUtc(signature.UpdatedAtUtc)));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("The existing signature is protected from this replacement.");
        await WriteContentsAsync(connection, transaction, signature, signature.Origin == ProcessSignatureOrigin.Discovered
            ? new DiscoveredSignatureMetadata(null, null, null, ProcessSignatureValidationState.NeedsRevalidation, Guid.NewGuid())
            : null, cancellationToken);
        await ClearProofAsync(connection, transaction, signature.GameId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken cancellationToken) =>
        (await ReadAsync(gameId, cancellationToken)).SingleOrDefault();

    public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken cancellationToken) =>
        ReadAsync(null, cancellationToken);

    private async Task<IReadOnlyList<ProcessSignature>> ReadAsync(Guid? gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: true);
        using var command = Command(connection, transaction, """
            SELECT p.game_id,p.origin,p.updated_at_utc,v.installation_id,v.generation_id,
                   v.policy_version,v.validation_state,v.concurrency_token
            FROM process_signatures p LEFT JOIN process_signature_validation v ON v.game_id=p.game_id
            WHERE $gameId IS NULL OR p.game_id=$gameId ORDER BY p.game_id COLLATE BINARY;
            """, ("$gameId", gameId?.ToString("D")));
        var parents = new List<ProcessSignature>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var origin = ReadEnum<ProcessSignatureOrigin>(reader.GetInt32(1), "signature origin");
                var metadata = reader.IsDBNull(7) ? null : new DiscoveredSignatureMetadata(
                    reader.IsDBNull(3) ? null : new InstallationId(Guid.Parse(reader.GetString(3))),
                    reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    ReadEnum<ProcessSignatureValidationState>(reader.GetInt32(6), "validation state"),
                    Guid.ParseExact(reader.GetString(7), "N"));
                parents.Add(new ProcessSignature(Guid.Parse(reader.GetString(0)), [], origin,
                    ParseUtc(reader.GetString(2)), metadata));
            }
        }
        var result = new List<ProcessSignature>(parents.Count);
        foreach (var parent in parents)
            result.Add(parent with { Entries = await LoadEntriesAsync(connection, transaction, parent.GameId, cancellationToken) });
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task WriteContentsAsync(SqliteConnection connection, SqliteTransaction transaction,
        ProcessSignature signature, DiscoveredSignatureMetadata? metadata, CancellationToken cancellationToken)
    {
        using (var delete = Command(connection, transaction,
            "DELETE FROM process_signature_validation WHERE game_id=$gameId", ("$gameId", signature.GameId.ToString("D"))))
            await delete.ExecuteNonQueryAsync(cancellationToken);
        if (metadata is not null)
        {
            using var insert = Command(connection, transaction, """
                INSERT INTO process_signature_validation(game_id,installation_id,generation_id,policy_version,validation_state,concurrency_token)
                VALUES($gameId,$installation,$generation,$policy,$state,$token);
                """, ("$gameId", signature.GameId.ToString("D")), ("$installation", metadata.InstallationId?.ToString()),
                ("$generation", metadata.GenerationId?.ToString("D")), ("$policy", metadata.PolicyVersion),
                ("$state", (int)metadata.ValidationState), ("$token", metadata.ConcurrencyToken.ToString("N")));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        using (var delete = Command(connection, transaction,
            "DELETE FROM process_signature_entries WHERE game_id=$gameId", ("$gameId", signature.GameId.ToString("D"))))
            await delete.ExecuteNonQueryAsync(cancellationToken);
        for (var index = 0; index < signature.Entries.Count; index++)
        {
            var entry = signature.Entries[index];
            if (string.IsNullOrWhiteSpace(entry.ExecutableName))
                throw new ArgumentException("Process signature executable names cannot be empty.", nameof(signature));
            using var insert = Command(connection, transaction, """
                INSERT INTO process_signature_entries(game_id,ordinal,executable_name,kind,executable_path,
                    validated_size_bytes,validated_last_write_utc)
                VALUES($gameId,$ordinal,$name,$kind,$path,$size,$date);
                """, ("$gameId", signature.GameId.ToString("D")), ("$ordinal", index), ("$name", entry.ExecutableName),
                ("$kind", (int)entry.Kind), ("$path", entry.ExecutablePath), ("$size", entry.ValidatedRevision?.SizeBytes),
                ("$date", entry.ValidatedRevision is null ? null : FormatUtc(entry.ValidatedRevision.LastWriteTimeUtc)));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ClearProofAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid gameId, CancellationToken cancellationToken)
    {
        using var command = Command(connection, transaction, """
            UPDATE process_signature_learning SET reference_json=NULL,confirmation_json=NULL,reasons_json='[]',
                concurrency_token=lower(hex(randomblob(16))) WHERE game_id=$gameId;
            """, ("$gameId", gameId.ToString("D")));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ProcessSignatureEntry>> LoadEntriesAsync(SqliteConnection connection,
        SqliteTransaction transaction, Guid gameId, CancellationToken cancellationToken)
    {
        using var command = Command(connection, transaction, """
            SELECT executable_name,kind,executable_path,validated_size_bytes,validated_last_write_utc
            FROM process_signature_entries WHERE game_id=$gameId ORDER BY ordinal;
            """, ("$gameId", gameId.ToString("D")));
        var result = new List<ProcessSignatureEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(3) != reader.IsDBNull(4))
                throw new InvalidDataException("Stored file revision is incomplete.");
            result.Add(new ProcessSignatureEntry(reader.GetString(0),
                ReadEnum<ProcessSignatureEntryKind>(reader.GetInt32(1), "entry kind"),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : new FileRevision(reader.GetInt64(3), ParseUtc(reader.GetString(4)))));
        }
        return result;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = _options.DatabasePath, Pooling = false, ForeignKeys = true }.ToString());
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

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        return command;
    }

    private static TEnum ReadEnum<TEnum>(int value, string label) where TEnum : struct, Enum =>
        Enum.IsDefined(typeof(TEnum), value) ? (TEnum)Enum.ToObject(typeof(TEnum), value)
            : throw new FormatException($"Unknown {label} value '{value}'.");
    private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.ParseExact(value, "O",
        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
