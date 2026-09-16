# PlayStead Notification Center Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:**

Implémenter le Notification Center local de PlayStead 0.4.1 en respectant la spec Phase 2B. Les notifications sont persistées dans `playstead.db`, dédupliquées par producteur/sujet/raison, non bloquantes pour le scan et le runtime, et exposées par une cloche avec panneau latéral droit.

**Architecture:**

- Core porte les enums, identifiants, records, contrats de store, service métier et contrat du producteur Identity.
- Data porte la migration SQLite v9 et `SqliteNotificationStore`; ses méthodes ne décident pas du cycle de vie.
- UI porte le ViewModel du centre, la cloche, le badge, le panneau droit et le bootstrap de rétention.
- `IdentityNotificationProducer` adapte les résultats de `LocalIdentityResolutionCoordinator` sans ajouter de décision humaine.
- `LocalStartupPipeline` reste l’orchestrateur du scan; les erreurs non liées à l’annulation du centre sont isolées après la persistance du scan.

**Tech Stack:**

C# / .NET 10, xUnit, WPF, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, Microsoft.Data.Sqlite, System.Text.Json, `TimeProvider`, CancellationToken.

**Spec:**

`docs/superpowers/specs/2026-09-16-playstead-notification-center-design.md`

## Global Constraints

- Chaque tâche suit RED → validation du RED → GREEN minimal → validation GREEN.
- Chaque tâche modifie uniquement les fichiers listés dans sa section.
- Aucun serveur, réseau, provider distant, télémétrie, event bus, event sourcing, toast Windows, push, e-mail, snooze, dismiss générique, muting, préférence avancée ou décision Phase 2C.
- Les commandes utilisent `$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"` et `-m:1` lorsque le projet UI ou la solution est exécuté.
- Les tests ne sont considérés passants qu’après une exécution réelle avec sortie et code de sortie nul.
- Les locks SQLite survenant exclusivement dans `Dispose` sont relancés isolément et documentés comme `SQLITE_DISPOSE_FLAKE`; aucune assertion ni fixture n’est affaiblie.
- Chaque tâche se termine par `git diff --check`, `git status --short`, une revue des fichiers autorisés, puis un commit unique au message indiqué.
- Les tests historiques de migration v1 à v8 restent inchangés sauf assertions explicitement nommées de version courante.

## Task 1 — Core Notification Contracts

**Files**

- Create: `src/PlayStead.Core/Notifications/NotificationId.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationState.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationPriority.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationProducer.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationDeduplicationKey.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationRecord.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationPublishRequest.cs`
- Create: `src/PlayStead.Core/Notifications/NotificationListFilter.cs`
- Create: `src/PlayStead.Core/Persistence/INotificationStore.cs`
- Create: `src/PlayStead.Core/Notifications/INotificationCenterService.cs`
- Create: `tests/PlayStead.Core.Tests/Notifications/NotificationModelTests.cs`

**Exact contracts**

`NotificationId` is a readonly record struct containing `Guid Value`, with `New()`, `ToString()`, and strict `Parse(string)`/ `TryParse(string, out NotificationId)`.

Enums use exactly these values:

```text
NotificationState: Unread=1, Read=2, Resolved=3
NotificationPriority: Info=1, Warning=2, ActionRequired=3
NotificationProducer: IdentityResolution=1
NotificationListFilter: Active=1, Resolved=2
```

`NotificationDeduplicationKey` is a sealed record containing one non-empty `string Value`. The value is persisted verbatim and is the only deduplication identity used by the store.

`NotificationRecord` is a sealed record with:

```csharp
NotificationId NotificationId,
NotificationProducer Producer,
string SubjectId,
string Reason,
string DeduplicationKey,
NotificationPriority Priority,
NotificationState State,
string Title,
string Message,
string? PayloadJson,
DateTimeOffset CreatedUtc,
DateTimeOffset UpdatedUtc,
DateTimeOffset? ReadUtc,
DateTimeOffset? ResolvedUtc
```

`NotificationPublishRequest` is a sealed record containing `NotificationProducer Producer`, `string SubjectId`, `string Reason`, `NotificationDeduplicationKey DeduplicationKey`, `NotificationPriority Priority`, `string Title`, `string Message`, and `string? PayloadJson`.

`INotificationStore` exposes exactly:

```csharp
Task<NotificationRecord?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken);
Task<NotificationRecord?> GetByDeduplicationKeyAsync(string deduplicationKey, CancellationToken cancellationToken);
Task InsertAsync(NotificationRecord notification, CancellationToken cancellationToken);
Task UpdateAsync(NotificationRecord notification, CancellationToken cancellationToken);
Task<IReadOnlyList<NotificationRecord>> ListActiveAsync(CancellationToken cancellationToken);
Task<IReadOnlyList<NotificationRecord>> ListResolvedAsync(CancellationToken cancellationToken);
Task<int> GetActiveCountAsync(CancellationToken cancellationToken);
Task<int> DeleteResolvedOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
```

`INotificationCenterService` exposes exactly:

```csharp
Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken);
Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken);
Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken);
Task<int> GetActiveCountAsync(CancellationToken cancellationToken);
Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken);
Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken);
```

**RED**

- [ ] Write tests for enum values, ID round-trip/invalid parse, stable deduplication value, nullable timestamps, and service/store signatures.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationModelTests"
```
- [ ] Confirm compilation fails only because the new types and interfaces are absent.

**GREEN**

- [ ] Add exactly the listed Core contracts without SQLite or service logic.
- [ ] Run the same filtered test; expect all tests in `NotificationModelTests` to pass.

**Gate and commit**

- [ ] Run Core Identity and Notification tests:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~Identity|FullyQualifiedName~NotificationModelTests"
```
- [ ] Commit: `feat(notification): add core notification contracts`

## Task 2 — SQLite schema v9

**Files**

- Create: `src/PlayStead.Data/Database/Migrations/009_notifications.sql`
- Modify: `src/PlayStead.Data/Database/DatabaseInitializer.cs`
- Create: `tests/PlayStead.Data.Tests/Database/NotificationMigrationTests.cs`
- Modify only current/latest schema assertions in existing migration tests when a test explicitly expects version 8.

**Migration contract**

Set `TargetVersion = 9` and map `[9] = "009_notifications.sql"`. The migration creates `notifications` with exactly the columns in the spec. Add checks `producer IN (1)`, `priority IN (1,2,3)`, and `state IN (1,2,3)`. Add a unique index only through `deduplication_key TEXT NOT NULL UNIQUE`, an active-state index on `state`, and a resolved-retention index on `state, resolved_utc`. Do not add an occurrence table, event log, occurrence count, or cross-database foreign key.

**RED**

- [ ] Add tests proving a v8 database upgrades to v9, preserves existing games/installations/sessions/identity rows, creates the notifications table, accepts nullable `payload_json`, rejects unknown enum values, and has no cross-database foreign key.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationMigrationTests"
```
- [ ] Confirm RED is limited to missing migration/version behavior.

**GREEN**

- [ ] Add migration 009 and update `DatabaseInitializer` to target version 9.
- [ ] Update only current/latest version assertions from 8 to 9; retain all fixtures intentionally constructing v8 databases.
- [ ] Run the filtered migration tests; expect all to pass.

**Gate and commit**

- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationMigrationTests|FullyQualifiedName~Identity"
dotnet build ".\src\PlayStead.Data\PlayStead.Data.csproj" --configuration Release --no-restore -m:1 --disable-build-servers /warnaserror
```
- [ ] Commit: `feat(notification): add notifications schema v9`

## Task 3 — SQLite Notification Store

**Files**

- Create: `src/PlayStead.Data/Notifications/SqliteNotificationStore.cs`
- Create: `tests/PlayStead.Data.Tests/Notifications/SqliteNotificationStoreTests.cs`

**Consumes / produces**

Consumes `DatabaseOptions`, `INotificationStore`, and the Task 1 notification records. Produces only persistence/query behavior. All connections use `Pooling=False`; read operations use `Mode=ReadOnly;Pooling=False` where no write is required. SQL stores timestamps in round-trip invariant format and payload JSON as supplied.

**RED**

- [ ] Add tests for insert/get by ID, get by deduplication key, update, active listing, resolved listing, active count including Unread and Read but excluding Resolved, priority/date ordering, retention deletion, protection of active rows, duplicate-key rejection, and cancellation.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~SqliteNotificationStoreTests"
```
- [ ] Confirm RED is caused by the absent store.

**GREEN**

- [ ] Implement the eight `INotificationStore` methods using parameterized SQLite commands.
- [ ] List active with `state IN (1,2)` ordered by `priority DESC, updated_utc DESC, notification_id`; list resolved with `state = 3` ordered by `updated_utc DESC, notification_id`.
- [ ] Make `DeleteResolvedOlderThanAsync` delete only `state = 3 AND resolved_utc < $cutoff`; propagate cancellation before opening and on every async I/O.
- [ ] Run the filtered store tests; expect all to pass.

**Gate and commit**

- [ ] Run Data catalog, Identity, and Notification tests:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~Catalog|FullyQualifiedName~Identity|FullyQualifiedName~SqliteNotificationStoreTests"
```
- [ ] Commit: `feat(notification): persist notification records in sqlite`

## Task 4 — Notification Center Service

**Files**

- Create: `src/PlayStead.Core/Notifications/NotificationCenterService.cs`
- Create: `tests/PlayStead.Core.Tests/Notifications/NotificationCenterServiceTests.cs`

**Dependencies**

The service constructor is exactly `NotificationCenterService(INotificationStore store, TimeProvider timeProvider)`. It uses `TimeProvider.GetUtcNow()` for all timestamps and never issues SQL directly.

**RED**

- [ ] Add a fake `INotificationStore` and tests for first publication, stable deduplication, refresh preserving Read, reactivation after Resolved, MarkRead idempotence, Resolve timestamps, active count/list forwarding, 90-day purge cutoff, and cancellation.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationCenterServiceTests"
```
- [ ] Confirm RED is caused by the absent service.

**GREEN**

- [ ] First publication inserts a new ID in Unread with both timestamps equal to now and null read/resolved timestamps.
- [ ] Active refresh reads by stable key, preserves NotificationId, CreatedUtc and current state, updates content and UpdatedUtc, and keeps ReadUtc when state is Read.
- [ ] Resolved reactivation preserves NotificationId, sets Unread and clears read/resolved timestamps.
- [ ] MarkRead changes only Unread to Read; Resolve sets Resolved, ResolvedUtc and UpdatedUtc; both operations are idempotent at their terminal state.
- [ ] Purge calls store deletion with exactly `now - TimeSpan.FromDays(90)`.
- [ ] Run the filtered service tests; expect all to pass.

**Gate and commit**

- [ ] Run Core notification plus Identity tests.
- [ ] Commit: `feat(notification): implement notification lifecycle service`

## Task 5 — Identity Notification Producer

**Files**

- Create: `src/PlayStead.Core/Notifications/IIdentityNotificationProducer.cs`
- Create: `src/PlayStead.Core/Notifications/IdentityNotificationProducer.cs`
- Modify: `src/PlayStead.Core/Scanning/ILocalIdentityResolutionCoordinator.cs`
- Modify: `src/PlayStead.Core/Scanning/LocalIdentityResolutionCoordinator.cs`
- Create: `tests/PlayStead.Core.Tests/Notifications/IdentityNotificationProducerTests.cs`
- Modify: `tests/PlayStead.Core.Tests/Scanning/LocalIdentityResolutionCoordinatorTests.cs` only to inject the optional producer while retaining existing constructor calls.

**Exact producer contract**

```csharp
public interface IIdentityNotificationProducer
{
    Task PublishForResolutionAsync(
        GameId gameId,
        IdentityResolutionResult result,
        CancellationToken cancellationToken);
}
```

`IdentityNotificationProducer` consumes `INotificationCenterService`. For `MatchProbable`, publish producer IdentityResolution, subject `gameId.ToString()`, reason `match-probable`, priority ActionRequired, and deduplication key `identity:{GameId}:match-probable`. For `Ambiguous`, use reason `ambiguous` and the corresponding key. `MatchConfirmed` and `New` do not publish. When the result is MatchConfirmed, call `ListAsync(NotificationListFilter.Active)`, find active records whose deduplication key is either identity key for that GameId, then call `ResolveAsync` for each found record; a missing notification is ignored. The producer never chooses a candidate and never emits Phase 2C actions.

Add an optional nullable producer to the coordinator. Preserve all existing constructors through overloads. After resolver result handling, invoke the producer with the resolved local GameId. Wrap non-cancellation producer exceptions in the coordinator so the scan flow remains successful; rethrow `OperationCanceledException` when the caller token is cancelled.

**RED**

- [ ] Add tests for probable publish, ambiguous publish, confirmed/no-new-publication, new/no-publication, stale notification resolution, deduplication across repeated scans, non-cancellation isolation, and cancellation.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~IdentityNotificationProducerTests"
```
- [ ] Confirm RED is limited to absent producer/optional coordinator API.

**GREEN**

- [ ] Implement the producer and coordinator injection exactly as specified.
- [ ] Run producer and coordinator tests; expect all to pass.

**Gate and commit**

- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~Identity|FullyQualifiedName~Notification"
```
- [ ] Commit: `feat(notification): publish identity notifications`

## Task 6 — DI and retention bootstrap

**Files**

- Create: `src/PlayStead.UI/Bootstrap/NotificationRetentionStartup.cs`
- Modify: `src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs`
- Modify: `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`
- Create: `tests/PlayStead.UI.Tests/Bootstrap/NotificationRetentionStartupTests.cs`

**Exact bootstrap contract**

`NotificationRetentionStartup` constructor is `NotificationRetentionStartup(INotificationCenterService service)`. It exposes `Task InitializeAsync(CancellationToken cancellationToken)`. It calls `PurgeExpiredResolvedAsync`. Non-cancellation exceptions are swallowed at this boundary; cancellation is rethrown when the caller token is cancelled.

The production constructor of `LocalStartupPipeline` receives a nullable `NotificationRetentionStartup` after its current dependencies. Existing constructors remain usable. `InitializeAsync` calls the bootstrap immediately after the playstead database initializer and catalog initializer, before health check. No notification code is added to `RefreshAsync`.

Register in `PlaySteadHost`:

```text
INotificationStore -> SqliteNotificationStore
INotificationCenterService -> NotificationCenterService
NotificationRetentionStartup -> NotificationRetentionStartup
```

**RED**

- [ ] Add tests proving DI resolves the store/service/bootstrap, startup purge is invoked, non-cancellation purge failure does not fail initialization, and cancellation propagates.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationRetentionStartupTests"
```
- [ ] Confirm RED is limited to absent bootstrap and registrations.

**GREEN**

- [ ] Add the bootstrap, nullable pipeline dependency and DI registrations.
- [ ] Ensure no notification side effect is placed in `RefreshAsync`.
- [ ] Run the filtered tests; expect all to pass.

**Gate and commit**

- [ ] Run Bootstrap, Identity, and Home media regressions:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~Bootstrap|FullyQualifiedName~HomeMediaIntegrationTests"
dotnet build ".\src\PlayStead.UI\PlayStead.UI.csproj" --configuration Release --no-restore -m:1 --disable-build-servers /warnaserror
```
- [ ] Commit: `feat(notification): wire retention and notification services`

## Task 7 — Notification Center UI Shell

**Files**

- Create: `src/PlayStead.UI/Notifications/NotificationCenterViewModel.cs`
- Create: `src/PlayStead.UI/Notifications/NotificationPanel.xaml`
- Create: `src/PlayStead.UI/Notifications/NotificationPanel.xaml.cs`
- Modify: `src/PlayStead.UI/Shell/ShellViewModel.cs`
- Modify: `src/PlayStead.UI/MainWindow.xaml`
- Modify: `src/PlayStead.UI/MainWindow.xaml.cs`
- Modify: `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`
- Create: `tests/PlayStead.UI.Tests/Notifications/NotificationCenterViewModelTests.cs`
- Create: `tests/PlayStead.UI.Tests/Notifications/NotificationPanelContractTests.cs`

**Exact UI contract**

`NotificationCenterViewModel` constructor is `NotificationCenterViewModel(INotificationCenterService service)`. It exposes `IReadOnlyList<NotificationRecord> Items`, `NotificationListFilter Filter`, `int ActiveCount`, `bool IsPanelOpen`, `ICommand TogglePanelCommand`, `ICommand SelectFilterCommand`, `Task RefreshAsync(CancellationToken)`, and `Task SelectAsync(NotificationId, CancellationToken)`. Opening the panel calls `RefreshAsync` without marking records Read. `SelectAsync` calls `MarkReadAsync` for the selected record and refreshes. Closing changes only `IsPanelOpen`.

Bind the existing header `AttentionNavButton` to the center toggle while preserving its existing `NavigateAttentionCommand` route; add a distinct bell button named `NotificationBellButton` with badge `NotificationBadge`. The badge is hidden at zero and displays ActiveCount otherwise. The panel is a right-aligned `Border` named `NotificationPanelHost`, contains an ItemsControl bound to Items, and exposes two filter buttons named `ActiveNotificationsFilterButton` and `ResolvedNotificationsFilterButton`. No generic resolve/ignore button exists.

**RED**

- [ ] Add ViewModel tests for zero/nonzero badge state, Read counted in ActiveCount, Resolved excluded, panel opening without MarkRead, individual selection marking only that record Read, filter selection, priority/date ordering, and closing without mutation.
- [ ] Add source/XAML contract tests only for the named bell, badge, right panel, filter controls, existing Attention route, and absence of a generic resolve/ignore action.
- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationCenter"
```
- [ ] Confirm RED is limited to missing ViewModel/UI wiring.

**GREEN**

- [ ] Implement the ViewModel with the existing CommunityToolkit command/property notification patterns.
- [ ] Add the panel and named bindings to the existing MainWindow header; preserve Attention navigation and do not alter global visual styling.
- [ ] Register `NotificationCenterViewModel` in `PlaySteadHost`.
- [ ] Run the filtered UI tests; expect all to pass.

**Gate and commit**

- [ ] Run:
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers --filter "FullyQualifiedName~NotificationCenter|FullyQualifiedName~Bootstrap|FullyQualifiedName~Attention"
```
- [ ] Commit: `feat(notification): add notification center shell UI`

## Task 8 — Phase 2B Final Acceptance Gate

**Files**

- No production or test files are created by this task.
- Create a report only if the gate runner requires one: `D:/Dev/PlayStead/02_RAPPORTS/PLAYSTEAD_PHASE2B_FINAL_<timestamp>.txt`.

**Gate commands**

```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1 --disable-build-servers
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 --disable-build-servers /warnaserror
```

**Acceptance matrix**

- [ ] Core, Data, and UI suites complete with no functional failures.
- [ ] Schema version is 9 and migration 009 is embedded and applied.
- [ ] Notification lifecycle tests prove stable deduplication, independent Read/Resolved states, reactivation, active count, ordering, 90-day purge and cancellation.
- [ ] Identity producer tests prove publication only for MatchProbable/Ambiguous and resolution for obsolete problems.
- [ ] Retention bootstrap and all five DI registrations resolve.
- [ ] UI tests prove badge, panel, filters, ordering, individual Read behavior, and no generic resolve action.
- [ ] `git diff --check` passes and `git status --short` is empty.
- [ ] Any SQLite cleanup failure is rerun by exact test name. A test that passes in isolation is recorded as `SQLITE_DISPOSE_FLAKE`, not a functional failure.
- [ ] Build reports zero warnings and zero errors.

Final condition:

```text
PHASE2B_NOTIFICATION_CENTER=GREEN
```

only when there is no functional failure, all acceptance invariants pass, the strict build passes, and any SQLite flakes pass in isolation.

## Global out of scope

Phase 2B does not implement Phase 2C, `UserConfirmed`, `UserRejected`, manual candidate selection, occurrence history, event sourcing, event bus, server, network, IGDB, SteamGridDB, RAWG, cloud synchronization, push, e-mail, Windows toast, snooze, generic dismiss, mute rules, advanced preferences, or telemetry.

## Self-review checklist

- [ ] Re-read the complete authoritative spec.
- [ ] Every spec requirement maps to at least one task.
- [ ] Contrôler l’absence de marqueurs de spécification incomplets; toutes les recherches ne renvoient aucun résultat.
- [ ] Verify all type names and method signatures match across Tasks 1–8.
- [ ] Verify migration numbering is v8 → v9 and only current/latest assertions are updated.
- [ ] Verify every path against the repository structure.
- [ ] Verify Phase 2C remains out of scope.
- [ ] Run `git diff --check`.
