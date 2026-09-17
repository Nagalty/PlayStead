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

Schema: `game_identity_decisions(decision_id TEXT NOT NULL PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(game_id) ON DELETE CASCADE, catalog_content_id TEXT NOT NULL, decision_type INTEGER NOT NULL CHECK(decision_type IN (1,2)), created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, revoked_utc TEXT NULL)`. Use exactly two unique partial indexes, with no trigger, extra table, or extra column:

```sql
CREATE UNIQUE INDEX ux_game_identity_decisions_active_confirm
ON game_identity_decisions(game_id)
WHERE decision_type = 1 AND revoked_utc IS NULL;

CREATE UNIQUE INDEX ux_game_identity_decisions_active_candidate
ON game_identity_decisions(game_id, catalog_content_id)
WHERE revoked_utc IS NULL;
```

The first index allows at most one active `UserConfirmed` for a `GameId`. The second allows at most one active decision of either type for a given `GameId + CatalogContentId`, therefore forbids an active confirm/reject contradiction while allowing multiple active rejects for different candidates. Thus `Game X + Candidate A + UserConfirmed`, `Candidate B + UserRejected`, and `Candidate C + UserRejected` are valid; two active confirmations for one game, two active decisions for one pair, or an active confirmation plus rejection for one pair are invalid. A row with non-null `revoked_utc` leaves both indexes, so a new active decision for the same game/candidate is allowed and history is retained. There is no cross-database FK. Migration v9→v10 preserves all rows.

**TDD:** tests for fresh v10, v9→v10, table/columns, FK and absence of cross-db FK, invalid `decision_type`, second active confirmation for the same game with another candidate (fails), active confirmation plus active rejection for the same pair (fails), multiple active rejects for different candidates (pass), revoked pair followed by a new active decision (pass), and revoked confirmation followed by a new confirmation for another candidate (pass). Run targeted RED, implement SQL/resource registration, run GREEN. Gate with Data build and database regression tests; commit `feat(identity): add decision schema v10`.

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

## Task 6 — Identity Decision Context Foundation

**Create:** `src/PlayStead.Core/Identity/IdentityDecisionCandidate.cs`, `IdentityDecisionContext.cs`, `IIdentityDecisionContextProvider.cs`.
**Test:** `tests/PlayStead.Core.Tests/Identity/IdentityDecisionContextTests.cs`.

Cette tâche transporte explicitement les candidats fournis par une source locale ou synthétique ; elle ne découvre aucun candidat et n’ajoute ni fuzzy matching, ni ranking, ni scoring, ni réseau. `IdentityDecisionCandidate` contient au minimum `CatalogContentId` et les seules données d’affichage déjà disponibles. `IdentityDecisionContext` contient `GameId`, `IdentityResolutionState`, une collection immuable de candidats et l’instant d’observation selon les conventions existantes. `IIdentityDecisionContextProvider` expose `Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken)`.

Le provider filtre les `UserRejected` actifs via `IIdentityDecisionStore` avant d’exposer le contexte. Un candidat restant produit `MatchProbable`, au moins deux produisent `Ambiguous`, zéro produit un contexte fonctionnel `New/provisional`. Un `UserConfirmed` actif retourne `null`. Le PS-TEMP existant est conservé et aucune liaison canonique n’est créée par cette tâche.

TDD : RED ciblé sur les contrats et le provider synthétique, puis GREEN minimal pour les cardinalités, le filtrage exact, la conservation des candidats non rejetés, le fallback zéro candidat, la cancellation et l’absence de dépendance réseau. Gate Core identity et commit `feat(identity): add decision context foundation`.

## Task 7 — Decision Notification Orchestration Foundation

**Create:** `src/PlayStead.Core/Notifications/IIdentityDecisionNotificationOrchestrator.cs`, `IdentityDecisionNotificationOrchestrator.cs`.
**Test:** `tests/PlayStead.Core.Tests/Notifications/IdentityDecisionNotificationOrchestratorTests.cs`.

Cette couche consomme exclusivement `IIdentityDecisionService`, `IIdentityDecisionContextProvider` et `INotificationCenterService`. Elle expose `ConfirmAsync(GameId, CatalogContentId, CancellationToken)` et `RejectAsync(GameId, CatalogContentId, CancellationToken)` ; le choix UI d’un candidat est un `ConfirmAsync`. `IIdentityNotificationProducer` reste réservé aux résultats automatiques du resolver.

Avant une action, l’orchestrateur charge le contexte, vérifie l’état et la présence exacte du candidat, puis appelle le service de décision. Après une décision réussie, il recharge le contexte et applique les clés stables `identity:{GameId}:match-probable` et `identity:{GameId}:ambiguous` : confirmation résout la notification correspondante ; rejet résout l’ancienne notification si zéro candidat, conserve/rafraîchit Ambiguous si deux candidats ou plus, et effectue la transition Ambiguous → MatchProbable (résolution de l’ancienne puis publication/réactivation de la nouvelle) si un seul candidat reste. Il ne crée aucune `NotificationId` parallèle.

Une erreur non-cancellation du Notification Center ne rollback jamais la décision humaine ; une erreur du service de décision ne modifie aucune notification. Une cancellation est propagée. Aucun SQL, réseau, matching, scoring ou ranking dans l’orchestrateur.

TDD : RED puis GREEN pour confirm MatchProbable/Ambiguous, rejets zéro/uno/multiples, clés et réactivation stables, isolation des erreurs, cancellation et absence de rejet implicite. Gate Core notification/decision ; commit `feat(identity): add decision notification orchestrator`.

## Task 8 — Runtime Decision Context Gateway

**Create:** `src/PlayStead.Core/Identity/IIdentityDecisionContextGateway.cs`, `IdentityDecisionContextGateway.cs`, `EmptyIdentityDecisionCandidateSource.cs`.
**Test:** `tests/PlayStead.Core.Tests/Identity/IdentityDecisionContextGatewayTests.cs`.

Le gateway expose `Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken)` et délègue exclusivement à `IIdentityDecisionContextProvider`. Il ne découvre, ne filtre et ne fabrique aucun candidat. `EmptyIdentityDecisionCandidateSource` implémente `IIdentityDecisionCandidateSource`, retourne toujours une collection vide, n’utilise aucun réseau et ne produit aucune notification. Le wiring est réservé à la Task 12.

TDD : RED puis GREEN pour contexte absent, contextes synthétiques MatchProbable/Ambiguous, candidats préservés, UserRejected déjà filtré, UserConfirmed sans contexte, source vide, cancellation, absence de mutation DB et absence de notification. Gate Core identity ; commit `feat(identity): add runtime decision context gateway`.

## Task 9 — Notification Decision Integration

**Create:** `src/PlayStead.Core/Identity/IIdentityDecisionApplicationService.cs`, `src/PlayStead.Core/Identity/IdentityDecisionApplicationService.cs`.
**Test:** `tests/PlayStead.Core.Tests/Identity/IdentityDecisionApplicationServiceTests.cs`.

`IIdentityDecisionApplicationService` exposes exactement :

```csharp
Task<IdentityDecisionContext?> GetContextAsync(GameId gameId, CancellationToken cancellationToken);
Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
```

`IdentityDecisionApplicationService` dépend uniquement de `IIdentityDecisionContextGateway` et `IIdentityDecisionNotificationOrchestrator`. GetContext délègue au gateway ; Confirm/Reject délèguent exclusivement à l’orchestrateur. Aucun accès direct aux services de décision, stores, SQL ou Notification Center, aucun filtrage, déduplication, calcul de contexte, matching ou réseau.

TDD : RED puis GREEN pour délégation, nullité et préservation des contextes, transmission des identifiants, cancellation et absence de dépendances interdites. Gate Core identity/notification ; commit `feat(identity): add decision application facade`.

## Task 10 — Notification Center Decision UI

**Modify:** `src/PlayStead.UI/Notifications/NotificationCenterViewModel.cs`, `NotificationPanel.xaml`, `NotificationPanel.xaml.cs`.
**Test:** `tests/PlayStead.UI.Tests/Notifications/IdentityDecisionNotificationUiTests.cs`.

Consume `IIdentityDecisionApplicationService` as the sole UI decision facade. Load context, confirm and reject through that facade; do not inject or call the gateway or orchestrator directly. For an Identity `ActionRequired` record, expose no action when the facade returns null; expose Confirm/Reject for one `MatchProbable` candidate and candidate selection/rejection for `Ambiguous`. Pass exact GameId/CatalogContentId, refresh panel and badge after success; with the empty production source, no artificial decision action appears. Other notification types expose no decision controls. No redesign or new page.

Run targeted RED/GREEN, then Notification/Shell UI regressions; commit `feat(identity): add notification decision actions`.

## Task 11 — Revocation / Reconsideration Entry Point

**Modify:** `src/PlayStead.Core/Identity/IIdentityDecisionService.cs`, `src/PlayStead.Data/Identity/SqliteIdentityDecisionService.cs`, `src/PlayStead.Core/Notifications/IIdentityDecisionNotificationOrchestrator.cs`, `src/PlayStead.Core/Notifications/IdentityDecisionNotificationOrchestrator.cs`, `src/PlayStead.Core/Identity/IIdentityDecisionApplicationService.cs`, `src/PlayStead.Core/Identity/IdentityDecisionApplicationService.cs`, `src/PlayStead.UI/Notifications/NotificationCenterViewModel.cs`, `src/PlayStead.UI/Notifications/NotificationPanel.xaml`.

**Create:** `tests/PlayStead.UI.Tests/Notifications/IdentityDecisionRevocationTests.cs`.

**Modify tests:** `tests/PlayStead.Data.Tests/Identity/IdentityDecisionServiceTests.cs`, `tests/PlayStead.Core.Tests/Notifications/IdentityDecisionNotificationOrchestratorTests.cs`, `tests/PlayStead.Core.Tests/Identity/IdentityDecisionApplicationServiceTests.cs`, and only the fakes in `tests/PlayStead.UI.Tests/Notifications/IdentityDecisionNotificationUiTests.cs` required by the extended application-service contract.

### Contracts exacts

`GetContextAsync` ne constitue pas une lecture de confirmation active : `IdentityDecisionContextProvider.GetAsync` retourne `null` lorsqu'un `UserConfirmed` actif existe et `IdentityDecisionContext.Create` refuse l'état `MatchConfirmed`. Sans changer la sémantique déjà livrée des Tasks 6–10, étendre les trois interfaces existantes avec les signatures suivantes et ne créer aucun nouveau type :

```csharp
// IIdentityDecisionService
Task<GameIdentityDecision?> GetActiveConfirmedAsync(
    GameId gameId,
    CancellationToken cancellationToken);

// IIdentityDecisionNotificationOrchestrator
Task<GameIdentityDecision?> GetActiveConfirmedAsync(
    GameId gameId,
    CancellationToken cancellationToken);
Task RevokeConfirmedAsync(
    GameId gameId,
    CancellationToken cancellationToken);

// IIdentityDecisionApplicationService
Task<GameIdentityDecision?> GetActiveConfirmedAsync(
    GameId gameId,
    CancellationToken cancellationToken);
Task RevokeConfirmedAsync(
    GameId gameId,
    CancellationToken cancellationToken);
```

`SqliteIdentityDecisionService.GetActiveConfirmedAsync` réutilise la lecture exacte existante de la confirmation active dans `game_identity_decisions`, avec le prédicat `game_id = $game AND decision_type = 1 AND revoked_utc IS NULL`, une connexion `Mode=ReadOnly;Pooling=False`, des paramètres SQL et propagation du `CancellationToken`. Il ne modifie aucune donnée. `IdentityDecisionNotificationOrchestrator.GetActiveConfirmedAsync` délègue uniquement à `IIdentityDecisionService.GetActiveConfirmedAsync`; `IdentityDecisionApplicationService.GetActiveConfirmedAsync` délègue uniquement à l'orchestrateur. Aucune couche UI ne dépend du store ou du service transactionnel.

`IdentityDecisionApplicationService.RevokeConfirmedAsync` délègue uniquement à `IIdentityDecisionNotificationOrchestrator.RevokeConfirmedAsync`. L'orchestrateur appelle `IIdentityDecisionService.RevokeConfirmedAsync(gameId, DateTimeOffset.UtcNow, cancellationToken)`. Le service transactionnel reste l'unique propriétaire de la mutation : il renseigne `revoked_utc`, conserve la ligne historique, remet `games.canonical_content_id` à `NULL`, conserve les `UserRejected`, garantit l'atomicité et retourne `null` si aucune confirmation active n'existe.

### Sémantique Notification exacte

Une révocation réussie ne modifie aucun `NotificationRecord` existant et ne crée aucune notification : l'orchestrateur n'appelle ni `INotificationCenterService.PublishOrRefreshAsync`, ni `ResolveAsync`, ni `MarkReadAsync`, ni `IIdentityNotificationProducer`. La notification historique depuis laquelle l'action est déclenchée reste `Resolved`. Le ViewModel appelle ensuite son `RefreshAsync` existant pour rafraîchir liste et badge, sans mutation Notification supplémentaire. Si un passage automatique futur produit `MatchProbable` ou `Ambiguous`, seul `IIdentityNotificationProducer.PublishForResolutionAsync` peut publier ou réactiver la clé stable correspondante.

Si `IIdentityDecisionService.RevokeConfirmedAsync` échoue, l'exception est propagée et aucune méthode de `INotificationCenterService` n'est appelée par l'orchestrateur. Une `OperationCanceledException` associée au token annulé est propagée de la même façon, sans fausse résolution. Un retour `null` signifie qu'aucune confirmation active n'existait ; l'opération reste idempotente et ne touche pas aux notifications.

### Entrée UI

Sur le filtre `Resolved`, pour une notification `IdentityResolution`/`ActionRequired` dont `SubjectId` est un `GameId` valide, le ViewModel charge `GetActiveConfirmedAsync`. Il expose `Annuler le choix` uniquement si le résultat est un `GameIdentityDecision` actif (`DecisionType == UserConfirmed`, `RevokedUtc == null`) portant exactement ce `GameId`. Le bouton est lié à un `RevokeConfirmedCommand` qui appelle exclusivement `IIdentityDecisionApplicationService.RevokeConfirmedAsync`; il réutilise `IsDecisionActionInProgress` et le verrou `CanExecute` commun de Task 10, propage l'annulation et appelle `RefreshAsync` puis recharge l'état de décision seulement après succès. En cas d'échec, aucun refresh ne doit masquer l'erreur. Après succès, la lecture active retourne `null` et l'action disparaît. Aucun contrôle n'est ajouté pour les notifications non Identity, actives, sans confirmation active ou dont le `SubjectId` est invalide. Aucun nouveau route/page ni code métier dans `NotificationPanel.xaml.cs`.

### TDD RED → GREEN

1. Ajouter d'abord les tests de contrat/délégation dans `IdentityDecisionApplicationServiceTests` : résultat actif/null transmis sans transformation, `GameId` et token exacts, révocation déléguée à l'orchestrateur, cancellation propagée. RED attendu : méthodes absentes sur les interfaces.
2. Ajouter les tests orchestrateur : lecture déléguée au service métier ; révocation réussie appelle une fois le service ; retour `null`, échec métier et cancellation n'appellent aucune méthode de `INotificationCenterService` ; aucune dépendance ou invocation de `IIdentityNotificationProducer`. RED attendu : API orchestrateur absente.
3. Étendre `IdentityDecisionServiceTests` uniquement pour la lecture active et réutiliser les tests existants de révocation afin de prouver `revoked_utc`, canonical `NULL`, conservation des rejets, unicité et atomicité. RED attendu : lecture absente du contrat/service.
4. Créer `IdentityDecisionRevocationTests` : action visible uniquement dans l'historique Identity avec confirmation active correspondante ; action masquée pour null, décision révoquée, rejet, mauvais `GameId`, notification active/non Identity ; commande transmet le `GameId` exact, se verrouille contre une action concurrente, propage la cancellation, rafraîchit seulement après succès et disparaît après succès. Ajouter un contract test XAML vérifiant le libellé `Annuler le choix`, `RevokeConfirmedCommand` et la visibilité dérivée.
5. Exécuter les RED séparément avant toute production :

```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~IdentityDecisionApplicationServiceTests|FullyQualifiedName~IdentityDecisionNotificationOrchestratorTests" --no-restore -m:1
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --filter "FullyQualifiedName~IdentityDecisionServiceTests" --no-restore -m:1
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~IdentityDecisionRevocationTests" --no-restore -m:1
```

6. Implémenter le minimum dans l'ordre service métier/lecture, orchestrateur, façade, puis UI ; relancer chaque filtre jusqu'au GREEN sans modifier les assertions pour contourner un échec.
7. Exécuter les régressions :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~IdentityDecision" --no-restore -m:1
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" --configuration Release --filter "FullyQualifiedName~IdentityDecision" --no-restore -m:1
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~Notification" --no-restore -m:1
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --no-restore -m:1
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

8. Auditer le diff : UI dépend uniquement de `IIdentityDecisionApplicationService`; aucun SQL dans UI/Core ; aucun appel direct UI à `IIdentityDecisionService`, `INotificationCenterService` pour la révocation ou `IIdentityNotificationProducer`; aucun matching, candidat runtime ou réseau ajouté. Terminer par `git diff --check`, `git status --short` et `git diff --stat`.

**Gate/commit prévu après validation :** historique par `revoked_utc`, canonical remis à `NULL` atomiquement, rejets conservés, aucune mutation Notification lors de la révocation, pipeline automatique de nouveau applicable au prochain passage, suites ci-dessus GREEN ; commit `feat(identity): add decision revocation entry point`.

## Task 12 — DI and runtime wiring

**Modify:** `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`, `src/PlayStead.Core/Scanning/LocalIdentityResolutionCoordinator.cs` only where the registrations and deterministic coordinator construction require it. `LocalStartupPipeline.cs` remains unchanged unless a real constructor-registration dependency requires it.
**Test:** `tests/PlayStead.UI.Tests/Bootstrap/IdentityDecisionStartupTests.cs`, with the existing `tests/PlayStead.Core.Tests/Scanning/LocalIdentityResolutionCoordinatorTests.cs` regression suite.

The current coordinator definitions are:

```csharp
public LocalIdentityResolutionCoordinator(
    ILibraryGameLookup libraryGameLookup,
    IGameIdentityResolver identityResolver,
    IIdentityResolutionStore identityResolutionStore,
    ILocalIdentityReconciler identityReconciler);

public LocalIdentityResolutionCoordinator(
    ILibraryGameLookup libraryGameLookup,
    IGameIdentityResolver identityResolver,
    IIdentityResolutionStore identityResolutionStore,
    ILocalIdentityReconciler identityReconciler,
    IIdentityNotificationProducer? notificationProducer);

public LocalIdentityResolutionCoordinator(
    ILibraryGameLookup libraryGameLookup,
    IGameIdentityResolver identityResolver,
    IIdentityResolutionStore identityResolutionStore,
    ILocalIdentityReconciler identityReconciler,
    IIdentityDecisionStore? identityDecisionStore);
```

The private six-parameter constructor is the only complete production shape:

```csharp
LocalIdentityResolutionCoordinator(
    ILibraryGameLookup libraryGameLookup,
    IGameIdentityResolver identityResolver,
    IIdentityResolutionStore identityResolutionStore,
    ILocalIdentityReconciler identityReconciler,
    IIdentityDecisionStore? identityDecisionStore,
    IIdentityNotificationProducer? notificationProducer);
```

Task 12 MUST use the explicit composition-root factory below as the single production DI path. It supplies both `IIdentityDecisionStore` and `IIdentityNotificationProducer` to the complete six-dependency shape; the container MUST NOT select among the partial constructors by convention. The existing public partial constructors remain only for the direct constructor calls already present in `LocalIdentityResolutionCoordinatorTests`. A Core change to `LocalIdentityResolutionCoordinator.cs` is allowed only if required to expose or normalize this exact complete shape; no second coordinator or service locator may be introduced.

```csharp
builder.Services.AddSingleton<ILocalIdentityResolutionCoordinator>(services =>
    new LocalIdentityResolutionCoordinator(
        services.GetRequiredService<ILibraryGameLookup>(),
        services.GetRequiredService<IGameIdentityResolver>(),
        services.GetRequiredService<IIdentityResolutionStore>(),
        services.GetRequiredService<ILocalIdentityReconciler>(),
        services.GetRequiredService<IIdentityDecisionStore>(),
        services.GetRequiredService<IIdentityNotificationProducer>()));
```

Register exactly once, using the existing persistence lifetime:

```csharp
IIdentityDecisionStore -> SqliteIdentityDecisionStore (Singleton)
IIdentityDecisionService -> SqliteIdentityDecisionService (Singleton)
IIdentityDecisionCandidateSource -> EmptyIdentityDecisionCandidateSource (Singleton)
IIdentityDecisionContextProvider -> IdentityDecisionContextProvider (Singleton)
IIdentityDecisionContextGateway -> IdentityDecisionContextGateway (Singleton)
IIdentityDecisionNotificationOrchestrator -> IdentityDecisionNotificationOrchestrator (Singleton)
IIdentityDecisionApplicationService -> IdentityDecisionApplicationService (Singleton)
```

Verify all registrations resolve, the coordinator receives both decision store and automatic notification producer, and the empty source produces zero candidates without network or candidate fabrication. Preserve Phase 2A/2B startup order and cancellation behavior; no identity work is added to the database-only initialization path. Task 11's interface extensions remain consumed by the same concrete singleton instances; no additional revocation registration is needed.

Run targeted RED/GREEN, the coordinator regression suite, Bootstrap/Identity/Notification suites and strict UI build; commit `feat(identity): wire human decision integration`.

## Task 13 — Final Acceptance Gate Phase 2C

**Test/artifact:** `tests/PlayStead.Core.Tests`, `tests/PlayStead.Data.Tests`, `tests/PlayStead.UI.Tests`; no production changes.

Run with `$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1'` and `-m:1`: Core full, Data full, UI full, solution build `/warnaserror`. Verify schema v10, all transactional invariants, decision context contracts/provider, UserRejected filtering and cardinalities, zero-candidate provisional fallback, no matching engine or network dependency, `IIdentityDecisionNotificationOrchestrator`, `IIdentityDecisionApplicationService`, GetContext delegation through the gateway, Confirm/Reject/RevokeConfirmed delegation through the orchestrator, active-confirmation reading through the application service, the UI consuming only the application facade, `IIdentityNotificationProducer` reserved for automatic resolution and never called by revocation, notification failure isolation, decision failure not resolving notifications, Ambiguous → MatchProbable, notification lifecycle, confirm/reject/choose UI in Task 10, revoke/reconsideration UI in Task 11, `revoked_utc` history preservation, canonical reset to `NULL`, automatic-pipeline applicability after the active confirmation is absent, DI, exactly one deterministic coordinator construction path with both producer and decision store, the empty production candidate source, and `RUNTIME_CANDIDATE_GENERATION=NOT_IMPLEMENTED_BY_DESIGN`. Also run `git diff --check` and require a clean worktree. Isolate known SQLite Dispose flakes without weakening assertions.

If and only if there is no functional failure, declare `PHASE2C_IDENTITY_DECISION_INTEGRATION=GREEN`; commit `test(identity): close Phase 2C acceptance` only after the gate is complete.

## Global Out of Scope

No central server, network, live IGDB/SteamGridDB/RAWG, new fuzzy matcher, ML scoring, advanced ranking, cloud sync, multi-user collaboration, event sourcing, physical history deletion, dedicated Identity page, global Notification Center/Library redesign, or new NuGet dependency.
