# PlayStead Process Signature Discovery B3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate the B1/B2 discovery pipeline in the real application, using the existing Sessions capture and real Library installations, then prove a positive discovery and an ambiguous refusal in a running PlayStead.

**Architecture:** `SessionRuntime` keeps sole ownership of the two-second process capture. An optional Core capture observer receives one immutable capture after capture and before matching; the signature list already loaded for that cycle cannot include a newly promoted signature. A UI/composition inventory manager publishes fresh, complete installation generations outside the poll loop. A sequential discovery orchestrator drives the B2 coordinator and acceptance service, while B2 validation gates Discovered sessions. Startup and rescans schedule inventory from durable `LibrarySnapshot`; external launches need no interception.

**Tech Stack:** C# / .NET 10, Core, Platform Windows, Data SQLite, WPF composition, xUnit, PowerShell.

**Spec:** `docs/superpowers/specs/2026-09-15-playstead-process-signature-discovery-design.md`

**Authority:** `docs/superpowers/specs/2026-09-15-playstead-process-signature-discovery-design.md` > this plan > implementation convenience. The B1/B2 plans and their delivered code define reusable contracts. B3 baseline: `eefc938cd084e5cee2955ab4e5ef2ecd12cf0792` on `feat/0.4.1-media-foundation`.

## Global Constraints and verified starting architecture

- Work only in `D:\Dev\PlayStead\worktrees\0.4.1-media-foundation`. Protect `stash@{0}: On feat/0.4.1-media-foundation: wip/task7-home-media-integration`, object `622b590181ed07283907190c342963489be60340`. Never pop/push/merge; do not edit Task 7/Home/media, providers, historical migrations, process-signature rules or UI discovery flows.
- Follow strict RED -> GREEN -> affected regression -> fresh spec/quality review -> gate -> controlled commit -> postverify per implementation task. A missing API may give an initial compiler RED, followed by executed behavioral REDs before task closure. Never alter historical assertions to make GREEN.
- B1 commits `a716102..3b1ed43` supplied contracts and conservative inventory/policy. B2 commits `50981a7..eefc938` supplied schema 6, CAS stores, path validation, coordinator, acceptance and simulated restart/session tests. B2 baseline was Core 363/363, Data 222/222, Platform 113/113, UI 361/361, Release 0 warnings/errors; these are prior evidence, not a B3 test threshold.
- `SessionMonitor.RunAsync` calls `ISessionRuntime.RefreshAsync` then delays by `SessionMonitorOptions.Default.PollInterval` (2 s). `SessionRuntime.RefreshAsync` loads recovery/signatures, validates Discovered, calls `_processSource.CaptureAsync` once, then matches and persists sessions. Maintain one process enumeration per monitor cycle and the 5 s persisted heartbeat/two-snapshot session start. No independent discovery hosted poller.
- `LocalStartupPipeline.InitializeAsync` initializes DB, checks health and returns durable `ILibraryStore.LoadSnapshotAsync`; `RefreshAsync` applies local scan results then reloads durable snapshot. `ApplicationStartupCoordinator` starts host before showing cached Library and schedules background rescan. `App.xaml.cs` wires those operations. Initial inventory must start after durable snapshot is known, asynchronously without blocking cache display. On rescan, invalidate publication immediately, discard stale work and publish only an entire new generation.
- `ILibraryStore` has only `ApplySourceScanAsync` and `LoadSnapshotAsync`, no change event. Use the existing startup/rescan path as the signal; do not invent a second scan. `LibrarySnapshot.Installations` contains the actual `GameInstallation(InstallationId Id, GameId GameId, ProviderKind Provider, string ExternalId, string InstallPath, long? InstalledSizeBytes, bool IsPreferred, bool IsPresent, DateTimeOffset LastSeenUtc)`; only present, reliably rooted installations qualify. Steam is the only local source registered. Do not infer other provider scanner support from enums.
- B2 `ProcessSignatureLearningCoordinator.InitializeAsync/PrepareEpisodeAsync/ObserveAsync` consumes `DiscoveryInventoryContext` and `ProcessObservationBatch`. `ProcessSignatureAcceptanceService.TryAcceptAsync` and `DiscoveredSignatureValidator.ValidateAsync` both read a current-inventory callback. `SessionRuntime` has optional `IDiscoveredSignatureValidator`, currently absent in `PlaySteadHost` registration. Avoid a new state machine or replacing B1 policy.
- B2 initialization treats an incoming generation different from the persisted generation as changed even if files match. At startup, load the existing learning state for that InstallationId and reuse its generation ID for a fresh inventory of the same root/presence; let B2 compare candidate paths/revisions and rotate the generation on actual change. An unchanged rescan preserves completed Reference/Confirmation. Never carry old evidence across a changed root, revision, file set, policy or ambiguity.
- Core remains free of Windows/SQLite/WPF/Steam; Platform supplies trusted paths/capture/revision; Data remains conditional authority. `GameLaunchService.TryLaunch` opens Steam URI and proves only request dispatch. B3 does not need `LaunchIntent` or a `GameLaunchService` edit: absent/present/absent captures learn both PlayStead-initiated and external launches under identical rules.
- `SessionsView.xaml.cs` refreshes historical data when loaded; its one-second timer calls `RefreshLive()` only. Reopening Sessions is the historical refresh gate. A separate history refresh defect, if observed, gets an independent bugfix; do not bundle a page timer rewrite into B3 or mistake stale page data for missing durable sessions.

## File map and interfaces to implement

| Task | Production | New tests |
|---|---|---|
| B3.1 | `src/PlayStead.Core/Sessions/ProcessCaptureResult.cs` (new), `IProcessSnapshotSource.cs`, `IProcessCaptureObserver.cs` (new), `SessionRuntime.cs`; `src/PlayStead.Platform/Processes/WindowsProcessSnapshotSource.cs` | `tests/PlayStead.Core.Tests/Sessions/SessionRuntimeSharedCaptureTests.cs`, `tests/PlayStead.Platform.Tests/Processes/WindowsProcessCaptureQualityTests.cs` |
| B3.2 | `src/PlayStead.UI/Sessions/DiscoveryInventoryManager.cs` (new) | `tests/PlayStead.UI.Tests/Sessions/DiscoveryInventoryManagerTests.cs` |
| B3.3 | `src/PlayStead.UI/Sessions/ProcessDiscoveryCaptureObserver.cs` (new); targeted B2 `ProcessSignatureLearningCoordinator.cs` adjustment only if the observed unknown-identity RED proves it necessary | `tests/PlayStead.UI.Tests/Sessions/ProcessDiscoveryCaptureObserverTests.cs`, optionally `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureLearningCoordinatorQualityTests.cs` |
| B3.4 | `src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs`, `ApplicationStartupCoordinator.cs`, `App.xaml.cs`, `PlaySteadHost.cs` | `tests/PlayStead.UI.Tests/Bootstrap/TaskB3DiscoveryStartupTests.cs`, `tests/PlayStead.UI.Tests/Bootstrap/TaskB3DiscoveryRegistrationTests.cs` |
| B3.5 | `src/PlayStead.UI/Sessions/SessionMonitor.cs`, `DiscoveryInventoryManager.cs`, `ProcessDiscoveryCaptureObserver.cs` if RED shows a lifecycle/fault defect | `tests/PlayStead.UI.Tests/Sessions/ProcessDiscoveryLifecycleTests.cs` |
| B3.6 | No production files unless new RED reveals a B3 defect; fix only the owning B3 file with a separate RED/review | `tests/PlayStead.UI.Tests/Sessions/ProcessDiscoveryProductionPipelineTests.cs` (new), plus existing Sessions/B1/B2 regressions unchanged |

The above map is bounded: do not automatically edit every listed file. Existing SDK project globbing needs no `.csproj` edit. B3.6 combines automated cross-layer evidence with real application acceptance; no empty paperwork-only task. Before each task verify clean index/worktree after the previous commit and unchanged stash object. New `IProcessCaptureObserver` is a Core interface, not a second source:

```csharp
public interface IProcessCaptureObserver
{
    Task ObserveAsync(ProcessCaptureResult capture,
        DateTimeOffset observedAtUtc, CancellationToken cancellationToken);
    void MarkCaptureGap();
}

public sealed record ProcessCaptureResult(
    IReadOnlyList<ProcessSnapshot> Processes, bool IsComplete);
```

`ProcessCaptureResult` defensively copies its process list. Add a default `IProcessSnapshotSource.CaptureWithQualityAsync(CancellationToken)` that invokes existing `CaptureAsync` **once**, wraps that result as complete for legacy fakes, and permits `WindowsProcessSnapshotSource` to report actual enumeration quality without a second enumeration. Successful enumeration with some inaccessible per-process metadata must retain those snapshots with nullable path/start rather than mark every Windows tick globally incomplete; the observer judges newly appearing unknown identities using cycle continuity. `SessionRuntime` calls only `CaptureWithQualityAsync`, sends the same array to observer and matcher, and retains its existing `RefreshAsync` contract. A `false` completeness flag cannot mean an empty/confirmed-absence snapshot. Existing fake sources and old public constructors must remain compatible.

## Task B3.1 — One capture, shared before matching, next-cycle promotion

**Files:** Core/Platform files and two new test classes in the B3.1 map. Existing `SessionRuntimeDiscoveredSignatureTests`, `SessionRuntimeRecoveryTests`, `WindowsProcessSnapshotSourceTests` stay unchanged.

- [ ] **Step 1: RED:** Test one fake-source invocation per refresh; same immutable process values and observed time sent to observer then matcher; preloaded signatures exclude acceptance promoted by observer until next refresh; observer cancellation propagates; observer fault is *not* treated as process absence; Platform enumeration once and captured Process objects disposed once. Test incomplete capture cannot become a qualifying absence. Named cases: `Capture_is_shared_once_before_matching`, `Promotion_only_matches_on_next_cycle`, `Legacy_source_uses_one_capture`, `Incomplete_capture_is_not_known_absence`, `Cancellation_does_not_match_or_backfill`.
- [ ] **Step 2: Run RED:**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SessionRuntimeSharedCaptureTests"
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~WindowsProcessCaptureQualityTests"
```

Require failures of the missing capture/observer contracts or executed ordering assertions. Bad fixture/namespace/testhost RED does not count.
- [ ] **Step 3: GREEN:** Implement the exact interfaces above. In `SessionRuntime.RefreshAsync`, keep recovery/signature read/Discovered validation *before* capture, then obtain one `ProcessCaptureResult` and call optional observer. If `IsComplete` is false, mark it nonqualifying and return a `SessionRuntimeSnapshot` projected from unchanged `_active`, without matching, closing or heartbeating any session; never treat its missing rows as absent processes. For a complete result, match only the already-loaded usable signatures with `capture.Processes`. On a capture failure mark a gap before rethrow; do not synthesize an empty capture. Keep existing constructor parameters, append nullable observer with default null. Windows uses one enumeration for either public capture method. New promotion cannot backfill its learning episode into `ISessionStore`.
- [ ] **Step 4: Run GREEN and regressions:**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SessionRuntimeSharedCaptureTests|FullyQualifiedName~SessionRuntimeDiscoveredSignatureTests|FullyQualifiedName~SessionRuntimeRecoveryTests"
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~WindowsProcessCaptureQualityTests|FullyQualifiedName~WindowsProcessSnapshotSourceTests"
```

- [ ] **Step 5: Check/review/commit:** `git diff --check`, `git status --short`, `git diff --stat`; inspect new untracked tests too. Fresh review verifies exactly one underlying capture, unchanged session timing/recovery and no observer in matcher. Stage only B3.1 files, run `git diff --cached --check`, `git diff --cached --name-status`, commit `feat(sessions): share monitor capture with discovery`, then `git status --short`, `git show --stat --oneline HEAD`, `git rev-parse "stash@{0}"`.

## Task B3.2 — Fresh inventory publication from durable Library snapshots

**Files:** `DiscoveryInventoryManager.cs` and its tests. Reuse `IExecutableInventorySource.InventoryAsync`, `IProcessSignatureLearningStore.LoadAsync`, `ProcessSignatureLearningCoordinator.InitializeAsync/PrepareEpisodeAsync`, `WindowsExecutablePath.NormalizeRoot`, and `DiscoveryInventoryContext`; no new filesystem crawler.

**Interface:** `DiscoveryInventoryManager.MarkRefreshing()` makes old contexts Pending before a local rescan; `Schedule(LibrarySnapshot snapshot, CancellationToken cancellationToken)` owns asynchronous inventory work after a durable initial/postscan snapshot; `RequestEpisodePreparation(InstallationId installationId, CancellationToken cancellationToken)` schedules a fresh inventory **for that installation only** after a completed episode or invalidation, outside the monitor cycle; `DiscoveryInventoryContext? GetCurrent(InstallationId installationId)` returns null while that scope is pending/stale; `Task AwaitIdleAsync(CancellationToken)` exists for deterministic tests/shutdown only; `Task StopAsync(CancellationToken)` cancels/joins owned work. Its constructor receives only the actual inventory/learning/coordinator dependencies and `ILogger<DiscoveryInventoryManager>`. Keep versioned scheduling state private; publish each complete scope/generation by an atomic immutable dictionary replacement, never a partially scanned scope.

- [ ] **Step 1: RED:** `Initial_snapshot_schedules_without_waiting_for_scan`; `Only_present_installations_are_inventoried`; `Restart_same_inventory_reuses_persisted_generation_and_reference`; `Changed_revision_rotates_generation_and_suspends_old_context`; `Unchanged_rescan_retains_reference`; `Removed_installation_suspends_discovery_and_discards_old_publication`; `Reappearing_changed_installation_starts_new_generation`; `Rescan_pending_returns_null_for_validation`; `Stale_inventory_result_never_publishes`; `Overlapping_roots_are_ambiguous`; `Incomplete_or_inaccessible_root_never_authorizes_promotion`; `Post_episode_preparation_inventories_only_its_installation`; `Fresh_unchanged_preparation_preserves_reference_and_trailing_absences`; `Cancellation_drops_late_publication`. Use real `LibrarySnapshot`/`GameInstallation` and fake bounded inventory; arrange a durable B2 learning state for restart. The RED must show absent manager or failing executed scheduling/generation assertions.
- [ ] **Step 2: Run RED:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~DiscoveryInventoryManagerTests"
```

- [ ] **Step 3: GREEN:** On full snapshot schedule, increment a work revision and make relevant contexts Pending; on `MarkRefreshing`, make old contexts Pending **before** `ScanAllAsync`. Snapshot only present installations; canonicalize roots via Platform, reject missing/overlapping roots. For each installation load B2 persisted inventory first, reuse persisted generation for same root/presence, otherwise create a new generation, run real `InventoryAsync` off the monitor/Dispatcher path (the Platform method does synchronous filesystem work before returning `Task.FromResult`, so the manager must own an awaited background `Task.Run` rather than call it on WPF). At startup call B2 `InitializeAsync` with fresh result; for a post-episode fresh result call `PrepareEpisodeAsync` with the current learning token, preserving Reference and the two trailing known absences when unchanged. Publish each complete scope by atomic immutable dictionary replacement only if that scope's work revision, library snapshot revision and cancellation state still match; otherwise drop. A full rescan never publishes a mix of pre/postscan scopes. Repeated unchanged scans retain B2 evidence; actual changes defer validation/rotate via B2 rules. Unexpected inventory errors log a transition and yield Pending/incomplete proof, never an empty complete inventory. Do not hold SQLite transactions across recursion. No timer in manager.
- [ ] **Step 4: Run GREEN/regressions:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~DiscoveryInventoryManagerTests"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureLearningCoordinatorTests"
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~WindowsExecutableInventorySourceTests"
```

- [ ] **Step 5: Check/review/commit:** `git diff --check`, status/stat and full new-file review. Review generation reuse, no DB write/tick, stale scan rejection, no UI startup block. Stage only manager/test, cached check/name-status, commit `feat(sessions): prepare discovery inventory from library snapshots`, postverify clean status and stash SHA.

## Task B3.3 — Sequential observation and external-launch acceptance

**Files:** `ProcessDiscoveryCaptureObserver.cs`, `ProcessDiscoveryCaptureObserverTests.cs`; Core coordinator/quality tests only if their specific RED proves a B2-to-real-capture defect. Do not alter the B1 policy or add per-game heuristics.

**Interface:** implement B3.1 `IProcessCaptureObserver`. Constructor takes `DiscoveryInventoryManager`, `ProcessSignatureLearningCoordinator`, `ProcessSignatureAcceptanceService`, `ILogger<ProcessDiscoveryCaptureObserver>`, `TimeProvider`. It supplies B2 `ProcessObservationBatch(sequenceNumber, observedAtUtc, EpisodeQuality, capture.Processes)`; one monotonic sequence per completed/failed monitor cycle. `MarkCaptureGap()` marks a missing cycle for the next observation, not a disappearance. Read only the manager's immutable contexts; a manager-requested out-of-band inventory refresh occurs on B2 `IncompleteInventory`/`RevisionChanged`/unrecognized under-root path, never synchronously within the critical poll.

- [ ] **Step 1: RED:** `External_absent_present_absent_episodes_promote_without_launch_intent`; `One_completed_episode_keeps_NO_SIGNATURE`; `Promotion_waits_for_second_complete_episode`; `Observer_uses_exact_shared_batch_once_per_installation`; `Missing_or_pending_inventory_does_not_observe`; `Incomplete_capture_and_gap_clear_qualification_not_fake_end`; `Unknown_preexisting_unrelated_process_does_not_invent_new_identity`; `New_unknown_identity_during_episode_blocks_promotion`; `Ambiguous_installations_remain_NO_SIGNATURE`; `New_under_root_path_requests_inventory_outside_tick`; `Acceptance_failure_never_reports_promotion`; `Cancellation_does_not_promote_or_write_late`. Real B1/B2 policy/stores can be faked here; later B3.6 uses real SQLite. Cover external launch with no `GameLaunchService` event in the fixture. If current coordinator's blanket rejection of preexisting unrelated null-path processes prevents the positive real-capture scenario, first add the focused Core quality test, observe its behavioral RED, then adjust only that condition to distinguish previously present unknown identities from a newly appearing unknown identity while an episode is open; keep all ambiguity guards.
- [ ] **Step 2: Run RED:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessDiscoveryCaptureObserverTests"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureLearningCoordinatorQualityTests"
```

Run the second command only if that optional test file was actually added for a demonstrated B2 quality defect; never use a no-tests-matched result as RED.
- [ ] **Step 3: GREEN:** Feed a stable capture exactly once to each prepared present scope, run B2 acceptance only on `PromoteMain`, reload/reset coordinator after successful proof consumption before later observation. A completed decision, including `AwaitingIndependentEpisode`, requests `DiscoveryInventoryManager.RequestEpisodePreparation` outside the critical poll so the next episode has a fresh full inventory; a new/changed under-root path requests the same preparation immediately after invalidation. Never inventory recursively on each stable tick. Deduplicate transition logs by installation/generation/reason; never print each PID every 2 s. Preserve complete persisted Reference when a *mere new episode start* or shutdown interrupts Current; only observed contradiction/quality/scope change invalidates it. Launching externally uses the same path, identity, two known absences and two complete episodes. No URI interception or extra snapshot source.
- [ ] **Step 4: Run GREEN/regressions:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessDiscoveryCaptureObserverTests"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureLearningCoordinatorTests|FullyQualifiedName~ProcessSignatureAcceptanceServiceTests|FullyQualifiedName~ProcessSignatureLearningCoordinatorQualityTests"
```

If no optional Core test exists, omit only its filter term. Also run B2 Data pipeline targeted. Review false-negative policy, restart proof and no per-tick DB/inventory write before staging only task files. `git diff --check`; cached check/name-status; commit `feat(sessions): observe external launches for discovery`; postverify clean status/stash.

## Task B3.4 — Startup/rescan registration and Pending Discovered recovery

**Files:** `LocalStartupPipeline.cs`, `ApplicationStartupCoordinator.cs`, `App.xaml.cs`, `PlaySteadHost.cs`, two TaskB3 bootstrap test files. Existing startup constructors/operation records must remain compatible with historical tests (append optional constructor/operation members only where real source requires it). Do not change `BackgroundServiceExceptionBehavior` or Dispatcher policy.

- [ ] **Step 1: RED:** `Host_registers_one_inventory_manager_one_observer_and_validator`; `SessionRuntime_receives_registered_validator_and_observer`; `Initial_inventory_is_scheduled_after_durable_snapshot_before_discovered_matching`; `Cached_Library_is_shown_without_waiting_for_inventory`; `Rescan_marks_old_inventory_pending_before_scan_and_publishes_new_snapshot_after_commit`; `Identical_rescan_preserves_reference`; `Startup_pending_discovered_session_has_no_heartbeat_until_validation`; `Pending_then_unchanged_valid_recovery_uses_fresh_capture`; `Pending_then_changed_binary_invalidates`; `Pending_then_inaccessible_binary_invalidates`; `Pending_then_removed_installation_invalidates`; `Manual_and_BuiltIn_sessions_run_while_discovery_pending`; `Invalid_startup_revision_ends_at_last_persisted_reliable_time`; `Cancellation_leaves_recovery_pending_for_next_start`. Use the actual `ApplicationStartupCoordinator.Operations`, `LocalStartupPipeline`, host DI and real `SessionRuntime` tests, no source-text DI assertions when resolved services can be tested.
- [ ] **Step 2: Run RED:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~TaskB3DiscoveryStartupTests|FullyQualifiedName~TaskB3DiscoveryRegistrationTests"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SessionRuntimeDiscoveredSignatureTests"
```

- [ ] **Step 3: GREEN:** Register the actual B1 `IExecutableInventorySource`/B2 `IExecutableRevisionSource`, learning/discovery stores implemented by existing SQLite types, policy, coordinator, acceptance, inventory manager, capture observer, `IDiscoveredSignatureValidator`. Alias `IProcessSignatureDiscoveryStore` to the already registered singleton `SqliteProcessSignatureStore` that also implements `IProcessSignatureStore`; do not create two competing store instances for one logical authority. Use one singleton per stateful component; callback `manager.GetCurrent` is the sole fresh inventory authority for acceptance and validation. Adapt `SessionRuntime` DI construction explicitly so both optional validator/observer are supplied; do not rely on optional-service constructor guessing. `LocalStartupPipeline` schedules initial snapshot after DB health/snapshot without awaiting inventory enumeration, calls `manager.MarkRefreshing()` *before* relevant `ScanAllAsync`, and schedules the post-commit durable snapshot. `ApplicationStartupCoordinator`/`App` retain cache-display-before-background-refresh behavior, own manager background tasks via app lifetime cancellation, and stop/join before host disposal. If an operation hook is needed for scheduling, add it at the actual lifecycle point with compatible optionality; no second loader/scan or synchronous Dispatcher wait. Discovered Valid remains Pending until complete fresh inventory/revision validation; Manual/BuiltIn bypass Pending. No Pending heartbeat/backfill.
- [ ] **Step 4: Run GREEN/regressions:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~TaskB3DiscoveryStartupTests|FullyQualifiedName~TaskB3DiscoveryRegistrationTests|FullyQualifiedName~LocalStartupPipelineTests|FullyQualifiedName~Task09SessionStartupOrderingTests|FullyQualifiedName~SessionRuntimeRegistrationTests"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~SessionRuntimeDiscoveredSignatureTests|FullyQualifiedName~SessionRuntimeRecoveryTests"
```

- [ ] **Step 5: Check/review/commit:** `git diff --check`, status/stat, review exact DI graph/startup ordering, cache UI nonblocking, Bugfix A Dispatcher tests and no Task7/Home edit. Stage only B3.4 files, cached check/name-status, commit `feat(sessions): wire discovery into local startup and monitor`, postverify clean status/stash.

## Task B3.5 — Fault isolation, shutdown and bounded cycle work

**Files:** `ProcessDiscoveryLifecycleTests.cs` and only the B3.5 owning production files if its RED exposes a defect. No `BackgroundServiceExceptionBehavior` change, no global silent `catch(Exception)`; capture failures and discovery persistence failures have different semantics.

- [ ] **Step 1: RED:** `Inventory_error_degrades_only_discovery_and_logs_once`; `Learning_store_failure_does_not_stop_Manual_or_BuiltIn_sessions`; `Acceptance_store_failure_never_reports_Valid`; `Expected_capture_error_marks_gap_without_confirming_absence`; `Cancelled_shutdown_drops_late_inventory_publication_and_no_late_DB_write`; `Completed_proof_survives_shutdown_but_Current_does_not`; `No_recursive_inventory_or_learning_write_on_stable_tick`; `Revision_reads_are_targeted_only_for_active_discovered_candidates`; `No_dispatcher_cross_thread_notification_regression`. Use counters/fault-injection for the real interfaces, no arbitrary SLA. An unexpected source/testhost failure is not RED.
- [ ] **Step 2: Run RED:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessDiscoveryLifecycleTests"
```

- [ ] **Step 3: GREEN:** Catch/log only discovery-boundary inventory/persistence faults and leave manager context Pending; continue the already captured `SessionRuntime` path for explicit signatures. Rethrow cancellation. For an expected *capture-source* fault, mark a B2 quality gap and skip matching/heartbeat for that failed cycle; allow `SessionMonitor` to wait for its normal next tick without declaring process exit. No catch(Exception) that swallows unexplained program defects. Manager/observer dispose or cancel/join before SQLite/host teardown; ignore stale work revision after stop. Transition logs are structured and deduped by reason/generation, not per process/tick. Measure actual cycle work in tests/counters and real gate, do not invent latency limits.
- [ ] **Step 4: Run GREEN/regressions:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessDiscoveryLifecycleTests|FullyQualifiedName~LibrarySessionThreadAffinityTests|FullyQualifiedName~SessionMonitorTests"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessSignatureLearningCoordinatorTests|FullyQualifiedName~SessionRuntimeHeartbeatPersistenceTests"
```

- [ ] **Step 5: Check/review/commit:** `git diff --check`, status/stat and full scope/fault-log review. Do not silence unexpected exceptions or adjust host exception behavior. Stage exact task files, cached check/name-status, commit `fix(sessions): isolate discovery failures from session monitoring`, postverify clean status/stash.

## Task B3.6 — Production pipeline, full automated gate and real acceptance

**Files:** new `ProcessDiscoveryProductionPipelineTests.cs`; use unchanged B1/B2/runtime tests. Fix an owning B3 production file only after a specific newly observed RED and fresh review. Tests use local temporary installation, real Platform inventory/revisions and real schema-6 SQLite, but fake process captures; they never launch a commercial game or alter the user's production DB.

- [ ] **Step 1: RED:** `Real_library_snapshot_inventory_two_episodes_then_exact_signature`; `Restart_retains_reference_but_discards_Current`; `After_promotion_next_cycle_only_then_two_snapshots_start_session`; `Heartbeat_and_history_use_only_future_observations`; `Ambiguous_installation_refuses_without_signature`; `Revision_change_suspends_discovered_no_name_fallback`; `Manual_and_BuiltIn_are_untouched`; `No_second_capture_or_DB_write_per_stable_tick`; `Historical_session_requires_page_reopen_to_refresh_current_UI`. The last test can instead be a targeted existing `SessionsView` contract assertion if a WPF page-load test is inappropriate; do not implement a history timer. A test must earn its signature through real inventory + two completed episodes + acceptance, never seed it directly for the positive case.
- [ ] **Step 2: Run RED:**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore --filter "FullyQualifiedName~ProcessDiscoveryProductionPipelineTests"
```

If this is already GREEN because B3.1-B3.5 cover the behavior, retain it as integration evidence; do not manufacture a RED. Any revealed defect gets its own focused RED and minimal GREEN before the final gate.
- [ ] **Step 3: GREEN/review:** Verify the whole pipeline with real SQLite/Platform and fake deterministic process source. Review cumulative B3 diff against the spec; inspect startup/scan ownership, no-backfill, Manual/BuiltIn, path+revision, cancellations, structured logs, bounded work and Task7 isolation. Fresh code/spec reviewers resolve findings through observed failing tests. No UI manual correction, Task7, provider network or GameLaunchService interception added.
- [ ] **Step 4: Fresh automated gate after last correction:**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --no-restore
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore
dotnet build ".\PlayStead.sln" --configuration Release --no-restore
git diff --check
git status --short
git stash list
git rev-parse "stash@{0}"
```

Record actual counts including failed/skipped/notExecuted and build warning/error counts; require all executed tests PASS and Release 0 warning/0 error. A test-run total alone is not a product gate. Check cumulative B3 changed files and absence of Home/Task7/Providers/process-signature exceptions; pre/post commit clean index/worktree and original stash SHA.
- [ ] **Step 5: Real runtime/product/visual gate in the running application:** Start the freshly built product with `dotnet run --project ".\src\PlayStead.UI\PlayStead.UI.csproj" --configuration Release --no-build`. On a safe **actually simple** present installation (or a controlled local test program in a distinct test installation), record real inventory root, candidates/path/revisions, timestamps, generation, reasons and DB facts without manually injecting a signature. Begin with no automatic signature. Observe a full first episode; assert persisted Reference, no session/backfill and silent UX. Stop/restart PlayStead, confirm the same complete generation/reference and fresh known absence. Observe a later independent second episode; require actual `PromoteMain`, exact Discovered Valid path and consumed proofs only if the spec predicate is fully met. On a later third run, confirm two-snapshot session start, 5 s persisted heartbeat, observed-only end, recent durable history and Sessions page reload. Confirm Library live indication and no WPF/thread crash. If the selected game is ambiguous, it is a negative case, **not** a substitute for a positive gate.
- [ ] **Step 6: Real ambiguity/protection/revision gate:** Gray Zone Warfare may correctly remain `NO SIGNATURE`: observed `GZWClientEAC.exe` and on-disk `GZWClientSteam-Win64-Shipping.exe` are insufficient while the second process path/relation is unproved or coextensive. Record the real refusal reason without AppID/title-specific code or invented path; repeat launches never override ambiguity. Verify a changed real candidate revision suspends Discovered until fresh generation/two new episodes; verify an existing Manual/BuiltIn case still follows historical sessions. Verify one poller/no per-tick recursive scan/DB proof write and actual performance baseline, **no invented SLA**. A stale open Sessions page must be reloaded before diagnosing DB persistence; any separate refresh defect is filed separately.
- [ ] **Step 7: Controlled final commit/postverify:** Only after both full automated and real gates plus reviews, stage only B3.6 tests and any separately justified RED-fix file, cached check/name-status/stat, commit `feat(sessions): validate production discovery end to end`; then verify clean worktree/index, exact commit, unchanged stash SHA and all B3 commits. Do not push/merge or pop Task7. If real positive or ambiguity proof is unavailable, leave B3.6 open/uncommitted and report the missing evidence rather than declaring discovery fixed.

## Final B3 Gate and plan self-review

`B3_GREEN=True` and `SESSION_DISCOVERY_FIXED=True` require the fresh automated gate **and** real positive end-to-end learning/signature/future session/heartbeat/history **and** real ambiguity refusal **and** Manual/BuiltIn/revision/runtime/visual/product-coherence evidence, reviews, controlled commits and postverify. If any real positive evidence is absent, both are `False`. B1/B2 GREEN and this planning commit establish neither B3 implementation nor a fixed real session discovery.

Self-review before implementation:

1. One owner: existing `SessionRuntime` captures once via `SessionMonitor`; observer has no `IProcessSnapshotSource` or independent timer. Preloaded signatures enforce next-cycle visibility and no synthetic learning-time sessions.
2. Inventory starts from durable snapshot, is asynchronous relative to cached UI, only present installations, whole-generation publication, pending on refresh/error, stale work dropped. Persisted generation reuse prevents a clean restart from invalidating Reference merely because a new Guid was fabricated.
3. B2 coordinator/acceptance/validator and B1 policy are reused; no replacement scoring, automatic exclusions, game-specific branch or launch-intent requirement. External launches work from genuine absent/present/absent evidence.
4. Actual coordinator unknown-identity logic is flagged for focused behavioral verification: previously present unrelated inaccessible processes should not silently become invented new identities, yet newly appearing unknown identities during a learning episode remain disqualifying.
5. Valid Discovered startup is Pending until fresh inventory/revision proof; no heartbeat while Pending, no time backfill; Manual/BuiltIn remain available. Cancellation/faults never turn failed capture into absence.
6. B3.1-B3.6 give executable targeted RED/GREEN commands, bounded files, regressions, review, controlled commits and postverify. Approximate **60-75 new RED test cases** across eight new test classes plus one optional focused Core class; theory rows may change actual count. No CI minimum is invented.
7. Real positive and negative gates are separate. Sessions historical refresh-on-load is treated as an independent UI limitation. The plan does not claim a visual/product gate from automated tests or a Gray Zone refusal alone.

This document is a plan only. None of the checkbox steps above have been executed by creating/committing it.
