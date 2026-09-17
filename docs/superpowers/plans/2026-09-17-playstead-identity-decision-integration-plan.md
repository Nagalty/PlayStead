# PlayStead Identity Decision Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter les décisions humaines locales `UserConfirmed` et `UserRejected` aux résolutions `MatchProbable` et `Ambiguous`, avec persistance, transactions, filtrage automatique, intégration Notification Center et révocation réversible.

**Architecture:** `game_identity_resolutions` reste l’état automatique. Une table `game_identity_decisions` et son store portent l’historique humain. Un service transactionnel orchestre les transitions et met à jour `games.canonical_content_id`. Le pipeline Phase 2A est enrichi par ce service ; l’UI réutilise exclusivement le Notification Center existant.

**Tech Stack:** C#/.NET, SQLite via Microsoft.Data.Sqlite, WPF, CommunityToolkit.Mvvm, tests NUnit.

**Spec:** `docs/superpowers/specs/2026-09-17-playstead-identity-decision-integration-design.md`

## Global Constraints

- TDD strict RED → GREEN ; chaque tâche est indépendante, reviewable et se termine par un gate et un commit unique.
- Chaque tâche indique les chemins exacts Create/Modify/Test, ses interfaces consommées/produites et ses commandes.
- Aucun réseau, serveur, provider live, fuzzy matching, ML, cloud, event sourcing, nouvelle page Identity, nouvelle dépendance ou refactor opportuniste.
- Les décisions humaines ne sont jamais écrasées par un scan automatique ; toutes les transitions critiques sont atomiques et annulables.
- Les migrations historiques et les contrats Phase 2A/2B restent compatibles.

## Task 1 — Core Decision Contracts

**Create:** `src/PlayStead.Core/Identity/IdentityDecisionId.cs`, `IdentityDecisionType.cs`, `GameIdentityDecision.cs`, `src/PlayStead.Core/Persistence/IIdentityDecisionStore.cs`, `src/PlayStead.Core/Identity/IIdentityDecisionService.cs`.

**Test:** `tests/PlayStead.Core.Tests/Identity/IdentityDecisionModelTests.cs`.

**Contracts produits:** `IdentityDecisionType` (`UserConfirmed=1`, `UserRejected=2`); `IIdentityDecisionStore` expose `GetActiveConfirmedAsync(GameId, CancellationToken)`, `ListActiveRejectedAsync(GameId, CancellationToken)`, `ListActiveAsync(GameId, CancellationToken)`, `InsertAsync(GameIdentityDecision, CancellationToken)`, `RevokeAsync(IdentityDecisionId, DateTimeOffset, CancellationToken)`; `IIdentityDecisionService` expose `ConfirmAsync(GameId, CatalogContentId, DateTimeOffset, CancellationToken)`, `RejectAsync(GameId, CatalogContentId, DateTimeOffset, CancellationToken)`, `RevokeConfirmedAsync(GameId, DateTimeOffset, CancellationToken)`.

**Consumed:** `GameId`, `CatalogContentId`, existing identity and notification contracts. No SQLite in Core.

**TDD:** add tests first; run `dotnet test .\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~IdentityDecisionModelTests"`; confirm missing-contract RED; implement immutable records/enums and nullability rules; rerun targeted then Core identity tests.

**Gate/commit:** Core project build `/warnaserror`, `git diff --check`; commit `feat(identity): add human decision contracts`.

## Task 2 — SQLite schema v10

**Create:** `src/PlayStead.Data/Database/Migrations/010_identity_decisions.sql`.
**Modify:** `src/PlayStead.Data/Database/DatabaseInitializer.cs` only for target version/resource discovery if required; update only current/latest schema expectations in `tests/PlayStead.Data.Tests`.
**Test:** `tests/PlayStead.Data.Tests/Database/IdentityDecisionDatabaseTests.cs`.

Schema: `game_identity_decisions(decision_id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(game_id) ON DELETE CASCADE, catalog_content_id TEXT NOT NULL, decision_type INTEGER NOT NULL CHECK(decision_type IN (1,2)), created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, revoked_utc TEXT NULL)`. Add partial unique index on `game_id` where `decision_type=1 AND revoked_utc IS NULL`; add partial unique index preventing active confirmed/rejected overlap for the same `(game_id,catalog_content_id)`; no uniqueness on rejected rows, no cross-database FK, and retain revoked history. Migration v9→v10 preserves all rows.

**TDD:** tests for fresh v10, v9→v10, constraints, multiple rejects, FK and absence of cross-db FK; run targeted RED, implement SQL/resource registration, run GREEN. Gate with Data build and database regression tests; commit `feat(identity): add decision schema v10`.

## Task 3 — SQLite Identity Decision Store

**Create:** `src/PlayStead.Data/Identity/SqliteIdentityDecisionStore.cs`.
**Modify:** `src/PlayStead.Data/PlayStead.Data.csproj` only if source inclusion is needed.
**Test:** `tests/PlayStead.Data.Tests/Identity/SqliteIdentityDecisionStoreTests.cs`.

Implement `IIdentityDecisionStore` with `Mode=ReadWrite;Pooling=False`, parameterized exact lookups, UTC ISO persistence, cancellation propagation, and no transition policy. Active queries require `revoked_utc IS NULL`; history remains queryable only through the explicit history method if added in Task 1.

TDD targeted RED/GREEN for roundtrip, active filtering, multiple rejects, revoked history and cancellation. Gate Data full; commit `feat(identity): persist human decisions`.

## Task 4 — Transactional Human Decision Service

**Create:** `src/PlayStead.Core/Identity/IdentityDecisionService.cs`; `src/PlayStead.Data/Identity/SqliteIdentityDecisionService.cs` only if transaction ownership must be Data-side.
**Modify:** existing library lookup/reconciler contracts only when an exact existing signature cannot support atomic update.
**Test:** `tests/PlayStead.Data.Tests/Identity/IdentityDecisionServiceTests.cs`.

The selected service implementation must execute one SQLite transaction for confirm, reject and revoke. Confirm revokes prior active confirmation, revokes same-candidate rejection, inserts/activates confirmation and updates `games.canonical_content_id`. Rejecting a confirmed candidate revokes it, clears the canonical and inserts rejection; rejecting another candidate only inserts rejection. Revoke clears canonical and retains rejected rows. Preserve GameId, installations and sessions; rollback all writes on missing GameId or any failure; propagate cancellation.

TDD each transition RED then GREEN, including timestamps and idempotence. Gate Core/Data identity suites; commit `feat(identity): add transactional decision service`.

## Task 5 — Decision-Aware Identity Pipeline

**Modify:** `src/PlayStead.Core/Scanning/LocalIdentityResolutionCoordinator.cs` and its smallest required collaborator; preserve `IGameIdentityResolver` Phase 2A behavior.
**Test:** `tests/PlayStead.Core.Tests/Scanning/DecisionAwareIdentityPipelineTests.cs`, plus existing coordinator regressions.

Before automatic resolution, read active confirmation and use it directly. Otherwise resolve normally and remove active rejected candidates before proposing/reconciling. All-rejected cases retain PS-TEMP and clear no unrelated state; a future non-rejected candidate is eligible. No scoring is added; Phase 2A `MatchConfirmed/New` remains valid.

Run targeted RED/GREEN, then all Identity and scan tests; commit `feat(identity): honor human decisions in resolution pipeline`.

## Task 6 — Notification Decision Integration

**Modify:** `src/PlayStead.Core/Notifications/IdentityNotificationProducer.cs`, `NotificationCenterService.cs`, and the minimal decision service collaborator.
**Test:** `tests/PlayStead.Core.Tests/Notifications/IdentityDecisionNotificationTests.cs`.

Confirm/choose resolves the stable Identity notification. Partial rejection leaves it active; all known candidates rejected may resolve it. Revoke permits reactivation through the existing Phase 2B deduplication key. Propagate cancellation and isolate only non-cancellation side-effect failures.

Run targeted RED/GREEN and notification regressions; commit `feat(identity): integrate decision notification lifecycle`.

## Task 7 — Notification Center Decision UI

**Modify:** `src/PlayStead.UI/Notifications/NotificationCenterViewModel.cs`, `NotificationPanel.xaml`, `NotificationPanel.xaml.cs`.
**Test:** `tests/PlayStead.UI.Tests/Notifications/IdentityDecisionNotificationUiTests.cs`.

Expose actions only for relevant Identity `ActionRequired` records. Confirm, reject and candidate selection call the decision service with exact GameId/CatalogContentId; refresh panel and badge after success; show minimal busy/non-critical error state; do not expose raw payload. Other notification types expose no decision controls. No redesign or new page.

Run targeted RED/GREEN, then Notification/Shell UI regressions; commit `feat(identity): add notification decision actions`.

## Task 8 — Revocation / Reconsideration Entry Point

**Modify:** the existing Notification Center history surface chosen after inspection, limited to `NotificationCenterViewModel`/`NotificationPanel` if that is the natural surface.
**Test:** `tests/PlayStead.UI.Tests/Notifications/IdentityDecisionRevocationTests.cs`.

Add one `Annuler le choix` action for an active human confirmation. It calls `RevokeConfirmedAsync`, clears canonical through the service, retains rejections, and refreshes the panel. No new route or page is created.

Run targeted RED/GREEN and UI regressions; commit `feat(identity): add decision revocation entry point`.

## Task 9 — DI and runtime wiring

**Modify:** `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`, `LocalStartupPipeline.cs` only where registrations and command construction require it.
**Test:** `tests/PlayStead.UI.Tests/Bootstrap/IdentityDecisionStartupTests.cs`.

Register decision store/service, decision-aware coordinator and UI command dependencies while preserving Phase 2A/2B startup order and cancellation behavior. No identity work is added to the database-only initialization path.

Run targeted RED/GREEN, Bootstrap/Identity/Notification suites and strict UI build; commit `feat(identity): wire human decision integration`.

## Task 10 — Final Acceptance Gate Phase 2C

**Test/artifact:** `tests/PlayStead.Core.Tests`, `tests/PlayStead.Data.Tests`, `tests/PlayStead.UI.Tests`; no production changes.

Run with `$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1'` and `-m:1`: Core full, Data full, UI full, solution build `/warnaserror`. Verify schema v10, all transactional invariants, priority/filtering, PS-TEMP retention, notification lifecycle, confirm/reject/choose/revoke UI, DI, `git diff --check`, and clean worktree. Isolate known SQLite Dispose flakes without weakening assertions.

If and only if there is no functional failure, declare `PHASE2C_IDENTITY_DECISION_INTEGRATION=GREEN`; commit `test(identity): close Phase 2C acceptance` only after the gate is complete.

## Global Out of Scope

No central server, network, live IGDB/SteamGridDB/RAWG, new fuzzy matcher, ML scoring, advanced ranking, cloud sync, multi-user collaboration, event sourcing, physical history deletion, dedicated Identity page, global Notification Center/Library redesign, or new NuGet dependency.
