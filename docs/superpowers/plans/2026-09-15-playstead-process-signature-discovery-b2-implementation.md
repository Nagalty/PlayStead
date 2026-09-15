# PlayStead Process Signature Discovery B2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify the unregistered intermediate discovery pipeline: simulated captures, completed learning evidence across restarts, conditional SQLite acceptance, and safe path-aware session matching.

**Architecture:** Core owns observation, evidence and B1 policy orchestration through interfaces; Data owns durable state and atomic authority checks; Platform supplies trusted targeted revision reads. The existing signature store remains authoritative, and acceptance consumes proof in the same SQLite transaction. B2 components are constructed explicitly in tests; production discovery wiring belongs to B3.

**Tech Stack:** C# / .NET 10 / SQLite / xUnit / WPF where existing runtime tests require it

**Spec:** `docs/superpowers/specs/2026-09-15-playstead-process-signature-discovery-design.md`

## Global Constraints

- Workspace: `D:\Dev\PlayStead\worktrees\0.4.1-media-foundation`; branch: `feat/0.4.1-media-foundation`.
- Implementation baseline: `3b1ed43a19d8a959d2526361d8d2737f844ea361`. B1 reported Core 170/170, Platform 89/89 and Release build with zero warnings/errors; rerun verification during implementation, not during creation of this plan.
- Authority: spec > this plan > implementation convenience. B1 policy remains unchanged.
- Strict RED -> GREEN -> regressions -> review -> commit -> postverify per task. A missing newly specified API can establish an initial compile RED; errors in test fixtures, namespaces or infrastructure cannot. Follow that API RED with executed behavioral assertions before closing a task.
- Fresh implementer and spec/quality reviewers per implementation task when using subagent-driven-development. This document was self-reviewed without dispatching implementation agents.
- False negative is preferred to false positive; two independently completed qualifying episodes are necessary and not sufficient without all other guards.
- No invented path, parent-process relation, session, exclusion heuristic, confidence score or elapsed time.
- No Windows/SQLite/WPF/Steam dependency in Core. Data and Platform never select Main.
- No changes to SessionMonitor, LocalStartupPipeline, Library scan, launch service, LaunchIntent, DI, startup, UI, Home, Providers, polling interval or exception behavior.
- No second poller. No activation of the coordinator from SessionRuntime. Capture remains once per existing runtime refresh.
- No recursive inventory per tick, executable launch, hash of game binaries, remote provider, production DB operation, opportunistic refactor, push or merge.
- Task 7 stays in `stash@{0}: On feat/0.4.1-media-foundation: wip/task7-home-media-integration` with stash object `622b590181ed07283907190c342963489be60340`. Never pop it.
- No alteration of migrations 001 through 005; no change to observed sessions, heartbeat interval (5 s), two-snapshot session confirmation or correction semantics.
- Cancellation propagates; no acceptance after cancellation observed before commit. A committed transaction remains valid. No global silent exception handler.
- All new collection-bearing Core contracts copy inputs into read-only arrays as B1 does; use existing value objects GameId/InstallationId and B1 FileRevision, not replacement identity types.

## File map

Paths below are relative to the authoritative worktree. CREATE entries are proposed future files, not files created by this planning intervention. TEST distinguishes new tests from the five necessary historical migration-test adjustments.

### CREATE

- `src/PlayStead.Core/Sessions/ProcessSignatureValidationState.cs` — Persisted validation enum.
- `src/PlayStead.Core/Sessions/DiscoveredSignatureMetadata.cs` — Parent discovery scope, validation and concurrency metadata.
- `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureLearningState.cs` — Immutable, bounded completed learning state.
- `src/PlayStead.Core/Sessions/Discovery/IProcessSignatureLearningStore.cs` — Compare-and-swap learning persistence contract.
- `src/PlayStead.Core/Sessions/Discovery/DiscoveredSignatureWrite.cs` — Acceptance request and expected signature identity.
- `src/PlayStead.Core/Sessions/Discovery/IProcessSignatureDiscoveryStore.cs` — Conditional insert, revalidation and suspension contracts.
- `src/PlayStead.Core/Sessions/Discovery/ExecutableRevisionResult.cs` — Readable revision or explicit inventory issue.
- `src/PlayStead.Core/Sessions/Discovery/IExecutableRevisionSource.cs` — Targeted file revision read contract.
- `src/PlayStead.Core/Sessions/Discovery/DiscoveryInventoryContext.cs` — Fresh inventory plus installation ambiguity supplied by the caller.
- `src/PlayStead.Core/Sessions/Discovery/DiscoveredSignatureValidator.cs` — Per-use admissibility and targeted revision checks.
- `src/PlayStead.Core/Sessions/IDiscoveredSignatureValidator.cs` — Runtime Pending/Valid/Invalid gate, without discovery.
- `src/PlayStead.Core/Sessions/Discovery/ProcessObservationBatch.cs` — Immutable supplied capture and capture quality.
- `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureLearningCoordinator.cs` — Sequential in-memory episode transitions and B1 policy invocation.
- `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureAcceptanceService.cs` — Recheck completed proof and revisions, then conditional acceptance.
- `src/PlayStead.Data/Database/Migrations/006_process_signature_discovery.sql` — Add paths, validation metadata and bounded learning persistence.
- `src/PlayStead.Data/Sessions/SqliteProcessSignatureLearningStore.cs` — Learning CAS and generation invalidation in one transaction.
- `src/PlayStead.Platform/Processes/Discovery/WindowsExecutableRevisionSource.cs` — Read a single trusted file revision, without enumeration.

### MODIFY

- `src/PlayStead.Core/Sessions/ProcessSignature.cs` — Append optional parent discovery metadata.
- `src/PlayStead.Core/Sessions/ProcessSignatureEntry.cs` — Append nullable confirmed path and validated revision.
- `src/PlayStead.Core/Sessions/ProcessSignatureMatcher.cs` — Exact path/name matching and legacy origin gating.
- `src/PlayStead.Core/Sessions/SessionRuntime.cs` — Optional validation gate and deferred Discovered recovery.
- `src/PlayStead.Data/Database/DatabaseInitializer.cs` — Target schema 6 and register migration 006.
- `src/PlayStead.Data/Sessions/SqliteProcessSignatureStore.cs` — Read new fields and own atomic discovery writes/consumption.

### TEST — CREATE

- `tests/PlayStead.Core.Tests/Sessions/ProcessSignatureValidationTests.cs` — Model compatibility and validation contracts.
- `tests/PlayStead.Data.Tests/Database/DiscoveryDatabaseFixture.cs` — Real embedded historical schemas and isolated SQLite fixtures; no production DB.
- `tests/PlayStead.Data.Tests/Database/DatabaseProcessSignatureDiscoveryMigrationTests.cs` — Schema 6 preservation, rollback and FK tests.
- `tests/PlayStead.Data.Tests/Sessions/SqliteProcessSignatureLearningStoreTests.cs` — Bounded state round-trip, CAS, lifecycle and rollback.
- `tests/PlayStead.Data.Tests/Sessions/SqliteProcessSignatureDiscoveryStoreTests.cs` — Atomic acceptance, revalidation, authority and concurrent writes.
- `tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutableRevisionSourceTests.cs` — Trusted targeted reads, cancellation and inaccessible paths.
- `tests/PlayStead.Core.Tests/Sessions/ProcessSignaturePathMatcherTests.cs` — Exact path and origin matrix.
- `tests/PlayStead.Core.Tests/Sessions/SessionRuntimeDiscoveredSignatureTests.cs` — Per-use validation and existing session semantics.
- `tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveredSignatureValidatorTests.cs` — Fresh inventory, revision and token validation.
- `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureLearningCoordinatorTests.cs` — Deterministic episode and quality transitions.
- `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureAcceptanceServiceTests.cs` — Proposal versus acceptance, cancellation and races.
- `tests/PlayStead.Data.Tests/Sessions/ProcessSignatureDiscoveryPipelineTests.cs` — Real SQLite restart, promotion and future sessions.

### TEST — MODIFY

- `tests/PlayStead.Data.Tests/Database/DatabaseInitializerTests.cs` — Current schema assertion 5 -> 6 only.
- `tests/PlayStead.Data.Tests/Database/DatabaseSteamEvidenceMigrationTests.cs` — Current version assertions; make the v1 fixture structurally complete.
- `tests/PlayStead.Data.Tests/Database/DatabaseSessionMigrationTests.cs` — Current version assertions; make the v2 fixture structurally complete.
- `tests/PlayStead.Data.Tests/Database/DatabaseSessionCorrectionMigrationTests.cs` — Current version assertions/names; complete v3 fixture while preserving observed-data assertions.
- `tests/PlayStead.Data.Tests/Database/Task08Fix01MigrationSafetyTests.cs` — Current version assertion; complete v4 fixture while retaining every correction assertion.

## Grounded decisions and shared contracts

1. B1 consists of immutable FileRevision, InstallationScope, ExecutableInventory, CandidateEpisodeEvidence, SnapshotRange and LearningEpisodeSummary, plus ProcessSignatureDiscoveryPolicy.Evaluate(DiscoveryEvaluation). Constructors on those B1 records use camelCase arguments. B1 has no public exclusion classifier; its inventory candidates are all plausible. B2 must not add filename exclusions to bypass that policy.
2. Existing ProcessSignature/Entry are positional records. Keep their existing positional parameters and append nullable parameters with defaults. File revision belongs to each entry, because it describes that executable; installation/generation/policy/state/token belong to the parent. This also preserves the legacy ability to have multiple explicit Main entries.
3. Current UpsertAsync unconditionally replaces parent and ordered entries. Retain its interface for explicit callers; automatic acceptance uses a new interface implemented by the same SqliteProcessSignatureStore. A legacy Discovered without metadata can only be inserted absent through UpsertAsync, for historical compatibility; it remains unmatchable. UpsertAsync cannot write a validated Discovered or overwrite any existing signature with Discovered. Manual can replace other origins; BuiltIn cannot replace Manual.
4. Current schema is 5. Several migration fixtures pretend to be v1-v4 while omitting tables introduced in those versions. Restore realistic fixture schemas through embedded historical migrations; retain seeded values and all preservation assertions. Do not make migration 006 silently repair an incomplete schema.
5. ProcessSignatureDiscoveryPolicy accepts a single finished summary and returns AwaitingIndependentEpisode if that summary qualifies. Use that result to retain a reference. Do not fabricate a duplicate episode to extract a candidate; do not reimplement Main selection.
6. Current episode is memory-only. Starting another episode leaves the completed Reference and any completed Confirmation persisted and unchanged; it does not write an empty state or reserve a durable episode sequence. Restart discards only the unfinished CurrentEpisode, reloads completed summaries and requires newly observed known absence. A mere start, shutdown or restart is neither a contradiction nor a confirming episode. An observed contradiction, invalidating capture-quality/continuity failure, or scope/generation/policy change clears the retained proof through CAS; no later success may reuse that invalidated reference. This lifecycle follows spec sections 10, 11, 13, 15 and 25.
7. Stored inventory is evidence, not proof of fresh startup validation. Every coordinator initialization and validator preparation receives or requests a fresh inventory; a null current inventory is Pending. Persisted Valid alone cannot authorize matching.
8. One optional runtime validator parameter is sufficient; no registration change. The default null validator leaves metadata-complete Discovered pending, and rejects legacy/NeedsRevalidation. Explicit signatures continue normally. A manually constructed validator in B2 tests supplies the capability that B3 will wire.
9. Guid tokens change on every successful state/signature mutation, including invalidation and explicit replacement of Discovered metadata. They prevent delete/reinsert ABA, unlike a counter reset to 1. Learning CAS and signature CAS are separate guards, both checked at acceptance.
10. The final task combines the executable end-to-end integration and the full gate. It is not an empty paperwork task.

---

## Task B2.1 — Compatible signature and validation models

**Files**

- MODIFY: `src/PlayStead.Core/Sessions/ProcessSignature.cs`
- MODIFY: `src/PlayStead.Core/Sessions/ProcessSignatureEntry.cs`
- CREATE: `src/PlayStead.Core/Sessions/ProcessSignatureValidationState.cs`
- CREATE: `src/PlayStead.Core/Sessions/DiscoveredSignatureMetadata.cs`
- TEST CREATE: `tests/PlayStead.Core.Tests/Sessions/ProcessSignatureValidationTests.cs`

**Interfaces**

Namespace PlayStead.Core.Sessions; use PlayStead.Core.Library.InstallationId and PlayStead.Core.Sessions.Discovery.FileRevision.

```csharp
public enum ProcessSignatureValidationState
{
    NeedsRevalidation = 0,
    Valid = 1
}

public sealed record DiscoveredSignatureMetadata(
    InstallationId? InstallationId,
    Guid? GenerationId,
    int? PolicyVersion,
    ProcessSignatureValidationState ValidationState,
    Guid ConcurrencyToken);

public sealed record ProcessSignatureEntry(
    string ExecutableName,
    ProcessSignatureEntryKind Kind,
    string? ExecutablePath = null,
    FileRevision? ValidatedRevision = null);

public sealed record ProcessSignature(
    Guid GameId,
    IReadOnlyList<ProcessSignatureEntry> Entries,
    ProcessSignatureOrigin Origin,
    DateTimeOffset UpdatedAtUtc,
    DiscoveredSignatureMetadata? Discovery = null);
```

These records represent readable legacy states; do not throw merely because a legacy Discovered lacks a path/metadata. Accepting a new Valid signature is stricter and enforced by B2.4. No derived bool equates Origin=Discovered to admissibility.

- [ ] **Step 1: RED**

Add named tests: Legacy_entry_defaults_to_no_path_or_revision; Legacy_signature_constructor_remains_compatible; Discovery_metadata_retains_scope_generation_policy_and_token; Validation_state_has_safe_zero_default; Path_and_revision_are_entry_specific; Explicit_entries_do_not_require_discovery_metadata. The last test uses both Manual and BuiltIn.

```csharp
[Fact]
public void Legacy_entry_defaults_to_no_path_or_revision()
{
    var entry = new ProcessSignatureEntry("Game.exe", ProcessSignatureEntryKind.Main);
    Assert.Null(entry.ExecutablePath);
    Assert.Null(entry.ValidatedRevision);
}
```

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureValidationTests"
```

Expected initial RED: missing ExecutablePath/ValidatedRevision/metadata API. Record exact compiler diagnostics. No production edit before observing it.

- [ ] **Step 3: GREEN minimal**

Implement exactly the signatures above, with existing file-scoped namespaces. Do not change IProcessSignatureStore, runtime or matcher yet.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureValidationTests"
```

Expected: all six named contracts, including both explicit origins, pass with no warnings/errors.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Sessions"
```

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

- [ ] **Step 7: Review**

Verify old constructors still compile, enum storage values are fixed, metadata is not duplicated on entries, and no newly accepted signature can be inferred merely from constructing these records. Review all new untracked content, which git diff alone omits.

- [ ] **Step 8: Commit**

Stage only this task's four production files and its test file. Check cached names/stat/check before commit; then verify clean status, exact commit files and unchanged stash.

```powershell
git add "src/PlayStead.Core/Sessions/ProcessSignature.cs" "src/PlayStead.Core/Sessions/ProcessSignatureEntry.cs" "src/PlayStead.Core/Sessions/ProcessSignatureValidationState.cs" "src/PlayStead.Core/Sessions/DiscoveredSignatureMetadata.cs" "tests/PlayStead.Core.Tests/Sessions/ProcessSignatureValidationTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(sessions): model discovered signature validation"
git status --short
git show --stat --oneline HEAD
git stash list
```

## Task B2.2 — Additive schema 006 and realistic migration tests

**Files**

- CREATE: `src/PlayStead.Data/Database/Migrations/006_process_signature_discovery.sql`
- MODIFY: `src/PlayStead.Data/Database/DatabaseInitializer.cs`
- TEST CREATE: `tests/PlayStead.Data.Tests/Database/DiscoveryDatabaseFixture.cs`
- TEST CREATE: `tests/PlayStead.Data.Tests/Database/DatabaseProcessSignatureDiscoveryMigrationTests.cs`
- TEST MODIFY: `tests/PlayStead.Data.Tests/Database/DatabaseInitializerTests.cs`
- TEST MODIFY: `tests/PlayStead.Data.Tests/Database/DatabaseSteamEvidenceMigrationTests.cs`
- TEST MODIFY: `tests/PlayStead.Data.Tests/Database/DatabaseSessionMigrationTests.cs`
- TEST MODIFY: `tests/PlayStead.Data.Tests/Database/DatabaseSessionCorrectionMigrationTests.cs`
- TEST MODIFY: `tests/PlayStead.Data.Tests/Database/Task08Fix01MigrationSafetyTests.cs`

**Interfaces**

Keep DatabaseInitializer(DatabaseOptions) and InitializeAsync(CancellationToken). TargetVersion becomes 6; add dictionary entry [6] = "006_process_signature_discovery.sql". The existing embedded-resource wildcard already includes the file.

Test fixture (namespace PlayStead.Data.Tests.Database) exposes:

```csharp
internal sealed class DiscoveryDatabaseFixture : IDisposable
{
    public DiscoveryDatabaseFixture();
    public DatabaseOptions Options { get; }
    public Task InitializeAsync(CancellationToken cancellationToken);
    public static Task CreateSchemaAsync(
        string databasePath, int version, CancellationToken cancellationToken);
    public Task SeedInstallationAsync(
        GameId gameId, InstallationId installationId, string rootPath,
        CancellationToken cancellationToken);
    public Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
    public void Dispose();
}
```

The fixture owns a GUID temp directory under Path.GetTempPath()/PlayStead.Tests/Discovery. Open uses Pooling=False and foreign_keys=ON. CreateSchemaAsync reads the actual embedded resources from typeof(DatabaseInitializer).Assembly, selects prefix 001 through the requested version in ordinal order, executes each in its own transaction and inserts schema_migrations with fixed UTC dates. It does not call current InitializeAsync to fake a historical version. Dispose closes connections/pools before deleting only the owned fixture root. SeedInstallationAsync inserts the game first and a real installations row (provider=0, external_id="fixture", preferred=0, present=1, sizes null), all mandatory timestamps fixed.

### SQL contract

No existing table rebuilt; existing entry ordinals and payloads remain intact. Add the following entry columns and new tables/index. Guid values use the existing "D" string format except tokens, which use lowercase "N" (32 hex characters). Timestamps use invariant UTC "O"; revision sizes are non-negative.

```sql
ALTER TABLE process_signature_entries ADD COLUMN executable_path TEXT NULL;
ALTER TABLE process_signature_entries ADD COLUMN validated_size_bytes INTEGER NULL
    CHECK (validated_size_bytes IS NULL OR validated_size_bytes >= 0);
ALTER TABLE process_signature_entries ADD COLUMN validated_last_write_utc TEXT NULL;

CREATE UNIQUE INDEX ux_installations_identity_game
    ON installations(installation_id, game_id);

CREATE TABLE process_signature_validation (
    game_id TEXT NOT NULL PRIMARY KEY,
    installation_id TEXT NULL,
    generation_id TEXT NULL,
    policy_version INTEGER NULL CHECK (policy_version IS NULL OR policy_version > 0),
    validation_state INTEGER NOT NULL DEFAULT 0 CHECK (validation_state IN (0, 1)),
    concurrency_token TEXT NOT NULL CHECK (length(concurrency_token) = 32),
    FOREIGN KEY (game_id) REFERENCES process_signatures(game_id) ON DELETE CASCADE,
    FOREIGN KEY (installation_id) REFERENCES installations(installation_id) ON DELETE SET NULL
);

CREATE INDEX ix_process_signature_validation_installation
    ON process_signature_validation(installation_id);

INSERT INTO process_signature_validation(game_id, validation_state, concurrency_token)
SELECT game_id, 0, lower(hex(randomblob(16)))
FROM process_signatures WHERE origin = 0;

CREATE TABLE process_signature_learning (
    installation_id TEXT NOT NULL PRIMARY KEY,
    game_id TEXT NOT NULL,
    root_path TEXT NOT NULL,
    generation_id TEXT NOT NULL,
    policy_version INTEGER NOT NULL CHECK (policy_version > 0),
    concurrency_token TEXT NOT NULL CHECK (length(concurrency_token) = 32),
    last_sequence_number INTEGER NOT NULL DEFAULT 0 CHECK (last_sequence_number >= 0),
    has_ambiguous_installation INTEGER NOT NULL CHECK (has_ambiguous_installation IN (0, 1)),
    inventory_json TEXT NOT NULL CHECK (json_valid(inventory_json)),
    reference_json TEXT NULL CHECK (reference_json IS NULL OR json_valid(reference_json)),
    confirmation_json TEXT NULL CHECK (confirmation_json IS NULL OR json_valid(confirmation_json)),
    reasons_json TEXT NOT NULL CHECK (json_valid(reasons_json)),
    CHECK (confirmation_json IS NULL OR reference_json IS NOT NULL),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (installation_id, game_id)
        REFERENCES installations(installation_id, game_id) ON DELETE CASCADE
);

CREATE INDEX ix_process_signature_learning_game
    ON process_signature_learning(game_id);

CREATE TRIGGER process_signature_validation_installation_deleted
AFTER UPDATE OF installation_id ON process_signature_validation
WHEN OLD.installation_id IS NOT NULL AND NEW.installation_id IS NULL
BEGIN
    UPDATE process_signature_validation
    SET validation_state = 0, concurrency_token = lower(hex(randomblob(16)))
    WHERE game_id = NEW.game_id;
END;
```

All new nullable columns default to NULL. Only validation_state and last_sequence_number have the defaults shown. No foreign key to a generation GUID: the learning row holds the current generation, whereas a suspended signature must retain its old generation for conditional revalidation. Metadata ownership/path/revision completeness is validated by the stores; SQL does not fabricate missing legacy values.

The composite FK prevents cross-game learning rows. Validation installation ownership is checked inside signature transactions; it remains nullable for historical discovery and deletion. Deleting an installation deletes its learning state and sets the signature's installation null/NeedsRevalidation without deleting the signature. Deleting a game cascades through existing signature/session/correction FKs and the new tables. No trigger changes origin, signature entries, sessions or corrections.

- [ ] **Step 1: RED**

In DatabaseProcessSignatureDiscoveryMigrationTests add:
Fresh_database_has_schema_6_and_discovery_constraints; V5_upgrade_preserves_all_signature_origins_and_entry_order; V5_upgrade_preserves_sessions_and_traceable_corrections; Legacy_discovered_is_unvalidated_without_invented_path; Explicit_legacy_signatures_have_no_discovery_metadata; Reinitialization_does_not_repeat_migration_or_backup; Intermediate_failure_rolls_back_006_and_restores_original_file; Installation_delete_cascades_learning_and_suspends_discovered; Game_delete_cascades_new_and_existing_dependents; Learning_row_cannot_reference_another_games_installation.

Use real v5 schema from the fixture, seed all three origins with multiple differently ordered Main/Auxiliary/Excluded entries, two simultaneous sessions, ended sessions and both one-sided/two-sided corrections. Snapshot every old column before/after, ordered by stable keys/ordinal. Inject intermediate failure by precreating process_signature_learning in a v5 fixture: 006 must fail after the ALTER statements, roll back those columns and leave MAX(version)=5; verify the existing backup/restore exact-byte contract after closing all connections. Separately execute the embedded 006 in a transaction against that incompatible fixture, roll it back and query on a fresh connection, so backup restoration alone cannot disguise nontransactional DDL.

```csharp
[Fact]
public async Task Fresh_database_has_schema_6_and_discovery_constraints()
{
    using var fixture = new DiscoveryDatabaseFixture();
    await fixture.InitializeAsync(CancellationToken.None);
    await using var connection = await fixture.OpenAsync(CancellationToken.None);
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT MAX(version) FROM schema_migrations;";
    Assert.Equal(6, Convert.ToInt32(await command.ExecuteScalarAsync()));
}
```

Update existing current-version assertions to 6; rename the two correction-migration test names mentioning target v5 to current_schema. In each legacy fixture replace only handcrafted DDL/version insertion with CreateSchemaAsync at its real v1/v2/v3/v4 version; retain its data INSERTs and all existing assertions. The five modified test files remain in the map; no historical runtime/correction test is relaxed.

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Database"
```

Expected behavior RED: schema remains 5 / new columns or tables absent. The tests must compile. A malformed historical fixture is a test fault and must be corrected before changing production.

- [ ] **Step 3: GREEN minimal**

Add exactly the SQL above and initializer version/registration. Retain existing transaction, backup and restore flow. New store connections and fixture connections enable foreign keys before transactions; do not broaden this task into rewriting the initializer's historical connection policy. Exercise the FK assertions on connections with foreign_keys=ON. The target schema is only marked 6 after its transaction commits.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Database"
```

Expected: all new schema/preservation tests and historical database tests pass; no warning/error.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore
```

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
git diff -- src/PlayStead.Data/Database/Migrations/001_initial.sql src/PlayStead.Data/Database/Migrations/002_steam_evidence.sql src/PlayStead.Data/Database/Migrations/003_sessions.sql src/PlayStead.Data/Database/Migrations/004_session_corrections.sql src/PlayStead.Data/Database/Migrations/005_session_corrections_traceable.sql
```

The historical migration diff must be empty.

- [ ] **Step 7: Review**

Review fresh/v5 preservation, DDL rollback independent of backup, identity FKs, installation deletion, and no synthetic path or Valid legacy row. Confirm the fixture corrections make schemas realistic rather than weaken preservation checks.

- [ ] **Step 8: Commit**

```powershell
git add "src/PlayStead.Data/Database/Migrations/006_process_signature_discovery.sql" "src/PlayStead.Data/Database/DatabaseInitializer.cs" "tests/PlayStead.Data.Tests/Database/DiscoveryDatabaseFixture.cs" "tests/PlayStead.Data.Tests/Database/DatabaseProcessSignatureDiscoveryMigrationTests.cs" "tests/PlayStead.Data.Tests/Database/DatabaseInitializerTests.cs" "tests/PlayStead.Data.Tests/Database/DatabaseSteamEvidenceMigrationTests.cs" "tests/PlayStead.Data.Tests/Database/DatabaseSessionMigrationTests.cs" "tests/PlayStead.Data.Tests/Database/DatabaseSessionCorrectionMigrationTests.cs" "tests/PlayStead.Data.Tests/Database/Task08Fix01MigrationSafetyTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(data): add process signature discovery schema"
git status --short
git show --stat --oneline HEAD
git stash list
```


## Task B2.3 — Bounded learning persistence and generation CAS

**Files**

- CREATE: `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureLearningState.cs`
- CREATE: `src/PlayStead.Core/Sessions/Discovery/IProcessSignatureLearningStore.cs`
- CREATE: `src/PlayStead.Data/Sessions/SqliteProcessSignatureLearningStore.cs`
- TEST CREATE: `tests/PlayStead.Data.Tests/Sessions/SqliteProcessSignatureLearningStoreTests.cs`
- TEST USE unchanged: `tests/PlayStead.Data.Tests/Database/DiscoveryDatabaseFixture.cs`

**Interfaces**

Core namespace PlayStead.Core.Sessions.Discovery; immutable state constructor and readable properties are:

```csharp
public sealed record ProcessSignatureLearningState
{
    public ProcessSignatureLearningState(
        ExecutableInventory inventory, int policyVersion, Guid concurrencyToken,
        long lastSequenceNumber, bool hasAmbiguousInstallation,
        LearningEpisodeSummary? reference, LearningEpisodeSummary? confirmation,
        IReadOnlyList<DiscoveryReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(reasons);
        if (policyVersion <= 0) throw new ArgumentOutOfRangeException(nameof(policyVersion));
        if (concurrencyToken == Guid.Empty) throw new ArgumentException("Token is required.", nameof(concurrencyToken));
        if (lastSequenceNumber < 0) throw new ArgumentOutOfRangeException(nameof(lastSequenceNumber));
        if (confirmation is not null && reference is null)
            throw new ArgumentException("Confirmation requires a reference.", nameof(confirmation));
        Inventory = inventory;
        PolicyVersion = policyVersion;
        ConcurrencyToken = concurrencyToken;
        LastSequenceNumber = lastSequenceNumber;
        HasAmbiguousInstallation = hasAmbiguousInstallation;
        Reference = reference;
        Confirmation = confirmation;
        Reasons = Array.AsReadOnly(reasons.ToArray());
    }
    public ExecutableInventory Inventory { get; }
    public int PolicyVersion { get; }
    public Guid ConcurrencyToken { get; }
    public long LastSequenceNumber { get; }
    public bool HasAmbiguousInstallation { get; }
    public LearningEpisodeSummary? Reference { get; }
    public LearningEpisodeSummary? Confirmation { get; }
    public IReadOnlyList<DiscoveryReason> Reasons { get; }
}

public interface IProcessSignatureLearningStore
{
    Task<ProcessSignatureLearningState?> LoadAsync(
        InstallationId installationId, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(
        ProcessSignatureLearningState state, Guid? expectedConcurrencyToken,
        CancellationToken cancellationToken);
}
```

Data namespace PlayStead.Data.Sessions:
SqliteProcessSignatureLearningStore(DatabaseOptions options) implements that interface. Store false means absent/conflicting row or stale DB installation identity, with no mutation; malformed supplied DTOs throw ArgumentException. Database/cancellation failures propagate. Constructor guards null.

### Persistence contract

- Each installation has one row, one current generation, a maximum of two summary columns; a new generation replaces that row, not a new historical row.
- inventory_json is System.Text.Json serialization of the actual B1 ExecutableInventory, retaining ordered Candidates, FileRevision, Scope, Completeness and Issues. reference_json/confirmation_json serialize the exact B1 LearningEpisodeSummary including EpisodeId, sequence, bounds, ranges, quality and scope. reasons_json stores the last structured decision reasons, enum integers. No PID list, unknown-process name/path, raw observation batch or running episode is serialized.
- Use default case-sensitive property names, no permissive enum-string converter. On load deserialize through B1 constructors; verify SQL identity/generation/root/policy fields agree with the JSON and summaries. Invalid JSON or inconsistent stored identity throws InvalidDataException and never returns usable evidence. No silent fallback to a complete empty inventory.
- Scope and summary identities, generation, policy, candidate membership/revisions and ordered interval bounds must agree. Confirmation requires distinct EpisodeIds, adjacent episode sequence numbers, strictly later StartedAtUtc and LastSequenceNumber equal to its sequence. A reference-only row uses its reference sequence as LastSequenceNumber; an empty row may retain the sequence of the last finalized, invalidated or consumed attempt. Starting CurrentEpisode assigns a tentative next sequence in memory only. Discarding it at restart neither changes LastSequenceNumber nor creates an artificial gap between the retained reference and a later completed confirmation.
- Full inventories compare root/presence, ordered canonical candidate paths/names/revisions, completeness and issues by value; path/name comparisons use OrdinalIgnoreCase. A changed inventory cannot reuse the old generation GUID. A changed policy or ambiguity clears both summaries even if inventory contents did not change. A caller can conservatively supply a new generation for unchanged contents; it also clears proof.
- Under a non-deferred SQLite write transaction, read the existing learning row, compare expected token, check installations.game_id/install_path/is_present against supplied Scope, then INSERT absent or UPDATE with the expected token in WHERE. An insert uses expected=null; an update requires a nonempty matching token and a new proposed token.
- Check the actual installation row within the transaction. Missing/moved/reassigned installation returns false; a deliberately absent Scope can be saved only when the DB installation is absent too. No stale scan result can overwrite a newer token.
- On inventory/generation/policy/ambiguity invalidation, clear reference/confirmation and set a same-game Discovered metadata row to NeedsRevalidation with a fresh signature token in the same transaction, guarded by origin=0. Preserve paths/revisions and all explicit signatures. A completed-summary save with unchanged inventory does not invalidate an accepted signature. Opening CurrentEpisode performs no learning-state write: Reference, Confirmation, LastSequenceNumber and the learning token remain unchanged until a completed result or a real invalidating transition is persisted.
- Cancellation checked before opening, after async reads and before commit. No disk inventory inside the transaction.
- Explicitly use non-deferred transactions (connection.BeginTransaction(deferred: false)); assign each command.Transaction. A stale competing writer returns false after acquiring the lock and checking the predicate. A genuine database lock/IO timeout propagates; no catch-and-upsert retry.

- [ ] **Step 1: RED**

Add tests named:
Absent_state_loads_null; Reference_round_trips_inventory_order_revisions_and_reasons; Confirmation_round_trips_both_exact_summaries; New_store_instance_loads_completed_state; State_copies_reason_collection; Confirmation_without_reference_is_rejected; Mismatched_summary_scope_is_rejected; Duplicate_or_nonadjacent_confirmation_is_rejected; Invalidated_state_retains_no_process_data; Save_requires_expected_token; Concurrent_state_saves_have_one_winner; Changed_inventory_requires_new_generation; Generation_change_clears_proof_and_invalidates_discovered; Policy_change_clears_proof; Ambiguity_change_clears_proof; Manual_and_builtin_are_untouched_by_invalidation; Stale_installation_identity_rejects_save; Installation_deletion_removes_learning; Failed_state_update_rolls_back_signature_invalidation; Corrupt_json_is_not_returned_as_evidence; Only_two_summary_slots_are_retained; Unchanged_state_is_not_written_by_load.

For the reference round-trip build a real one-candidate B1 summary, not a mocked JSON string:

```csharp
var game = new GameId(Guid.NewGuid());
var installation = new InstallationId(Guid.NewGuid());
var scope = new InstallationScope(game, installation, @"C:\Games\Example", Guid.NewGuid(), true);
var t0 = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
var candidate = new ExecutableCandidate(@"C:\Games\Example\Game.exe",
    "Game.exe", new FileRevision(10, t0));
var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete, [candidate], []);
var reference = new LearningEpisodeSummary(Guid.NewGuid(), 1, scope,
    ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, t0, t0.AddSeconds(10),
    1, 6, EpisodeQuality.Complete,
    [new CandidateEpisodeEvidence(candidate.ExecutablePath, candidate.Revision, true, true,
        [new SnapshotRange(3, 4)])]);
var expected = new ProcessSignatureLearningState(inventory, 1, Guid.NewGuid(), 1,
    false, reference, null, [DiscoveryReason.AwaitingIndependentEpisode]);
using var fixture = new DiscoveryDatabaseFixture();
await fixture.InitializeAsync(CancellationToken.None);
await fixture.SeedInstallationAsync(game, installation, scope.RootPath, CancellationToken.None);
var store = new SqliteProcessSignatureLearningStore(fixture.Options);
Assert.True(await store.TrySaveAsync(expected, null, CancellationToken.None));
var actual = await new SqliteProcessSignatureLearningStore(fixture.Options)
    .LoadAsync(installation, CancellationToken.None);
Assert.NotNull(actual);
Assert.Equal(reference.EpisodeId, actual.Reference!.EpisodeId);
Assert.Equal(candidate.Revision, actual.Inventory.Candidates[0].Revision);
Assert.Equal(new SnapshotRange(3, 4), actual.Reference.Candidates[0].PresenceRanges[0]);
```

Use SQLite triggers with RAISE(ABORT, 'test failure') on a fixture learning UPDATE to inject rollback; compare learning and validation rows before/after. Seed a legacy discovered parent/metadata through fixture SQL because B2.4 acceptance is not available yet. Test concurrent CAS with two separate store instances and a barrier before entering their writes, not sleeps.

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SqliteProcessSignatureLearningStoreTests"
```

Expected first RED: missing state/store API. After minimal API exists, all assertions must exercise actual SQLite state, not source strings.

- [ ] **Step 3: GREEN minimal**

Implement the state and interface above. Store SQL for the conditional update uses:

```sql
UPDATE process_signature_learning
SET game_id=$gameId, root_path=$root, generation_id=$generation,
    policy_version=$policy, concurrency_token=$newToken,
    last_sequence_number=$sequence, has_ambiguous_installation=$ambiguous,
    inventory_json=$inventory, reference_json=$reference,
    confirmation_json=$confirmation, reasons_json=$reasons
WHERE installation_id=$installation AND concurrency_token=$expectedToken;
```

For absent insertion use INSERT ... ON CONFLICT(installation_id) DO NOTHING with the same explicit columns. After exactly one affected row, apply required origin-guarded validation invalidation and commit. On zero rows return false and roll back. Validate serialized state before writing; preserve reference ordering exactly. Implement serialization inside this store, without generic persistence infrastructure or a new package.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SqliteProcessSignatureLearningStoreTests"
```

Expected: every named round-trip, bound, CAS and invalidation assertion passes.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Sessions"
```

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

- [ ] **Step 7: Review**

Verify max two summaries, constructor-safe deserialization, value comparisons instead of record/list reference equality, same-transaction invalidation, foreign keys enabled, no process-tick writer and no protected-origin mutation.

- [ ] **Step 8: Commit**

```powershell
git add "src/PlayStead.Core/Sessions/Discovery/ProcessSignatureLearningState.cs" "src/PlayStead.Core/Sessions/Discovery/IProcessSignatureLearningStore.cs" "src/PlayStead.Data/Sessions/SqliteProcessSignatureLearningStore.cs" "tests/PlayStead.Data.Tests/Sessions/SqliteProcessSignatureLearningStoreTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(sessions): persist bounded discovery learning state"
git status --short
git show --stat --oneline HEAD
git stash list
```

## Task B2.4 — Conditional acceptance, authority and revalidation

**Files**

- CREATE: `src/PlayStead.Core/Sessions/Discovery/DiscoveredSignatureWrite.cs`
- CREATE: `src/PlayStead.Core/Sessions/Discovery/IProcessSignatureDiscoveryStore.cs`
- MODIFY: `src/PlayStead.Data/Sessions/SqliteProcessSignatureStore.cs`
- TEST CREATE: `tests/PlayStead.Data.Tests/Sessions/SqliteProcessSignatureDiscoveryStoreTests.cs`

**Interfaces**

Namespace PlayStead.Core.Sessions.Discovery:

```csharp
public sealed record DiscoveredSignatureExpectation(
    Guid ConcurrencyToken,
    Guid? GenerationId,
    ProcessSignatureValidationState ValidationState,
    InstallationId? InstallationId);

public sealed record DiscoveredSignatureWrite(
    ProcessSignature Signature,
    Guid ExpectedLearningToken,
    Guid ReferenceEpisodeId,
    Guid ConfirmationEpisodeId);

public interface IProcessSignatureDiscoveryStore
{
    Task<bool> TryInsertDiscoveredIfAbsentAsync(
        DiscoveredSignatureWrite write, CancellationToken cancellationToken);
    Task<bool> TryRevalidateDiscoveredAsync(
        DiscoveredSignatureWrite write, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken);
    Task<bool> TryInvalidateDiscoveredAsync(
        Guid gameId, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken);
}
```

The two request records share one file because they are only the conditional-write contract. SqliteProcessSignatureStore(DatabaseOptions) implements the new interface in addition to the unchanged IProcessSignatureStore. Return true means committed, false means predicate conflict without partial mutation; invalid request throws ArgumentException; cancellation/SQLite failures propagate.

### Atomic contract

Before transaction: validate nonempty IDs/tokens, Origin=Discovered, metadata Valid with non-null installation/generation/policy, exactly one entry with Kind=Main, nonempty path and non-null FileRevision. No Auxiliary/Excluded produced by automatic acceptance. Reject a supplied signature token equal to the expected old token. Data checks proof identity and shape; it does not run the policy or choose Main.

Inside one non-deferred SQLite write transaction:

1. Load the current learning row with ExpectedLearningToken; require complete, present, nonambiguous inventory, current B1 policy version, exact request generation and matching game/installation. Require the stored two EpisodeIds and adjacent sequences from the request; both must have complete quality and agree with inventory/scope/policy. Check Main path/name/revision is an exact inventory candidate and occurs in both summaries. Selection correctness is the Core policy's responsibility.
2. Check the current installations row still has that GameId, root and present=1. Read present installation roots in the same transaction and reject overlapping/equal roots with Windows OrdinalIgnoreCase boundary comparison; never resolve ambiguity by is_preferred. The persisted ambiguity flag cannot overrule the actual DB guard.
3. INSERT path: INSERT INTO process_signatures ... ON CONFLICT(game_id) DO NOTHING. If zero rows, return false. Existing Discovered also yields false; it is not silently revalidated.
4. Revalidation path: update only origin=Discovered and metadata matching old token, old generation (null-safe IS), old state and old installation (null-safe IS). The expected state must be NeedsRevalidation. This permits legacy null scope metadata to be replaced only with fresh complete proof. A previously Valid row must first be explicitly suspended; do not silently overwrite it with different learning evidence.
5. Write parent timestamp, new validation metadata/token and ordered entries. Existing metadata is replaced only after the conditional update succeeded; explicit-origin writes never pass this predicate.
6. Consume proof with a token-qualified learning UPDATE that sets reference_json/confirmation_json=NULL, reasons_json='[]' and rotates the learning token, preserving inventory and LastSequenceNumber. Exactly one row must be affected; otherwise roll back the entire acceptance.
7. Check cancellation and commit. A crash/failure before commit shows neither partial entries nor consumed evidence. No filesystem call is made under this transaction.

Key revalidation predicate (all parameters bound, not interpolated):

```sql
UPDATE process_signatures
SET updated_at_utc=$updatedUtc
WHERE game_id=$gameId AND origin=0
AND EXISTS (
    SELECT 1 FROM process_signature_validation v
    WHERE v.game_id=process_signatures.game_id
      AND v.concurrency_token=$expectedSignatureToken
      AND v.generation_id IS $expectedGeneration
      AND v.validation_state=$expectedState
      AND v.installation_id IS $expectedInstallation
);
```

TryInvalidateDiscoveredAsync uses the same exact expectation/origin predicate, sets only metadata state=NeedsRevalidation and a new token, and clears any same-game completed learning proof/rotates its token in the same transaction. Keep entry paths/revisions and parent origin. No mutation if the expectation is stale or replaced by Manual/BuiltIn. Calling it for an already NeedsRevalidation record is unnecessary; consumers do not perform repeated invalidation writes.

Read GetAsync/GetAllAsync parent/metadata/entries within a single read transaction to avoid torn signatures during replacement. Map Guid tokens with ParseExact("N"); absent metadata for a Discovered is exposed as ineligible, never as Valid. Migration-created metadata round-trips nullable fields. Read entry revision only if both size/date exist; inconsistent half revisions throw InvalidDataException.

Explicit UpsertAsync:
- Manual may replace any origin and clears discovery metadata.
- BuiltIn inserts/replaces unless current origin is Manual; use a guarded SQL update inside its transaction and throw InvalidOperationException on protected conflict.
- Discovered with non-null metadata is rejected: validated writes require the new API.
- Legacy Discovered with null metadata is allowed only when no parent exists; initialize NeedsRevalidation metadata/token. A collision throws InvalidOperationException without touching the current entries. This preserves the historical Discovered-then-Manual fixture in SqliteProcessSignatureStoreTests.
- On explicit replacement, clear old learning summaries for the game and rotate their tokens so a stale proposal cannot survive an intervening edit. Never mutate observed sessions or corrections.

- [ ] **Step 1: RED**

Add named tests:
Insert_absent_discovered_consumes_proof_atomically; Insert_when_manual_exists_has_no_effect; Insert_when_builtin_exists_has_no_effect; Insert_when_discovered_exists_returns_conflict; Two_concurrent_inserts_have_exactly_one_winner; Manual_inserted_after_stale_read_wins; Builtin_inserted_after_stale_read_wins; Failure_after_parent_insert_rolls_back_parent_entries_and_proof; Revalidation_replaces_only_expected_discovered; Revalidation_stale_token_conflicts; Revalidation_changed_generation_conflicts; Revalidation_changed_state_conflicts; Revalidation_changed_installation_conflicts; Revalidation_replaced_by_manual_conflicts; Revalidation_replaced_by_builtin_conflicts; Two_concurrent_revalidations_have_one_winner; Learning_token_changed_before_acceptance_conflicts; Changed_db_root_or_presence_conflicts; Overlapping_db_installations_prevent_acceptance; Failed_revalidation_leaves_no_partial_entries; Invalidation_keeps_path_and_revision_but_rotates_token; Stale_invalidation_cannot_suspend_manual; Builtin_upsert_cannot_replace_manual; Legacy_discovered_upsert_cannot_replace_authority; Validated_discovered_cannot_use_upsert; Reads_round_trip_path_revision_metadata_and_order; Concurrent_read_never_combines_old_entries_with_new_metadata.

For concurrency create independent store instances on one fixture DB, create the same completed pair with TrySaveAsync, and synchronize start with TaskCompletionSource configured RunContinuationsAsynchronously. Invoke each write on separate Task.Run workers because SQLite async APIs may complete synchronously. Assert sorted results exactly [false,true], one parent/Main, and both proof slots consumed only once. Do not use delays as ordering assertions.

For stale-read tests explicitly await store.GetAsync (null), commit Manual/BuiltIn with the other store, then attempt discovery using the stale proposal. Compare all original columns and entry ordering after false.

```csharp
var before = await signatureStore.GetAsync(write.Signature.GameId, CancellationToken.None);
Assert.Null(before);
await signatureStore.UpsertAsync(
    new ProcessSignature(write.Signature.GameId,
        [new ProcessSignatureEntry("Chosen.exe", ProcessSignatureEntryKind.Main)],
        ProcessSignatureOrigin.Manual, write.Signature.UpdatedAtUtc),
    CancellationToken.None);
Assert.False(await discoveryStore.TryInsertDiscoveredIfAbsentAsync(write, CancellationToken.None));
var actual = await signatureStore.GetAsync(write.Signature.GameId, CancellationToken.None);
Assert.Equal(ProcessSignatureOrigin.Manual, actual!.Origin);
Assert.Equal("Chosen.exe", Assert.Single(actual.Entries).ExecutableName);
```

Here signatureStore and discoveryStore are the two interfaces of SqliteProcessSignatureStore. write is a DiscoveredSignatureWrite built from the actual two stored B1 summaries and their token. Use fixture-local helpers returning that exact record; no mocked transaction implementation.

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SqliteProcessSignatureDiscoveryStoreTests"
```

Expected first RED: missing conditional API; behavioral REDs include unsafe explicit overwrite and missing path/metadata persistence. Record each before its minimal fix.

- [ ] **Step 3: GREEN minimal**

Implement the interfaces/records and the transaction sequence above in the existing signature store. Use private connection/transaction-aware read/write helpers within that file to avoid opening a second connection during proof consumption. No caller-level check-then-Upsert. No migration, session-store or correction-store changes in this task.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SqliteProcessSignatureDiscoveryStoreTests"
```

Expected: all authority/race/rollback/round-trip tests pass with no SQLite lock flakiness.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Sessions"
```

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

- [ ] **Step 7: Review**

Inspect the actual SQL predicate and transaction boundaries, not just green counts. Verify Manual/BuiltIn races, null-safe legacy expectations, proof consumption rollback, token changes, snapshot reads, and preservation of the historical signature-store tests.

- [ ] **Step 8: Commit**

```powershell
git add "src/PlayStead.Core/Sessions/Discovery/DiscoveredSignatureWrite.cs" "src/PlayStead.Core/Sessions/Discovery/IProcessSignatureDiscoveryStore.cs" "src/PlayStead.Data/Sessions/SqliteProcessSignatureStore.cs" "tests/PlayStead.Data.Tests/Sessions/SqliteProcessSignatureDiscoveryStoreTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(sessions): accept discovered signatures atomically"
git status --short
git show --stat --oneline HEAD
git stash list
```


## Task B2.5 — Targeted trusted revision reader

**Files**

- CREATE: `src/PlayStead.Core/Sessions/Discovery/ExecutableRevisionResult.cs`
- CREATE: `src/PlayStead.Core/Sessions/Discovery/IExecutableRevisionSource.cs`
- CREATE: `src/PlayStead.Platform/Processes/Discovery/WindowsExecutableRevisionSource.cs`
- TEST CREATE: `tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutableRevisionSourceTests.cs`
- TEST USE unchanged: `tests/PlayStead.Platform.Tests/Processes/Discovery/ExecutableInventoryTestDirectory.cs`

**Interfaces**

```csharp
// Namespace PlayStead.Core.Sessions.Discovery
public sealed record ExecutableRevisionResult(
    FileRevision? Revision, InventoryIssue? Issue);

public interface IExecutableRevisionSource
{
    Task<ExecutableRevisionResult> ReadAsync(
        InstallationScope scope, string executablePath,
        CancellationToken cancellationToken);
}

// Namespace PlayStead.Platform.Processes.Discovery
// WindowsExecutableRevisionSource implements IExecutableRevisionSource.
// Constructor signatures:
public WindowsExecutableRevisionSource();
public WindowsExecutableRevisionSource(
    Func<string, FileAttributes> readAttributes,
    Func<string, FileRevision> readRevision);
```

Result invariant: exactly one of Revision/Issue is non-null; constructors/consumers reject impossible results. The expected revision stays in ExecutableCandidate.Revision or ProcessSignatureEntry.ValidatedRevision; the reader returns the current revision without deciding equality or Main.

- [ ] **Step 1: RED**

Add tests:
Targeted_read_returns_size_and_utc_timestamp; Changed_file_returns_new_revision; Missing_file_is_explicit_failure; Access_denied_is_explicit_failure; Io_failure_is_explicit_failure; Root_boundary_rejects_sibling_prefix; Relative_or_traversal_path_is_rejected; File_reparse_point_is_rejected; Ancestor_reparse_point_is_rejected; Absent_scope_performs_no_file_read; Cancellation_before_or_during_read_propagates; Reader_touches_only_target_and_ancestor_attributes; Directory_in_place_of_executable_is_rejected; Programming_error_propagates.

Use real temp files for size/time and delegates for deterministic access denial/cancellation. No executable is launched. The read-count test records each delegate call and proves exactly one revision read for the specified target and no sibling/recurse operation.

```csharp
var visited = new List<string>();
var revision = new FileRevision(12, DateTimeOffset.UnixEpoch);
var sut = new WindowsExecutableRevisionSource(
    path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        ? FileAttributes.Normal : FileAttributes.Directory,
    path => { visited.Add(path); return revision; });
var scope = new InstallationScope(new GameId(Guid.NewGuid()),
    new InstallationId(Guid.NewGuid()), @"C:\Games\Example", Guid.NewGuid(), true);
var result = await sut.ReadAsync(scope, @"C:\Games\Example\Game.exe", CancellationToken.None);
Assert.Equal(revision, result.Revision);
Assert.Null(result.Issue);
Assert.Equal(new[] { @"C:\Games\Example\Game.exe" }, visited);
```

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~WindowsExecutableRevisionSourceTests"
```

Expected: missing targeted reader API; after the API exists, exercise the filesystem failure assertions.

- [ ] **Step 3: GREEN minimal**

Use WindowsExecutablePath.NormalizeRoot and IsStrictlyUnderRoot; refuse invalid paths and reparse points on every ancestor from volume through target parent and on the file. Validate IsPresent before any I/O. Check cancellation between attribute reads and before/after file revision read. Recheck ancestor/file attributes after the read to refuse a newly visible reparse point. Do not enumerate directories or call InventoryAsync.

The default revision delegate opens the target with FileMode.Open/FileAccess.Read/FileShare.ReadWrite|Delete, reads stream.Length and FileInfo.LastWriteTimeUtc, mirroring the existing B1 implementation. Expected UnauthorizedAccessException/FileNotFoundException/DirectoryNotFoundException/IOException becomes InventoryIssue (AccessDenied/MissingRoot for missing scope root/IoFailure for missing file); malformed root -> InvalidRoot, out-of-root -> EscapedRoot, reparse -> ReparsePoint. OperationCanceledException and programming errors propagate. No hash, PE metadata or package. Keep the small reader self-contained; do not refactor the B1 inventory implementation to share private helpers.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~WindowsExecutableRevisionSourceTests"
```

Expected: all trusted-target, failure and cancellation assertions pass.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Discovery"
```

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

- [ ] **Step 7: Review**

This is the only Platform production addition in B2. Verify no enumeration, hash or execution; root/ancestor checks and cancellation do not swallow failures. State the residual filesystem race and size/date limitation; do not claim a security fingerprint.

- [ ] **Step 8: Commit**

```powershell
git add "src/PlayStead.Core/Sessions/Discovery/ExecutableRevisionResult.cs" "src/PlayStead.Core/Sessions/Discovery/IExecutableRevisionSource.cs" "src/PlayStead.Platform/Processes/Discovery/WindowsExecutableRevisionSource.cs" "tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutableRevisionSourceTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(platform): read targeted executable revisions"
git status --short
git show --stat --oneline HEAD
git stash list
```

## Task B2.6 — Path matching and runtime validation capability

**Files**

- CREATE: `src/PlayStead.Core/Sessions/IDiscoveredSignatureValidator.cs`
- CREATE: `src/PlayStead.Core/Sessions/Discovery/DiscoveryInventoryContext.cs`
- CREATE: `src/PlayStead.Core/Sessions/Discovery/DiscoveredSignatureValidator.cs`
- MODIFY: `src/PlayStead.Core/Sessions/ProcessSignatureMatcher.cs`
- MODIFY: `src/PlayStead.Core/Sessions/SessionRuntime.cs`
- TEST CREATE: `tests/PlayStead.Core.Tests/Sessions/ProcessSignaturePathMatcherTests.cs`
- TEST CREATE: `tests/PlayStead.Core.Tests/Sessions/SessionRuntimeDiscoveredSignatureTests.cs`
- TEST CREATE: `tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveredSignatureValidatorTests.cs`

**Interfaces**

```csharp
// Namespace PlayStead.Core.Sessions
public enum DiscoveredSignatureValidationResult { Pending = 0, Valid = 1, Invalid = 2 }
public interface IDiscoveredSignatureValidator
{
    Task<DiscoveredSignatureValidationResult> ValidateAsync(
        ProcessSignature signature, CancellationToken cancellationToken);
}

// Namespace PlayStead.Core.Sessions.Discovery
public sealed record DiscoveryInventoryContext(
    ExecutableInventory Inventory, bool HasAmbiguousInstallation);

// DiscoveredSignatureValidator implements IDiscoveredSignatureValidator.
// Constructor:
public DiscoveredSignatureValidator(
    IProcessSignatureStore signatureStore,
    IProcessSignatureLearningStore learningStore,
    IProcessSignatureDiscoveryStore discoveryStore,
    IExecutableRevisionSource revisionSource,
    Func<InstallationId, DiscoveryInventoryContext?> currentInventory);

// Existing SessionRuntime constructor: append only this optional parameter
// after TimeProvider timeProvider:
IDiscoveredSignatureValidator? discoveredSignatureValidator = null
```

Keep Match(ProcessSignature, IReadOnlyCollection<ProcessSnapshot>) and RefreshAsync(CancellationToken) signatures. The new callback is a read-only view of caller-supplied freshly prepared inventory, not a new poller or persistent inventory. B2 tests supply it explicitly. Nothing registers this validator in production.

### Matching and validation rules

- With an entry path, require both full path and executable name to match using StringComparison.OrdinalIgnoreCase. Null/blank/different observed path never falls back to name, including explicit path-aware entries.
- Without an entry path, only Manual/BuiltIn may use legacy name matching. Discovered without Valid metadata, installation/generation/policy/token, path or revision is not matchable. Unknown enum/policy versions are not admissible.
- Keep role precedence Excluded > Main > Auxiliary among entries that actually match the process. Do not collapse all homonymous entries by name before testing paths: an Excluded in another directory cannot suppress a Main at its confirmed path.
- Core does not canonicalize via filesystem or normalize untrusted traversal into a match. Platform supplies canonical identities.
- Pure matcher checks stored structural admissibility; it does not read disk. Runtime must additionally call the validator before using a Discovered.
- Validator rejects legacy and NeedsRevalidation immediately. For complete Valid metadata, currentInventory(installation)==null returns Pending without invalidation writes. Otherwise compare fresh inventory with the persisted generation/inventory, policy, presence, ambiguity and ownership. Missing/deleted/inconsistent persisted state returns Invalid.
- Validate every discovered entry's expected revision through ReadAsync before matching. Revision mismatch/inaccessibility or fresh scope/generation/policy mismatch calls TryInvalidateDiscoveredAsync with the original exact expectation, then returns Invalid even if the suspension CAS conflicts. Never grant Valid to stale metadata.
- After awaited reads reload the authoritative signature and learning token; if either changed, return Invalid for this stale attempt. Next refresh loads current authority; no blind retry. No valid cache survives a restart without fresh inventory.
- Cancellation propagates without converting Pending or a read failure into process absence.

### Runtime change boundaries

Read signatures and resolve validation before the refresh's single CaptureAsync, so resolution of a deferred startup gate is followed by a fresh capture. Use the current TimeProvider instant for transitions after that work. No coordinator, InventoryAsync, discovery store acceptance or second capture is invoked by runtime.

For Discovered Pending: remove any unconfirmed start candidate; do not match, start, heartbeat, persist a heartbeat or close its active session. Track persisted recovery identities until their first non-Pending result; the current local recoveredThisRefresh set alone is insufficient across multiple pending refreshes. A private HashSet<Guid> of unresolved recovered game IDs is enough; load persisted active sessions once as before.

On Valid: apply existing exact-match and transition rules on the fresh capture. A new session still needs two future observations; a persisted active session can recover under existing rules. Do not manufacture learning-time sessions or advance timestamps during Pending. On Invalid: close a previously active session at its last reliable LastSeenAtUtc (last persisted instant for a recovered session), using existing recovery/closure reasons. Explicit sessions proceed even while another game's validation is Pending. Keep cancellation recovery identity until resolution.

A null validator means Pending for structurally complete Valid Discovered, Invalid for legacy/NeedsRevalidation; it can never mean permission to skip revision checks. This safely provides capability without automatic discovery activation.

- [ ] **Step 1: RED**

ProcessSignaturePathMatcherTests:
Exact_path_and_name_match; Same_name_in_other_directory_does_not_match; Windows_path_case_is_ignored; Different_path_never_falls_back_to_name; Null_observed_path_never_falls_back; Null_discovered_entry_path_is_rejected; Legacy_manual_matches_name; Legacy_builtin_matches_name; Legacy_discovered_is_rejected; Needs_revalidation_is_rejected; Valid_metadata_and_revision_are_required; Inconsistent_name_is_rejected; Explicit_path_has_no_fallback; Excluded_in_another_directory_does_not_hide_main.

DiscoveredSignatureValidatorTests:
Persisted_valid_without_fresh_inventory_is_pending; Fresh_matching_inventory_and_revision_are_valid; Revision_is_reread_each_use; Changed_size_invalidates; Changed_timestamp_invalidates; Inaccessible_file_invalidates; Different_generation_or_policy_invalidates; Absent_or_ambiguous_inventory_invalidates; Stale_token_after_revision_read_is_rejected; Cancellation_propagates_without_invalidation; Needs_revalidation_has_no_read_or_write_loop.

SessionRuntimeDiscoveredSignatureTests:
Valid_path_starts_after_two_future_snapshots; Wrong_path_does_not_start; Legacy_explicit_origins_still_start; Legacy_discovered_does_not_start; Pending_recovery_is_not_closed_or_heartbeated; Pending_then_valid_uses_fresh_single_capture; Pending_then_invalid_closes_at_persisted_last_seen; Cancellation_during_pending_keeps_recoverable_state; Explicit_session_progresses_while_discovered_is_pending; Revision_failure_closes_active_at_last_reliable_time; No_validator_cannot_bypass_validation; Learning_dates_never_backfill_session.

Use the existing real ProcessSignatureMatcher, SessionTransitionPolicy, SessionCorrectionPolicy and deterministic fake stores/TimeProvider patterns from the Core runtime tests. New fakes implement the new interfaces; do not modify historical fake classes or registration tests.

```csharp
var id = Guid.NewGuid();
var t0 = DateTimeOffset.UnixEpoch;
var signature = new ProcessSignature(id,
    [new ProcessSignatureEntry("Game.exe", ProcessSignatureEntryKind.Main,
        @"C:\Games\One\Game.exe", new FileRevision(10, t0))],
    ProcessSignatureOrigin.Discovered, t0,
    new DiscoveredSignatureMetadata(new InstallationId(Guid.NewGuid()),
        Guid.NewGuid(), 1, ProcessSignatureValidationState.Valid, Guid.NewGuid()));
var process = new ProcessSnapshot(12, "Game.exe", @"C:\Games\Two\Game.exe", t0);
Assert.False(new ProcessSignatureMatcher().Match(signature, [process]).HasMainProcess);
```

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignaturePathMatcherTests|FullyQualifiedName~DiscoveredSignatureValidatorTests|FullyQualifiedName~SessionRuntimeDiscoveredSignatureTests"
```

First observe the behavioral matcher RED (wrong path currently matches) before adding tests requiring the new validator API; then observe their API RED. Save the named outcomes separately. Do not call a compile failure the evidence that runtime Pending behavior was exercised.

- [ ] **Step 3: GREEN minimal**

Implement the matcher per-process entry filtering and retained role precedence; add the validator; add the optional runtime gate and deferred recovery identity set. No alteration of transition-policy, session-store, correction-store, heartbeat constants or source capture implementation.

Core matching predicate inside the entry loop:

```csharp
var nameMatches = string.Equals(entry.ExecutableName, process.ExecutableName,
    StringComparison.OrdinalIgnoreCase);
var identityMatches = !string.IsNullOrWhiteSpace(entry.ExecutablePath)
    ? nameMatches && string.Equals(entry.ExecutablePath, process.ExecutablePath,
        StringComparison.OrdinalIgnoreCase)
    : nameMatches && signature.Origin is
        ProcessSignatureOrigin.Manual or ProcessSignatureOrigin.BuiltIn;
```

Apply Discovered metadata/revision gating before that loop. A null/empty returned revision is Invalid, not zero-length-file evidence.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignaturePathMatcherTests|FullyQualifiedName~DiscoveredSignatureValidatorTests|FullyQualifiedName~SessionRuntimeDiscoveredSignatureTests"
```

Expected: all matching, revision and Pending/recovery behaviors pass, including once-per-refresh capture.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~Sessions|FullyQualifiedName~Database"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore
```

Core covers SessionRuntimeTests (including simultaneous sessions), SessionRuntimeHeartbeatPersistenceTests, SessionRuntimeRecoveryTests, SessionCorrectionPolicyTests and Task08Fix01CorrectionRuntimeTests. Full UI includes LibrarySessionThreadAffinityTests (Bugfix A), Sessions, registration and startup ordering. Do not relax historical tests to accept a regression.

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
git diff --name-only -- src/PlayStead.UI src/PlayStead.Providers src/PlayStead.Platform
```

The last command must be empty for this task.

- [ ] **Step 7: Review**

Review exact path/no-name-fallback behavior, role precedence, fresh capture after Pending, cancellation recovery identity, optional constructor compatibility and continued explicit session operation. Ensure no discovery call or runtime registration was introduced.

- [ ] **Step 8: Commit**

```powershell
git add "src/PlayStead.Core/Sessions/IDiscoveredSignatureValidator.cs" "src/PlayStead.Core/Sessions/Discovery/DiscoveryInventoryContext.cs" "src/PlayStead.Core/Sessions/Discovery/DiscoveredSignatureValidator.cs" "src/PlayStead.Core/Sessions/ProcessSignatureMatcher.cs" "src/PlayStead.Core/Sessions/SessionRuntime.cs" "tests/PlayStead.Core.Tests/Sessions/ProcessSignaturePathMatcherTests.cs" "tests/PlayStead.Core.Tests/Sessions/SessionRuntimeDiscoveredSignatureTests.cs" "tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveredSignatureValidatorTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(sessions): validate discovered paths before matching"
git status --short
git show --stat --oneline HEAD
git stash list
```


## Task B2.7 — Core learning coordinator with simulated captures

**Files**

- CREATE: `src/PlayStead.Core/Sessions/Discovery/ProcessObservationBatch.cs`
- CREATE: `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureLearningCoordinator.cs`
- TEST CREATE: `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureLearningCoordinatorTests.cs`

**Interfaces**

Namespace PlayStead.Core.Sessions.Discovery:

```csharp
public sealed record ProcessObservationBatch
{
    public ProcessObservationBatch(long sequenceNumber, DateTimeOffset observedAtUtc,
        EpisodeQuality quality, IReadOnlyList<ProcessSnapshot> processes)
    {
        if (sequenceNumber < 0) throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        ArgumentNullException.ThrowIfNull(processes);
        if (processes.Any(process => process is null))
            throw new ArgumentException("Processes cannot contain null.", nameof(processes));
        SequenceNumber = sequenceNumber;
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        Quality = quality;
        Processes = Array.AsReadOnly(processes.ToArray());
    }
    public long SequenceNumber { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public EpisodeQuality Quality { get; }
    public IReadOnlyList<ProcessSnapshot> Processes { get; }
}

// ProcessSignatureLearningCoordinator constructor:
public ProcessSignatureLearningCoordinator(
    IProcessSignatureLearningStore learningStore,
    IProcessSignatureStore signatureStore,
    IExecutableRevisionSource revisionSource,
    ProcessSignatureDiscoveryPolicy policy);

// Public members:
public ProcessSignatureLearningState? GetState(InstallationId installationId);
public Task<ProcessSignatureLearningState> InitializeAsync(
    DiscoveryInventoryContext context, CancellationToken cancellationToken);
public Task<bool> PrepareEpisodeAsync(
    DiscoveryInventoryContext context, Guid expectedLearningToken,
    CancellationToken cancellationToken);
public Task<DiscoveryDecision?> ObserveAsync(
    InstallationId installationId, ProcessObservationBatch batch,
    CancellationToken cancellationToken);
```

One Core coordinator owns the per-installation states in a private Dictionary<InstallationId, InstallationLearning>, where InstallationLearning is a private nested class holding that installation's current episode. It is ready to be registered as the singleton required by the spec in B3; B2 does not register it or create a background owner. GetState returns an immutable snapshot for the requested installation. ObserveAsync receives an installation ID and a supplied batch; the caller can submit the same captured batch for each prepared installation without taking another capture. All async transitions serialize with one private SemaphoreSlim and release in finally. Preparation rejects a changed GameId for an existing InstallationId; root changes for the same identity follow generation invalidation. No filesystem/SQLite/source capture dependency.

InitializeAsync requires a freshly obtained inventory context, loads only completed state, and resets in-memory capture/absence/identity tracking. It uses the loaded token for any initial CAS. A conflicting initialization write throws InvalidOperationException and does not expose an initialized coordinator. PrepareEpisodeAsync requires the expected current learning token; stale publication returns false with no state mutation. Both compare inventory by value; changed root/presence/content/completeness/issue set requires a new generation. If the incoming inventory reused the old generation despite changed content, construct a new InstallationScope with Guid.NewGuid and a new ExecutableInventory preserving the returned candidates/issues. The returned state.Inventory is the authoritative prepared generation that the caller must also publish to validators/acceptance. A caller-supplied different generation also resets proof. PolicyVersion always equals ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion; loading another version clears evidence and suspends Discovered through the store.

InitializeAsync prepares the first episode. Every subsequent episode requires a successful PrepareEpisodeAsync with a fresh inventory result, even if unchanged. During an active episode an unchanged preparation returns false without mutation; a changed generation/scope/policy discards that episode and reference, resets absence and prepares the new state. The test driver obtains inventory outside ObserveAsync using B1 IExecutableInventorySource.InventoryAsync; production scheduling of that refresh is B3. If an apparition arrives while preparation is still pending, do not qualify or complete that partial current observation; keep completed Reference/Confirmation and require preparation plus two new absent captures. Pending preparation alone is neither a contradictory episode nor proof of an inventory change. An actual incomplete/changed inventory result or another observed invalidating event still clears proof under its own rule. Do not lazily enumerate inside ObserveAsync.

### Deterministic transition contract

- Before initialization, ObserveAsync throws InvalidOperationException. Pre-cancelled calls throw OperationCanceledException without I/O. Sequence numbers must increase by one during a live run and timestamps strictly increase; an observed gap, duplicate/out-of-order batch or incomplete/unreliable capture invalidates qualification, CAS-clears retained proof and resets known absence. A restart resets the capture sequence namespace without being classified as a live capture gap; it reloads the persisted completed episode sequence and never reserves or completes the abandoned attempt.
- No candidate at startup: accumulate two consecutive reliable absent batches. Candidate present at startup: treat the unobserved beginning as partial, keep completed Reference/Confirmation unchanged, and never infer its beginning from ProcessSnapshot.StartedAtUtc. Discard this partial current observation without emitting a qualifying or contradictory completed episode merely because its beginning was not observed. Wait until two reliable empty candidate batches before arming. A separately observed contradiction, revision change or unreliable capture still triggers its own invalidation rule.
- Candidate appearance after arming consumes the prepared-inventory flag and creates CurrentEpisode with a new EpisodeId and tentative LastSequenceNumber+1 in memory only. Reference remains the same completed persisted episode and Confirmation remains unchanged (normally null before the second completion). Do not clear or rotate either slot, the learning token or the persisted sequence at episode start; do not move a completed Confirmation into Reference. No start-only persistence call is made.
- FirstSnapshot/LastSnapshot delimit the evidence window, including the two observed pre-absence batches and eventual two final absent batches. StartedAtUtc is the first positive batch's actual ObservedAtUtc; EndedAtUtc is the second final absent batch's actual ObservedAtUtc. Thus episode times do not overlap even when consecutive episodes share the two absence captures as boundary evidence; no extra absence threshold is introduced. Candidate evidence contains maximal contiguous positive SnapshotRange intervals; no arbitrary duration threshold. Revision-read each observed known candidate before using that batch. A stable identity is (ProcessId, StartedAtUtc), never PID alone.
- Two distinct process identities at one candidate path are not silently stitched into a stable lifetime. An identity replacement before two confirmed global absent captures breaks the episode; this covers PID reuse and same PID/different start. A normal later episode may use a new PID/start.
- A candidate name with null/untrusted path, missing reliable identity, or any newly appearing unassignable identity during the episode breaks qualification. Reliably known unrelated processes outside the root are not candidates. Unknown process names/paths never enter persisted state.
- A new reliable path strictly under the installation root but absent from inventory invalidates qualification with IncompleteInventory and requires external re-inventory; it is not inferred as a companion or Main. Save an incomplete new generation with an InventoryIssue at that path (InventoryIssueKind.RevisionChanged identifies the changed inventory), atomically clearing proof and suspending Discovered through the learning store. Scope boundary comparisons in Core use already canonical identities and OrdinalIgnoreCase.
- Revision mismatch or inaccessible file discards the current episode and clears the reference, emits RevisionChanged/UnreliablePath, and requires a fresh inventory generation. Immediately construct an Incomplete ExecutableInventory retaining the known candidates, adding the reader's InventoryIssue (RevisionChanged for unequal values), and using a new scope generation GUID; CAS-save it with no summaries. The learning store suspends accepted Discovered in that same transaction. This is invalidation of known evidence, not a recursive scan or a claim to know a new file revision. Until an external fresh inventory is prepared, no observation qualifies. Runtime's independent validator also prevents use of changed binaries.
- Once all candidates disappear, require two consecutive reliable absent batches; one absence followed by a return does not close the episode and produces a second range. A capture error is not an absent snapshot.
- On real closure, create B1 LearningEpisodeSummary with all inventory candidates (including those with empty ranges), actual bounds, the tentative episode sequence and no invented process relationships. Use the retained completed Reference and its expected learning token, not a start-time replacement. Evaluate reference plus the now-completed candidate confirmation (or a single completed summary when no reference exists) through the real policy and current signature origin. Persist only if the expected token still matches; stale evidence cannot be reinstated after another invalidation.
- A first completed result with AwaitingIndependentEpisode persists that summary as Reference and leaves Confirmation null. A completed independent episode qualifying the same candidate under unchanged scope/generation/policy and without intervening invalidation yields PromoteMain and persists Confirmation alongside the unchanged Reference. A completed contradiction or other invalidating quality result clears both durable slots and in-memory retained proof, with its reason and a new learning token; do not keep the conflicting episode as a new reference or select older successes. A completed pair stays intact until atomic acceptance/consumption or a real invalidating transition; a mere subsequent start or restart does not rotate it. No policy evaluation at a mere start, cancellation or restart may manufacture a completed refusal or confirmation.
- The policy is the sole judge of startup companion behavior. A long-lived companion, reappearance, late competitor, equivalent survivors or unobserved executable can block promotion. B1 has no affirmative exclusion payload; all B1 inventory candidates participate. Existing explicit signatures short-circuit learning with ProtectedSignature.
- Persist only on initialization/preparation changes, completed episode results, acceptance/consumption, or the first real invalidating transition. No write is caused solely by starting an episode, stable positive ticks, ordinary repeated absent ticks, repeated identical refusal or LoadAsync. On a quality failure an already empty durable state needs no second identical write. Once an invalidating event is observed, stop using the old reference immediately and persist its removal before learning can continue; propagate persistence failure instead of reloading and treating that old reference as valid.
- Cancellation or shutdown mid-episode abandons only CurrentEpisode and its tentative sequence, preserves already completed Reference/Confirmation and requires known absence after resumption. It never finalizes that episode or promotes a signature. Propagate cancellation; do not write a synthetic completion or erase finished proof merely because the operation stopped. A prior real invalidation is not undone by cancellation or restart: an already-cleared reference must remain cleared.
- Decision is a proposal, not successful persistence of a signature. The coordinator never receives ISessionStore, calls UpsertAsync for a signature, or opens a historical session.

- [ ] **Step 1: RED**

Add named tests:
Two_absent_batches_are_required_before_episode; One_absent_batch_is_not_enough; Already_running_at_startup_is_partial; Two_final_absences_close_once; One_final_absence_does_not_complete; Complete_episode_emits_reference_and_actual_ranges; second_qualifying_episode_confirms_persisted_reference; Equivalent_candidates_remain_ambiguous; Unobserved_competitor_blocks_promotion; Startup_companion_with_two_main_only_captures_qualifies; Companion_still_alive_blocks; Companion_reappearing_blocks; Late_competitor_blocks; contradictory_second_episode_clears_reference; capture_gap_during_second_episode_does_not_promote; Nonmonotonic_capture_is_not_continuity; Unknown_path_invalidates; Unknown_started_identity_invalidates; New_unassignable_identity_invalidates; Pid_reuse_is_not_stable_presence; Same_pid_new_started_time_is_not_stable_presence; Known_unrelated_process_is_not_a_candidate; New_path_under_root_requires_inventory; Changed_revision_discards_episode; Inaccessible_revision_discards_episode; Changed_generation_discards_reference; Changed_policy_discards_reference; Absent_or_ambiguous_scope_refuses_learning; Stale_preparation_cannot_replace_state; New_episode_requires_fresh_preparation; Cancellation_discards_current_but_preserves_completed_reference; Stable_ticks_do_not_write_learning_state; reference_survives_start_of_second_episode; Existing_explicit_authority_refuses_learning; One_coordinator_keeps_installation_episodes_independent.

Required lifecycle RED assertions (spec sections 10, 11, 13, 15 and 25):

| Case / exact test name | Owning test class | Arrangement and required assertions |
|---|---|---|
| A. reference_survives_start_of_second_episode | ProcessSignatureLearningCoordinatorTests | Persist qualifying episode 1; prepare unchanged inventory; observe the beginning of episode 2. Load the store: the entire Reference summary, null Confirmation, LastSequenceNumber and token are unchanged; no start-only write or promotion occurs. |
| B. restart_mid_second_episode_keeps_completed_reference | ProcessSignatureDiscoveryPipelineTests | Stop after episode 2 begins; recreate coordinator and SQLite stores with fresh unchanged inventory. Episode 1 reloads unchanged; no current ranges or tentative sequence reload. Require two new absent captures, then allow a genuinely completed later episode to confirm episode 1. |
| C. second_qualifying_episode_confirms_persisted_reference | ProcessSignatureLearningCoordinatorTests | Finish an independent episode 2 with the same candidate/scope/generation/policy and no intervening invalidation. Real B1 policy yields PromoteMain; stored Reference retains episode 1's ID/content and Confirmation contains the distinct completed episode 2 with adjacent sequence and nonoverlapping times. |
| D. contradictory_second_episode_clears_reference | ProcessSignatureLearningCoordinatorTests | Finish episode 2 with a conflicting candidate. Both persisted slots are cleared, the learning token changes and no promotion occurs; the contradictory summary is not retained as a new reference. |
| E. capture_gap_during_second_episode_does_not_promote | ProcessSignatureLearningCoordinatorTests | Inject a live capture gap after episode 2 begins. Clear Reference/Confirmation for this real rupture, discard CurrentEpisode, reset known absence and reject further use of the old token; later positive captures cannot complete the broken episode. |
| F. restart_does_not_count_as_second_episode | ProcessSignatureDiscoveryPipelineTests | Restart with one completed reference, both while idle and with a process already present. Before a new fully observed episode completes, Confirmation stays null, no signature is accepted, and no persisted sequence advances because of the restart. |
| G. old_reference_cannot_pair_with_success_after_intermediate_contradiction | ProcessSignatureDiscoveryPipelineTests | Persist success 1; commit invalidation from contradictory episode 2; restart; complete success 3. It becomes only a new Reference, never a confirmation of episode 1. Only an independent compatible success 4 can promote; a stale episode-1 token cannot restore proof. |

In B2.8, repeat B/F/G against real SQLite and fresh store instances. Also assert that cancellation alone preserves the completed reference, that merely pending inventory preparation does not erase it, and that a previously committed contradiction remains cleared after cancellation/restart. These cases distinguish abandonment of incomplete CurrentEpisode from an observed invalidating rupture.

Use nested fake IProcessSignatureLearningStore with real CAS semantics and write-count tracking, fake IProcessSignatureStore, and a targeted revision fake keyed by path. All input models are actual B1/B2 DTOs. No tests call Windows or use a new provider model.

Concrete timeline for one candidate, with t0 fixed and every timestamp t0+(sequence*2 seconds):

```csharp
var first = new ProcessSnapshot(100, "Game.exe", @"C:\Games\Example\Game.exe", t0.AddSeconds(5));
await coordinator.ObserveAsync(installation, new ProcessObservationBatch(1, t0.AddSeconds(2), EpisodeQuality.Complete, []), CancellationToken.None);
await coordinator.ObserveAsync(installation, new ProcessObservationBatch(2, t0.AddSeconds(4), EpisodeQuality.Complete, []), CancellationToken.None);
await coordinator.ObserveAsync(installation, new ProcessObservationBatch(3, t0.AddSeconds(6), EpisodeQuality.Complete, [first]), CancellationToken.None);
await coordinator.ObserveAsync(installation, new ProcessObservationBatch(4, t0.AddSeconds(8), EpisodeQuality.Complete, [first]), CancellationToken.None);
await coordinator.ObserveAsync(installation, new ProcessObservationBatch(5, t0.AddSeconds(10), EpisodeQuality.Complete, []), CancellationToken.None);
var decision = await coordinator.ObserveAsync(installation, new ProcessObservationBatch(6, t0.AddSeconds(12), EpisodeQuality.Complete, []), CancellationToken.None);
Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, decision!.Kind);
Assert.Equal(new[] { DiscoveryReason.AwaitingIndependentEpisode }, decision.Reasons);
Assert.NotNull(coordinator.GetState(installation)!.Reference);
Assert.Null(coordinator.GetState(installation)!.Confirmation);
Assert.Equal(new SnapshotRange(3, 4),
    Assert.Single(coordinator.GetState(installation)!.Reference!.Candidates).PresenceRanges[0]);
```

Arrange coordinator with the exact one-candidate inventory shown in B2.3 and call InitializeAsync(new DiscoveryInventoryContext(inventory,false), None) before this timeline. For the second episode call PrepareEpisodeAsync with coordinator.GetState(installation)!.ConcurrencyToken and an unchanged fresh inventory; use a distinct PID/start and later timestamps. After a restart two new leading absent batches are mandatory. In a continuous run the previous two final absent batches can also establish the next leading absence, provided preparation found the same generation and no continuity defect occurred. StartedAtUtc at the new apparition remains strictly after the previous EndedAtUtc. Add Trailing_absences_arm_next_episode_without_extra_threshold using captures 5/6 as absence, 7/8 as the new identity's presence, and 9/10 as final absence.

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureLearningCoordinatorTests"
```

Expected initial RED: absent coordinator/batch. Implement in behavioral increments: first reference, second episode, contradictions, interruption/cancellation and write boundaries. Observe each failing assertion before its production fix.

- [ ] **Step 3: GREEN minimal**

Implement only the batch and coordinator. Each private InstallationLearning state contains its prepared immutable snapshot (including completed Reference/Confirmation), a separate memory-only CurrentEpisode with tentative sequence, capture continuity/known-absence tracking, candidate identity/ranges and first/last observed bounds. A SemaphoreSlim serializes public methods. Episode start changes only that private current state. Use CAS saves for completed results and real invalidations; only publish a successful durable result after true. On conflict discard CurrentEpisode and stop using stale proof, then throw InvalidOperationException to the orchestration boundary; never retry a stale reference against newer state.

Core policy invocation at closure:

```csharp
IReadOnlyList<LearningEpisodeSummary> episodes = reference is null
    ? [completed]
    : [reference, completed];
var existing = await signatureStore.GetAsync(
    completed.Scope.GameId.Value, cancellationToken);
var decision = policy.Evaluate(new DiscoveryEvaluation(
    inventory, episodes, hasAmbiguousInstallation, existing?.Origin));
```

reference is the unchanged completed summary retained in the learning state; completed is created only after actual episode closure. inventory/hasAmbiguousInstallation are the prepared immutable values; signatureStore/policy are constructor dependencies. The resulting pair is saved with that learning state's expected token, so a concurrent contradiction cannot be overwritten. AwaitingIndependentEpisode is accepted as a reference only for this evaluation of one actual finished episode, never for an empty list. Incomplete CurrentEpisode never enters the persisted summary slots or this completed-episode evaluation.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureLearningCoordinatorTests"
```

Expected: all named state-machine, quality, policy and no-per-tick-write tests pass.

- [ ] **Step 5: Regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SqliteProcessSignature"
```

- [ ] **Step 6: Diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

- [ ] **Step 7: Review**

Walk cases A-G and the nominal two-episode timeline against spec sections 10/11/13/15/25. Verify that start/restart/cancellation preserve completed proof, actual invalidations clear it durably, and a later success cannot bypass an intermediate contradiction. Check real B1 policy use, nonoverlapping episode times, no raw-current persistence, unchanged reference plus independent confirmation, and preparation outside ObserveAsync. No duplicate policy or production activation.

- [ ] **Step 8: Commit**

```powershell
git add "src/PlayStead.Core/Sessions/Discovery/ProcessObservationBatch.cs" "src/PlayStead.Core/Sessions/Discovery/ProcessSignatureLearningCoordinator.cs" "tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureLearningCoordinatorTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(sessions): learn completed process episodes"
git status --short
git show --stat --oneline HEAD
git stash list
```

## Task B2.8 — Acceptance orchestration, restart integration and final B2 gate

**Files**

- CREATE: `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureAcceptanceService.cs`
- TEST CREATE: `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureAcceptanceServiceTests.cs`
- TEST CREATE: `tests/PlayStead.Data.Tests/Sessions/ProcessSignatureDiscoveryPipelineTests.cs`
- USE unchanged: the coordinator, learning/signature stores, targeted revision contract, matcher, runtime and DiscoveryDatabaseFixture from completed tasks.

**Interfaces**

Namespace PlayStead.Core.Sessions.Discovery:

```csharp
public sealed class ProcessSignatureAcceptanceService
{
    public ProcessSignatureAcceptanceService(
        IProcessSignatureLearningStore learningStore,
        IProcessSignatureStore signatureStore,
        IProcessSignatureDiscoveryStore discoveryStore,
        IExecutableRevisionSource revisionSource,
        ProcessSignatureDiscoveryPolicy policy,
        Func<InstallationId, DiscoveryInventoryContext?> currentInventory,
        TimeProvider timeProvider);
    public Task<bool> TryAcceptAsync(
        InstallationId installationId, CancellationToken cancellationToken);
}
```

true means the conditional Data transaction committed. false means no accepted signature; cancellation/database faults propagate. No injection or call of ISessionStore, SessionRuntime, launch or a process source.

### Acceptance sequence

1. Load the completed state; require reference+confirmation and a freshly supplied, matching inventory context. Null context returns false without converting pending preparation into a contradiction.
2. Reload authoritative signature. Manual/BuiltIn returns false. Existing Valid Discovered is not replaced. A NeedsRevalidation Discovered yields an expectation from its persisted token/generation/state/installation; legacy migrated rows have null scope fields but a real token.
3. Evaluate the real B1 policy against both finished summaries, current origin, exact inventory and ambiguity. Only PromoteMain proceeds. Do not turn a refusal into a signature or mutate explicit authority.
4. Immediately before submitting the conditional write, targeted-read all inventory candidate revisions used in the proof, including the Main and startup companions. A changed/inaccessible revision refuses acceptance, clears completed evidence with CAS and suspends an existing Discovered by its expectation. Refresh/new generation belongs at the preparation boundary; no inventory under a SQL lock.
5. Check cancellation. Construct one Discovered Main with the policy candidate's exact name/path/revision, current scope generation/policy, Valid, new Guid token and TimeProvider.GetUtcNow() for UpdatedAtUtc. The request includes the exact loaded learning token and two EpisodeIds.
6. Call TryInsertDiscoveredIfAbsentAsync when no signature was loaded, otherwise TryRevalidateDiscoveredAsync with the exact expectation. false is final for this attempt: reload state when needed by the caller, never fall back to UpsertAsync.
7. On successful Data acceptance, proof is already consumed atomically. The coordinator must be reinitialized/reloaded before later observation if its learning token became stale; no duplicate in-memory acceptance state is invented.
8. A restart after confirmation but before commit reloads both completed summaries unchanged and can retry only after fresh inventory and revision validation; it does not count as new evidence. A restart midway through a second episode reloads only the completed reference, abandons CurrentEpisode and requires new known absence before another complete episode can confirm it. A restart after committed acceptance sees a signature and empty proof; it must not consume the pair twice. An earlier committed contradiction/rupture remains invalidating across every restart.

When an acceptance revision failure clears proof, use TrySaveAsync with a new learning token and no summaries, then invalidate the old signature expectation. Each operation is conditional; a concurrent winner may make one return false, but no stale successful promotion is reported. Runtime revalidation independently blocks use of a changed binary. Do not claim this filesystem/DB sequence is an atomic disk snapshot.

- [ ] **Step 1: RED**

Core tests:
Only_promote_decision_reaches_conditional_store; Exact_main_path_revision_scope_and_tokens_are_submitted; Reference_only_does_not_write_signature; Protected_origin_does_not_reach_acceptance; Every_proof_revision_is_checked_before_acceptance; Changed_revision_prevents_write; Cancellation_before_acceptance_propagates; Stale_learning_conflict_is_not_retried; Revalidation_uses_exact_old_expectation; Pending_fresh_inventory_keeps_finished_proof; Db_failure_is_not_reported_as_success.

Data integration tests:
Reference_survives_new_coordinator_and_store_instances; Restart_mid_episode_never_completes_it; restart_mid_second_episode_keeps_completed_reference; old_reference_cannot_pair_with_success_after_intermediate_contradiction; Restart_after_reference_requires_known_absence; restart_does_not_count_as_second_episode; Confirmation_survives_crash_before_acceptance; Restart_after_acceptance_does_not_promote_twice; Two_simulated_episodes_accept_exact_discovered_main; Revalidated_path_requires_two_new_episodes; Revision_change_between_observation_and_acceptance_refuses; Generation_change_between_read_and_write_conflicts; Manual_edit_between_proof_and_acceptance_wins; SessionRuntime_tracks_only_a_future_episode; Simultaneous_explicit_and_discovered_sessions_preserve_corrections; No_learning_row_update_for_stable_ticks.

Each integration test creates an isolated real schema-6 DB, real SqliteProcessSignatureLearningStore and SqliteProcessSignatureStore, real B1 policy/coordinator/service/matcher, and simulated inventory/revision/captures. Data.Tests already references Core/Data; no new Platform or UI dependency is required. Only targeted filesystem tests use the real Platform reader. Test source contains no production signatures injected for the positive pipeline: the coordinator must earn the two summaries. Implement B/F/G with the exact lifecycle assertions in B2.7, including unchanged reference/token after a mere start, restart without a synthetic sequence increment, and durable contradiction invalidation checked through a new store instance.

Positive test sequence (method names below are real APIs, not an added pipeline facade):
- Seed a game/installation; fake B1 inventory source returns one executable with real B1 revision DTO.
- Initialize first coordinator from that inventory, feed reliable absent/absent/present/present/absent/absent. Assert no signature and one reference.
- Discard coordinator/store instances, instantiate new ones, obtain fresh unchanged inventory, initialize, then feed a wholly later second episode with a distinct process identity. Assert real policy returns PromoteMain and two stored summaries.
- Construct ProcessSignatureAcceptanceService, TryAcceptAsync, reload signature and assert exactly one path-aware Main/Valid metadata, and both evidence slots empty.
- Assert ISessionStore.GetRecentAsync is still empty. Construct real SessionRuntime with DiscoveredSignatureValidator and fake source/TimeProvider; feed two later exact-path captures, then absence. Assert start is the first future observation and end is last reliable future observation, not either learning episode's dates.
- Repeat the acceptance boundary with Manual inserted first and verify false plus unchanged Manual.

Actual assertions after this arranged pipeline:

```csharp
Assert.True(await acceptance.TryAcceptAsync(installation, CancellationToken.None));
var accepted = await signatures.GetAsync(game.Value, CancellationToken.None);
Assert.Equal(ProcessSignatureOrigin.Discovered, accepted!.Origin);
Assert.Equal(ProcessSignatureValidationState.Valid, accepted.Discovery!.ValidationState);
Assert.Equal(@"C:\Games\Example\Game.exe", Assert.Single(accepted.Entries).ExecutablePath);
var consumed = await learning.LoadAsync(installation, CancellationToken.None);
Assert.Null(consumed!.Reference);
Assert.Null(consumed.Confirmation);
Assert.Empty(await sessions.GetRecentAsync(10, CancellationToken.None));
```

The locals are the fixture's actual IDs/stores/service named in the sequence, with constructors defined in this plan. Inject acceptance failure using fixture SQL RAISE triggers and construct another store instance afterward to prove no partial durable state.

- [ ] **Step 2: Run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureAcceptanceServiceTests"
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureDiscoveryPipelineTests"
```

Expected initial RED: missing acceptance service. After its minimal API exists, require executed RED for proof submission/consumption and revision/cancellation guards before each implementation increment. If an integration test exposes a fault in a prior completed task, stop the gate and report the precise owning contract; fix it with its own observed regression test and fresh review before resuming. Do not silently expand this task's production scope.

- [ ] **Step 3: GREEN minimal**

Implement the acceptance service using only the defined interfaces and the sequence above. No new orchestration registration. Constructor dependencies are mandatory and null-guarded. Validate the fresh context against the persisted inventory by value (not just generation ID); a caller must publish the authoritative Inventory returned by initialization or GetState after preparation when the coordinator renewed its generation.

The successful proposal construction is:

```csharp
var main = decision.Main
    ?? throw new InvalidOperationException("Promotion requires a main candidate.");
var accepted = new ProcessSignature(state.Inventory.Scope.GameId.Value,
    [new ProcessSignatureEntry(main.ExecutableName, ProcessSignatureEntryKind.Main,
        main.ExecutablePath, main.Revision)],
    ProcessSignatureOrigin.Discovered, timeProvider.GetUtcNow(),
    new DiscoveredSignatureMetadata(state.Inventory.Scope.InstallationId,
        state.Inventory.Scope.GenerationId, state.PolicyVersion,
        ProcessSignatureValidationState.Valid, Guid.NewGuid()));
var write = new DiscoveredSignatureWrite(accepted, state.ConcurrencyToken,
    state.Reference!.EpisodeId, state.Confirmation!.EpisodeId);
```

decision is the real B1 result after fresh validations; state is the unchanged loaded completed state. Bind the appropriate Data operation without a generic Upsert fallback.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureAcceptanceServiceTests"
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureDiscoveryPipelineTests"
```

Expected: all acceptance and restart tests pass; successful integration earns a real signature from simulated evidence without creating a past session.

- [ ] **Step 5: Regression tests — fresh final B2 gate**

Run the full suites once, freshly, after the last code correction. UI is mandatory here because B2.6 changed runtime/matcher contracts used by UI.

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore
dotnet build ".\PlayStead.sln" --configuration Release --no-restore
```

Record exact total/pass/fail/skipped counts and build warning/error counts from these runs. All executed tests must pass, zero skipped/not-executed, build zero warnings/errors. No asserted new total is an acceptance substitute; B1 counts are historical context. No commercial game, network request, executable launch or visual claim is needed for this B2 simulated gate; the real runtime/product/visual acceptance is explicitly still B3.

- [ ] **Step 6: Diff check and scope gate**

```powershell
git diff --check
git status --short
git diff --stat
git log --oneline --decorate -15
git stash list
git rev-parse "stash@{0}"
git diff 3b1ed43a19d8a959d2526361d8d2737f844ea361..HEAD --name-status
```

The range excludes uncommitted files: review the status/stat and the three new task files separately before staging. Allowed cumulative paths are exactly the File map and this plan. Confirm no source UI/Providers/Task7, registration, startup, monitor or launch edits; B1 policy and inventory remain unchanged. Stash object must remain 622b590181ed07283907190c342963489be60340.

- [ ] **Step 7: Review**

Fresh spec and code-quality reviews cover the entire B2 diff, not only acceptance. Require evidence for migration rollback, atomic origin/token guards, no name fallback, bounded proof persistence, interruption/restart, targeted revisions, deferred recovery, full Sessions/Dispatcher regressions and disabled production discovery. Resolve findings before committing; any behavior fix gets its own observed RED and rerun affected gate.

- [ ] **Step 8: Commit and postverify**

Only after all reviews/gates pass:

```powershell
git add "src/PlayStead.Core/Sessions/Discovery/ProcessSignatureAcceptanceService.cs" "tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureAcceptanceServiceTests.cs" "tests/PlayStead.Data.Tests/Sessions/ProcessSignatureDiscoveryPipelineTests.cs"
git diff --cached --check
git diff --cached --name-status
git diff --cached --stat
git commit -m "feat(sessions): complete simulated discovery acceptance pipeline"
git status --short
git show --stat --oneline HEAD
git log --oneline --decorate -15
git stash list
git rev-parse "stash@{0}"
git diff 3b1ed43a19d8a959d2526361d8d2737f844ea361..HEAD --name-status
git diff --check
```

Require clean worktree/index and unchanged stash. No push/merge. Report B2_GREEN=True only after this postverify and all conditions below; otherwise B2_GREEN=False.

## Completion conditions and B3 boundary

B2_GREEN=True requires all of the following observed evidence:
- Fresh schema 6 and real v5 upgrade pass; transactional failure and backup restore preserve old data.
- Manual/BuiltIn authority survives stale reads and concurrent writes.
- Legacy Discovered remains readable, NeedsRevalidation and unmatchable.
- Exact path/name matching has no Discovered name fallback, including inaccessible paths.
- Conditional insert/revalidation and proof consumption are atomic; stale signature/learning tokens cannot win.
- Inventory and both finished summaries round-trip; maximum two persisted; no raw current episode is recovered.
- Restart preserves completed Reference/Confirmation and discards only incomplete CurrentEpisode; two new absent captures are required and restart is never confirmation. Observed contradictions/invalidating ruptures cannot be skipped or restored from stale proof; no synthetic session/backfill.
- Coordinator consumes deterministic captures and delegates candidate choice to unmodified B1 policy.
- Targeted revision change/access failure invalidates; fresh inventory is required after restart and before new qualification.
- SessionRuntime consumes validated path-aware signatures while preserving explicit sessions, heartbeat, recovery, corrections, concurrency and Dispatcher behavior.
- No discovery production wiring, second poller or Task 7 change exists.
- Full Core/Data/Platform/UI suites and Release build pass with zero warnings/errors, diff check clean, worktree/index clean and stash intact.

Always report:
```text
B2_GREEN=True or B2_GREEN=False, selected from observed implementation evidence
SESSION_DISCOVERY_FIXED=False
```

This planning commit itself establishes neither implementation nor B2_GREEN.

B3 retains: production singleton orchestration across installations; startup and rescan inventory scheduling/publication; a single shared monitor capture and quality signal; optional LaunchIntent; real launch/library wiring; registration and lifecycle/logging; real positive learning -> accepted signature -> future session/history gate; real ambiguous refusal; filesystem update suspension; final runtime/product/visual review. B2 does not close any of these gates.

## Plan self-review record

1. Spec sections 8/12/13: entry path/revision and parent validation metadata are explicit in B2.1/B2.2.
2. B1 actual API: all evidence constructors, CurrentPolicyVersion=1, Evaluate and nullable Main reused; no invented exclusion classifier.
3. Production activation: new interfaces/components are unregistered; no monitor/coordinator or startup call added.
4. Migration: additive 006, correct current-schema fixtures, real v5 and intermediate rollback tests, historical SQL unchanged.
5. Atomicity: signature writer owns authority checks, proof consumption and entries in one non-deferred transaction.
6. Manual/BuiltIn: distinct guarded explicit API and acceptance API, including concurrency/priority tests.
7. Legacy Discovered: nullable scope/path remains readable with NeedsRevalidation, not upgraded by guesswork.
8. Path matching: exact full path plus coherent name, case-insensitive; explicit no-path fallback only.
9. Lifecycle: Reference/Confirmation remain durable at episode start; only CurrentEpisode is memory-only and abandoned at restart. Observed contradictions/invalidating ruptures clear retained proof and cannot be bypassed; start/restart alone neither clears nor confirms it. Cases A-G cover persistence, sequencing and the valid two-completed-episode path.
10. Tokens: Guid tokens for learning/signature; both expected identities checked; ABA cannot reuse token.
11. Revisions: targeted read before each used candidate observation, before acceptance and matching; no tick inventory or hash.
12. Layers: no concrete filesystem/SQLite/WPF in Core; Data checks structure/authority but never selects Main.
13. Cross-task signatures: model/store/reader/coordinator/acceptance APIs are defined once and used consistently.
14. Plan contains executable command blocks, concrete API/test names and implementation guidance for all eight tasks.
15. RED: existing matcher/schema support behavioral REDs; missing new APIs have explicit initial compile RED followed by executed behavioral assertions.
16. Commits: eight independent deliverables; earlier tasks contain all their own dependencies and tests; final task adds acceptance behavior before its gate. The coordinator retains singleton ownership with private per-installation state; shared absence evidence does not add a third/fourth absent-snapshot requirement.
17. Scope: no B3 scheduling service, launch API, provider heuristic, UI or additional poller planned.

### Learning lifecycle correction self-review

- ReferenceEpisode lifecycle = conforms to spec: a completed reference survives a new episode's start, incomplete abandonment and restart unless a real invalidating event has occurred.
- ConfirmationEpisode lifecycle = conforms to spec: only a distinct completed qualifying episode confirms the retained reference; completed confirmation survives restart until acceptance/consumption or real invalidation.
- CurrentEpisode = memory-only, including its tentative sequence and process ranges; it is never serialized as completed proof.
- Restart = incomplete current episode discarded only; completed summaries reload after fresh inventory validation, and known absence is re-established. An observed scope/generation/policy change still invalidates according to its own rule.
- Contradiction = cannot be skipped: clear durable and in-memory retained proof, rotate the learning token, and reject stale CAS writes. Cancellation/restart never restores an invalidated reference.
- Two independent completed episodes still possible = reference 1 remains available while episode 2 is observed; a compatible completion persists confirmation 2 and reaches the unchanged B1 policy.
- No B3 production activation introduced = interfaces, registration boundary and production exclusions are unchanged.

Approximate new RED coverage: 160-180 executed test cases across 11 new test classes; theory rows can change the count. One additional new test fixture supports isolated Data tests. Five historical migration test files need only realistic schema setup/current-version updates. Use observed test-run totals when implementing; this estimate is not a CI threshold.
