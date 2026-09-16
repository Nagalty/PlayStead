# PlayStead — Phase 2A Identity Resolution Foundation — Plan d’implémentation

> **Pour les workers agentiques :** utiliser `superpowers:subagent-driven-development` ou `superpowers:executing-plans` et exécuter les tâches dans l’ordre. Chaque tâche suit RED → GREEN → vérification → commit.

**Objectif :** ajouter une résolution d’identité locale et offline-first capable de reconnaître un `ProviderRef` canonique exact, de créer un `PS-TEMP` stable pour les jeux inconnus, puis de réconcilier localement un jeu avec un `CatalogContentId` sans perte de données.

**Architecture :** le scan Library existant reste autoritaire pour créer/mettre à jour `GameId`, `provider_game_refs` et `installations`. Après chaque persistance réussie d’un `SourceScanResult`, un sidecar `LocalIdentityResolutionCoordinator` effectue la résolution d’identité. Le resolver est pur et ne crée jamais de `PS-TEMP`; la persistance locale garantit la stabilité du provisoire. `games.canonical_content_id` reste l’unique vérité d’un rattachement canonique confirmé.

**Stack :** .NET 10, C#, Microsoft.Data.Sqlite, System.Text.Json, xUnit, WPF/DI existant.

**Spec :** `docs/superpowers/specs/2026-09-16-playstead-canonical-game-catalog-design.md`

**Baseline :** Phase 1 Canonical Catalog Foundation fermée, HEAD après `c2dbc30` et les commits Phase 1.

## Contraintes globales

- Workspace : `D:\Dev\PlayStead\worktrees\0.4.1-media-foundation`.
- Branche : `feat/0.4.1-media-foundation`.
- `GameId` reste l’identité locale.
- `CatalogContentId` reste l’identité technique canonique.
- Aucun `PlaySteadPublicId` global n’est créé par le client.
- `PS-TEMP` est local, provisoire et stable.
- Un match exact de catalogue peut devenir `MatchConfirmed` sans jamais créer de `PS-TEMP`.
- Le resolver ne fait aucun fuzzy match, aucune heuristique de titre, aucune déduction par développeur/date.
- `MatchProbable` et `Ambiguous` existent dans le modèle mais ne sont pas produits par le resolver Phase 2A.
- Aucun réseau.
- Aucun serveur OVH.
- Aucun IGDB / SteamGridDB / RAWG.
- Aucun Notification Center.
- Aucun `UserConfirmed` / `UserRejected` UI.
- Aucun Epic/GOG production.
- Aucun changement de comportement des sessions.
- `games.canonical_content_id` reste nullable et non unique.
- `candidate_content_id` n’est jamais unique.
- Les providers Library et Catalog restent deux espaces d’enum distincts ; aucun cast numérique implicite.
- `ProviderKind.Steam` est le seul mapping actif en 2A.
- Les erreurs du sidecar identité ne transforment jamais un scan Library réussi en échec.
- Une annulation demandée via `CancellationToken` est toujours propagée.
- Build final : 0 warning / 0 erreur avec `/warnaserror`.

---

# Cartographie des fichiers

## Core — nouveaux fichiers

- `src/PlayStead.Core/Identity/ProvisionalIdentityId.cs`
- `src/PlayStead.Core/Identity/IdentityResolutionState.cs`
- `src/PlayStead.Core/Identity/IdentityResolutionEvidenceKind.cs`
- `src/PlayStead.Core/Identity/IdentityResolutionEvidence.cs`
- `src/PlayStead.Core/Identity/GameIdentityObservation.cs`
- `src/PlayStead.Core/Identity/IdentityResolutionResult.cs`
- `src/PlayStead.Core/Identity/GameIdentityResolution.cs`
- `src/PlayStead.Core/Identity/ProviderKindMapping.cs`
- `src/PlayStead.Core/Identity/IGameIdentityResolver.cs`
- `src/PlayStead.Core/Identity/GameIdentityResolver.cs`
- `src/PlayStead.Core/Identity/ILocalIdentityReconciler.cs`
- `src/PlayStead.Core/Scanning/ILocalIdentityResolutionCoordinator.cs`
- `src/PlayStead.Core/Scanning/LocalIdentityResolutionCoordinator.cs`
- `src/PlayStead.Core/Persistence/IIdentityResolutionStore.cs`
- `src/PlayStead.Core/Persistence/ILibraryGameLookup.cs`

## Data — nouveaux fichiers

- `src/PlayStead.Data/Database/Migrations/008_identity_resolution.sql`
- `src/PlayStead.Data/Identity/SqliteIdentityResolutionStore.cs`
- `src/PlayStead.Data/Identity/SqliteLocalIdentityReconciler.cs`
- `src/PlayStead.Data/Library/SqliteLibraryGameLookup.cs`

## Data — fichiers modifiés

- `src/PlayStead.Data/Database/DatabaseInitializer.cs`
- tests de migration qui expriment la version courante du schéma.

## UI — fichiers modifiés

- `src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs`
- `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`

## Tests — nouveaux fichiers

- `tests/PlayStead.Core.Tests/Identity/IdentityResolutionModelTests.cs`
- `tests/PlayStead.Core.Tests/Identity/GameIdentityResolverTests.cs`
- `tests/PlayStead.Core.Tests/Scanning/LocalIdentityResolutionCoordinatorTests.cs`
- `tests/PlayStead.Data.Tests/Identity/IdentityResolutionDatabaseTests.cs`
- `tests/PlayStead.Data.Tests/Identity/SqliteIdentityResolutionStoreTests.cs`
- `tests/PlayStead.Data.Tests/Identity/LocalIdentityReconcilerTests.cs`
- `tests/PlayStead.Data.Tests/Library/SqliteLibraryGameLookupTests.cs`
- `tests/PlayStead.UI.Tests/Bootstrap/IdentityResolutionStartupTests.cs`

---

# Task 1 — Contrats Core : états, preuves et PS-TEMP

**Files**
- Create: `src/PlayStead.Core/Identity/ProvisionalIdentityId.cs`
- Create: `src/PlayStead.Core/Identity/IdentityResolutionState.cs`
- Create: `src/PlayStead.Core/Identity/IdentityResolutionEvidenceKind.cs`
- Create: `src/PlayStead.Core/Identity/IdentityResolutionEvidence.cs`
- Create: `src/PlayStead.Core/Identity/GameIdentityObservation.cs`
- Create: `src/PlayStead.Core/Identity/IdentityResolutionResult.cs`
- Create: `src/PlayStead.Core/Identity/GameIdentityResolution.cs`
- Test: `tests/PlayStead.Core.Tests/Identity/IdentityResolutionModelTests.cs`

**Consumes**
- `GameId`
- `CatalogContentId`
- `CatalogProviderKind`

**Produces**
- Modèle de résolution persistant.
- `PS-TEMP-<ULID>` local.
- Aucun accès SQLite.

## Interfaces exactes

```csharp
namespace PlayStead.Core.Identity;

public enum IdentityResolutionState
{
    MatchConfirmed = 1,
    MatchProbable = 2,
    Ambiguous = 3,
    New = 4
}

public enum IdentityResolutionEvidenceKind
{
    ExactProviderRef = 1,
    NoExactProviderRefMatch = 2
}
```

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Identity;

public sealed record IdentityResolutionEvidence(
    IdentityResolutionEvidenceKind Kind,
    CatalogProviderKind? Provider,
    string ExternalId,
    CatalogContentId? MatchedContentId);
```

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Identity;

public sealed record GameIdentityObservation(
    CatalogProviderKind Provider,
    string ExternalId,
    string Title,
    DateTimeOffset ObservedAtUtc);
```

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Identity;

public sealed record IdentityResolutionResult(
    IdentityResolutionState State,
    CatalogContentId? CandidateContentId,
    IdentityResolutionEvidence Evidence);
```

```csharp
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed record GameIdentityResolution(
    GameId GameId,
    ProvisionalIdentityId? ProvisionalIdentityId,
    IdentityResolutionState State,
    CatalogContentId? CandidateContentId,
    IdentityResolutionEvidence Evidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
```

## Format `PS-TEMP`

`ProvisionalIdentityId` encapsule une chaîne stricte :

```text
PS-TEMP-<26 caractères ULID Crockford>
```

Alphabet ULID :

```text
0123456789ABCDEFGHJKMNPQRSTVWXYZ
```

Règles :
- exactement 26 caractères après `PS-TEMP-`;
- premier caractère ULID entre `0` et `7`;
- lettres I, L, O, U interdites;
- casse canonique uppercase;
- 48 bits timestamp UTC millisecondes;
- 80 bits aléatoires via `RandomNumberGenerator.Fill`;
- aucune dépendance NuGet.

API :

```csharp
public readonly record struct ProvisionalIdentityId
{
    public string Value { get; }

    public static ProvisionalIdentityId New();
    public static ProvisionalIdentityId New(DateTimeOffset timestampUtc);
    public static ProvisionalIdentityId Parse(string value);
    public static bool TryParse(
        string? value,
        out ProvisionalIdentityId result);

    public override string ToString();
}
```

`New(DateTimeOffset)` existe pour tester le préfixe temporel sans rendre l’aléatoire déterministe.

## RED

- [ ] Créer `IdentityResolutionModelTests.cs`.

Tests obligatoires :

```csharp
[Fact]
public void Provisional_id_round_trips()
{
    var id = ProvisionalIdentityId.New(
        new DateTimeOffset(
            2026, 9, 16, 18, 0, 0, TimeSpan.Zero));

    Assert.StartsWith("PS-TEMP-", id.Value);
    Assert.Equal(34, id.Value.Length);
    Assert.Equal(id, ProvisionalIdentityId.Parse(id.Value));
}

[Fact]
public void Two_generated_ids_are_distinct()
{
    var first = ProvisionalIdentityId.New();
    var second = ProvisionalIdentityId.New();

    Assert.NotEqual(first, second);
}

[Theory]
[InlineData("")]
[InlineData("PS-TEMP-")]
[InlineData("PS-TEMP-0000000000000000000000000")]
[InlineData("PS-TEMP-80000000000000000000000000")]
[InlineData("PS-TEMP-0000000000000000000000000I")]
[InlineData("PLAYSTEAD-TEMP-00000000000000000000000000")]
public void Invalid_provisional_ids_are_rejected(string value)
{
    Assert.False(
        ProvisionalIdentityId.TryParse(value, out _));
}

[Fact]
public void Resolution_state_values_are_frozen_for_sqlite()
{
    Assert.Equal(1, (int)IdentityResolutionState.MatchConfirmed);
    Assert.Equal(2, (int)IdentityResolutionState.MatchProbable);
    Assert.Equal(3, (int)IdentityResolutionState.Ambiguous);
    Assert.Equal(4, (int)IdentityResolutionState.New);
}

[Fact]
public void Evidence_json_uses_numeric_enum_values()
{
    var evidence = new IdentityResolutionEvidence(
        IdentityResolutionEvidenceKind.ExactProviderRef,
        CatalogProviderKind.Steam,
        "2479810",
        new CatalogContentId(
            Guid.Parse(
                "11111111-1111-1111-1111-111111111111")));

    var json = JsonSerializer.Serialize(evidence);

    Assert.Contains("\"Kind\":1", json);
    Assert.Contains("\"Provider\":1", json);
    Assert.Contains("\"ExternalId\":\"2479810\"", json);
}
```

- [ ] Lancer RED :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~IdentityResolutionModelTests"
```

Attendu : échec de compilation sur les types absents.

## GREEN

- [ ] Implémenter les enums/records exacts ci-dessus.
- [ ] Implémenter `ProvisionalIdentityId`.

Encodage ULID :
1. allouer 16 octets;
2. écrire le timestamp Unix millisecondes sur les 6 premiers octets en big-endian;
3. `RandomNumberGenerator.Fill(bytes[6..])`;
4. encoder 128 bits en 26 symboles Crockford avec **deux bits zéro en tête**;
5. préfixer `PS-TEMP-`.

Validation :
- vérifier longueur;
- vérifier préfixe ordinal;
- vérifier 26 caractères dans l’alphabet;
- vérifier que le premier symbole ULID a une valeur <= 7.

- [ ] Relancer :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~IdentityResolutionModelTests"
```

Attendu : PASS.

- [ ] Build :

```powershell
dotnet build ".\src\PlayStead.Core\PlayStead.Core.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] Commit :

```powershell
git add `
  "src/PlayStead.Core/Identity" `
  "tests/PlayStead.Core.Tests/Identity/IdentityResolutionModelTests.cs"

git commit -m "feat(identity): add local resolution contracts"
```

---

# Task 2 — Mapping provider et resolver déterministe

**Files**
- Create: `src/PlayStead.Core/Identity/ProviderKindMapping.cs`
- Create: `src/PlayStead.Core/Identity/IGameIdentityResolver.cs`
- Create: `src/PlayStead.Core/Identity/GameIdentityResolver.cs`
- Test: `tests/PlayStead.Core.Tests/Identity/GameIdentityResolverTests.cs`

**Consumes**
- `ICanonicalCatalogStore`
- `CatalogProviderKind`
- `GameIdentityObservation`

**Produces**
- Exact provider match → `MatchConfirmed`.
- Unknown provider ref → `New`.
- Aucun PS-TEMP.

## Mapping exact

```csharp
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public static class ProviderKindMapping
{
    public static bool TryMap(
        ProviderKind provider,
        out CatalogProviderKind catalogProvider)
    {
        if (provider == ProviderKind.Steam)
        {
            catalogProvider = CatalogProviderKind.Steam;
            return true;
        }

        catalogProvider = default;
        return false;
    }
}
```

Aucun cast `(CatalogProviderKind)(int)provider`.

## Resolver

```csharp
namespace PlayStead.Core.Identity;

public interface IGameIdentityResolver
{
    Task<IdentityResolutionResult> ResolveAsync(
        GameIdentityObservation observation,
        CancellationToken cancellationToken);
}
```

Constructeur :

```csharp
public GameIdentityResolver(
    ICanonicalCatalogStore catalogStore)
```

Algorithme unique Phase 2A :

```text
FindByProviderRefAsync(observation.Provider, observation.ExternalId)
    trouvé
        -> MatchConfirmed
        -> CandidateContentId = content.Id
        -> Evidence ExactProviderRef
    null
        -> New
        -> CandidateContentId = null
        -> Evidence NoExactProviderRefMatch
```

Le `Title` n’est jamais utilisé pour confirmer.

## RED

- [ ] Créer `GameIdentityResolverTests.cs`.

Tests obligatoires :

```csharp
[Fact]
public async Task Exact_Steam_provider_ref_returns_match_confirmed()
```

```csharp
[Fact]
public async Task Unknown_Steam_provider_ref_returns_new()
```

```csharp
[Fact]
public async Task Same_title_without_provider_ref_never_confirms()
```

```csharp
[Theory]
[InlineData(ProviderKind.Epic)]
[InlineData(ProviderKind.Gog)]
[InlineData(ProviderKind.Manual)]
public void Unsupported_library_provider_is_not_mapped(
    ProviderKind provider)
{
    Assert.False(
        ProviderKindMapping.TryMap(
            provider,
            out _));
}
```

Le fake `ICanonicalCatalogStore` ne répond que par lookup exact.

- [ ] Lancer RED :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~GameIdentityResolverTests"
```

## GREEN

- [ ] Implémenter mapping + resolver.
- [ ] Propager `CancellationToken` au store.
- [ ] Ne jamais créer de `ProvisionalIdentityId`.
- [ ] Ne jamais retourner `MatchProbable` ou `Ambiguous`.

- [ ] GREEN :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Identity"
```

- [ ] Build/diff :

```powershell
dotnet build ".\src\PlayStead.Core\PlayStead.Core.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] Commit :

```powershell
git add `
  "src/PlayStead.Core/Identity" `
  "tests/PlayStead.Core.Tests/Identity"

git commit -m "feat(identity): add deterministic local resolver"
```

---

# Task 3 — Migration SQLite v8

**Files**
- Create: `src/PlayStead.Data/Database/Migrations/008_identity_resolution.sql`
- Modify: `src/PlayStead.Data/Database/DatabaseInitializer.cs`
- Test: `tests/PlayStead.Data.Tests/Identity/IdentityResolutionDatabaseTests.cs`
- Modify only current-schema expectations in:
  - `tests/PlayStead.Data.Tests/Database/DatabaseInitializerTests.cs`
  - `tests/PlayStead.Data.Tests/Database/DatabaseProcessSignatureDiscoveryMigrationTests.cs`
  - `tests/PlayStead.Data.Tests/Database/DatabaseSessionCorrectionMigrationTests.cs`
  - `tests/PlayStead.Data.Tests/Database/DatabaseSessionMigrationTests.cs`
  - `tests/PlayStead.Data.Tests/Database/DatabaseSteamEvidenceMigrationTests.cs`
  - `tests/PlayStead.Data.Tests/Database/DatabaseCanonicalCatalogLinkMigrationTests.cs`
  - `tests/PlayStead.Data.Tests/Database/Task08Fix01MigrationSafetyTests.cs`
  - `tests/PlayStead.Data.Tests/Database/DiscoveryDatabaseFixture.cs` si son helper encode explicitement la version latest.

**Consumes**
- schéma `playstead.db` v7.
- valeurs numériques de `IdentityResolutionState`.

**Produces**
- schéma v8.
- table `game_identity_resolutions`.

## SQL exact

```sql
CREATE TABLE game_identity_resolutions (
    game_id TEXT NOT NULL PRIMARY KEY,
    provisional_id TEXT NULL,
    state INTEGER NOT NULL
        CHECK (state IN (1, 2, 3, 4)),
    candidate_content_id TEXT NULL,
    evidence_json TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,

    FOREIGN KEY (game_id)
        REFERENCES games(game_id)
        ON DELETE CASCADE,

    CHECK (
        state <> 4
        OR candidate_content_id IS NULL
    ),

    CHECK (
        state <> 1
        OR candidate_content_id IS NOT NULL
    )
);

CREATE UNIQUE INDEX
    ux_game_identity_resolutions_provisional
ON game_identity_resolutions(provisional_id)
WHERE provisional_id IS NOT NULL;

CREATE INDEX
    ix_game_identity_resolutions_candidate
ON game_identity_resolutions(candidate_content_id);
```

Il n’existe :
- aucune unicité sur `candidate_content_id`;
- aucune unicité sur `games.canonical_content_id`;
- aucune FK cross-database vers `catalog.db`.

`DatabaseInitializer` :
- `TargetVersion = 8`;
- `[8] = "008_identity_resolution.sql"`.

## RED

- [ ] Créer `IdentityResolutionDatabaseTests.cs`.

Tests :
1. fresh DB finit en v8 et possède la table;
2. v7 → v8 conserve les jeux;
3. `provisional_id` accepte `NULL`;
4. deux lignes `NULL` sont autorisées;
5. deux lignes avec le même provisional non-null échouent;
6. deux `game_id` différents peuvent utiliser le même `candidate_content_id`;
7. deux jeux différents peuvent déjà avoir le même `games.canonical_content_id`;
8. `state=4` avec candidate non-null échoue;
9. `state=1` avec candidate null échoue.

- [ ] RED avant migration :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~IdentityResolutionDatabaseTests"
```

Attendu : version/table absente.

## GREEN

- [ ] Ajouter SQL et version 8.
- [ ] Mettre à jour uniquement les assertions de **version courante/latest** vers 8.
- [ ] Ne pas modifier les fixtures représentant volontairement v6 ou v7 en entrée.
- [ ] `DatabaseCanonicalCatalogLinkMigrationTests` peut partir de v6/v7 mais doit attendre la version finale 8 après `InitializeAsync`.

- [ ] Tests ciblés :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~IdentityResolutionDatabaseTests|FullyQualifiedName~Database"
```

- [ ] Data full :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --no-restore
```

Les flakes SQLite de cleanup déjà documentés doivent être vérifiés isolément, jamais masqués.

- [ ] Build/diff :

```powershell
dotnet build ".\src\PlayStead.Data\PlayStead.Data.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] Commit :

```powershell
git add `
  "src/PlayStead.Data/Database" `
  "tests/PlayStead.Data.Tests/Database" `
  "tests/PlayStead.Data.Tests/Identity/IdentityResolutionDatabaseTests.cs"

git commit -m "feat(identity): persist local resolution schema"
```

---

# Task 4 — Store local et PS-TEMP stable

**Files**
- Create: `src/PlayStead.Core/Persistence/IIdentityResolutionStore.cs`
- Create: `src/PlayStead.Data/Identity/SqliteIdentityResolutionStore.cs`
- Test: `tests/PlayStead.Data.Tests/Identity/SqliteIdentityResolutionStoreTests.cs`

**Consumes**
- `GameIdentityResolution`
- migration v8
- `DatabaseOptions`

**Produces**
- persistance/readback.
- `GetOrCreateProvisionalAsync`.

## Interface exacte

```csharp
using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public interface IIdentityResolutionStore
{
    Task<GameIdentityResolution?> GetAsync(
        GameId gameId,
        CancellationToken cancellationToken);

    Task<GameIdentityResolution> GetOrCreateProvisionalAsync(
        GameId gameId,
        IdentityResolutionEvidence evidence,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        GameIdentityResolution resolution,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameIdentityResolution>> ListAsync(
        CancellationToken cancellationToken);
}
```

## Sérialisation

```csharp
JsonSerializer.Serialize(evidence)
JsonSerializer.Deserialize<IdentityResolutionEvidence>(json)
```

Options par défaut; les enums restent numériques.

## `GetOrCreateProvisionalAsync`

Algorithme exact :

1. ouvrir connexion `Pooling=False`;
2. `SELECT` par `game_id`;
3. si ligne existante avec `provisional_id` non-null → retourner cette ligne;
4. générer un nouveau `ProvisionalIdentityId`;
5. `INSERT OR IGNORE` une ligne :
   - game_id
   - provisional_id
   - state = 4 (`New`)
   - candidate_content_id = NULL
   - evidence_json
   - created_utc = observedAtUtc
   - updated_utc = observedAtUtc
6. relire par `game_id`;
7. retourner la ligne gagnante;
8. si la ligne gagnante est `MatchConfirmed` avec provisional null, la retourner sans fabriquer de provisoire.

La contrainte PK sur `game_id` rend le `INSERT OR IGNORE` idempotent. L’index unique partiel protège l’unicité du provisional.

## `UpsertAsync`

SQL :

```sql
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
```

Ainsi un `PS-TEMP` historique n’est jamais effacé par un upsert ultérieur.

## RED

- [ ] Tests obligatoires :

```text
GetAsync absent → null
premier GetOrCreate → New + PS-TEMP
même GameId deuxième appel → même PS-TEMP
nouvelle instance du store / reload → même PS-TEMP
deux GameId → PS-TEMP différents
Upsert MatchConfirmed conserve ancien PS-TEMP
Upsert MatchConfirmed direct autorise provisional null
ListAsync round-trip
cancellation demandée → OperationCanceledException
```

- [ ] RED :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~SqliteIdentityResolutionStoreTests"
```

## GREEN

- [ ] Implémenter le store.
- [ ] Lecture seule pour `GetAsync`/`ListAsync` :
  `Mode=ReadOnly;Pooling=False`.
- [ ] Écriture : `Pooling=False`.
- [ ] Parser les timestamps avec round-trip UTC.

- [ ] Tests ciblés puis Data Identity :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Identity"
```

- [ ] Build/diff.

- [ ] Commit :

```powershell
git add `
  "src/PlayStead.Core/Persistence/IIdentityResolutionStore.cs" `
  "src/PlayStead.Data/Identity/SqliteIdentityResolutionStore.cs" `
  "tests/PlayStead.Data.Tests/Identity/SqliteIdentityResolutionStoreTests.cs"

git commit -m "feat(identity): add provisional identity store"
```

---

# Task 5 — Réconciliation transactionnelle locale

**Files**
- Create: `src/PlayStead.Core/Identity/ILocalIdentityReconciler.cs`
- Create: `src/PlayStead.Data/Identity/SqliteLocalIdentityReconciler.cs`
- Test: `tests/PlayStead.Data.Tests/Identity/LocalIdentityReconcilerTests.cs`

**Consumes**
- `DatabaseOptions`
- `GameId`
- `CatalogContentId`
- `IdentityResolutionEvidence`

**Produces**
- rattachement canonique confirmé.
- conservation du PS-TEMP historique.

## Interface exacte

```csharp
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface ILocalIdentityReconciler
{
    Task ReconcileAsync(
        GameId localGameId,
        CatalogContentId canonicalContentId,
        IdentityResolutionEvidence evidence,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);
}
```

## Transaction exacte

Dans une unique transaction SQLite :

```sql
UPDATE games
SET canonical_content_id = $canonicalContentId
WHERE game_id = $gameId;
```

Vérifier `ExecuteNonQuery == 1`.

Puis :

```sql
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
    NULL,
    1,
    $canonicalContentId,
    $evidenceJson,
    $observedAtUtc,
    $observedAtUtc)
ON CONFLICT(game_id) DO UPDATE SET
    provisional_id =
        game_identity_resolutions.provisional_id,
    state = 1,
    candidate_content_id = excluded.candidate_content_id,
    evidence_json = excluded.evidence_json,
    updated_utc = excluded.updated_utc;
```

`created_utc` existant reste intact via `ON CONFLICT`.

Ne modifier aucune ligne `installations`, `game_sessions`, corrections, signatures ou médias.

## RED

- [ ] Fixture avec :
  - 1 game;
  - 1 provider ref;
  - 1 installation;
  - 1 session;
  - variante avec PS-TEMP existant;
  - variante sans PS-TEMP.

Tests :
- match direct → canonical renseigné et provisional null;
- ancien PS-TEMP → conservé;
- GameId identique avant/après;
- installation_id identique;
- session_id identique;
- deux GameId peuvent être réconciliés vers le même `CatalogContentId`;
- deuxième `ReconcileAsync` identique → état identique;
- cancellation avant mutation → aucune mutation;
- game inexistant → exception et rollback.

- [ ] RED :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~LocalIdentityReconcilerTests"
```

## GREEN

- [ ] Implémenter exactement la transaction.
- [ ] Propager cancellation.
- [ ] `Pooling=False`.

- [ ] Tests ciblés + build/diff.

- [ ] Commit :

```powershell
git add `
  "src/PlayStead.Core/Identity/ILocalIdentityReconciler.cs" `
  "src/PlayStead.Data/Identity/SqliteLocalIdentityReconciler.cs" `
  "tests/PlayStead.Data.Tests/Identity/LocalIdentityReconcilerTests.cs"

git commit -m "feat(identity): reconcile canonical identity locally"
```

---

# Task 6 — Sidecar post-scan réellement branché

**Important :** dans l’architecture actuelle, `ILibraryStore.ApplySourceScanAsync(...)` est appelé dans `LocalStartupPipeline.RefreshAsync`, dans la boucle sur les `SourceScanResult`. Le branchement Phase 2A doit donc être ajouté dans **`RefreshAsync`**, immédiatement après la persistance réussie du résultat. Il ne doit pas être ajouté à `InitializeAsync`. fileciteturn25file0

**Files**
- Create: `src/PlayStead.Core/Persistence/ILibraryGameLookup.cs`
- Create: `src/PlayStead.Data/Library/SqliteLibraryGameLookup.cs`
- Create: `src/PlayStead.Core/Scanning/ILocalIdentityResolutionCoordinator.cs`
- Create: `src/PlayStead.Core/Scanning/LocalIdentityResolutionCoordinator.cs`
- Modify: `src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs`
- Modify: `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`
- Test: `tests/PlayStead.Data.Tests/Library/SqliteLibraryGameLookupTests.cs`
- Test: `tests/PlayStead.Core.Tests/Scanning/LocalIdentityResolutionCoordinatorTests.cs`
- Test: `tests/PlayStead.UI.Tests/Bootstrap/IdentityResolutionStartupTests.cs`

## Library lookup exact

```csharp
using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public interface ILibraryGameLookup
{
    Task<GameId?> FindGameIdByProviderRefAsync(
        ProviderKind provider,
        string externalId,
        CancellationToken cancellationToken);
}
```

Implémentation SQL :

```sql
SELECT game_id
FROM provider_game_refs
WHERE provider = $provider
  AND external_id = $externalId
LIMIT 1;
```

Connexion lecture seule :

```text
Mode=ReadOnly;Pooling=False
```

Cette table est la bonne source car `(provider, external_id)` y est déjà la clé canonique locale du scan. fileciteturn28file0

## Coordinateur exact

```csharp
namespace PlayStead.Core.Scanning;

public interface ILocalIdentityResolutionCoordinator
{
    Task ResolveAfterScanAsync(
        SourceScanResult result,
        CancellationToken cancellationToken);
}
```

Dépendances :

```csharp
ILibraryGameLookup
IGameIdentityResolver
IIdentityResolutionStore
ILocalIdentityReconciler
```

Algorithme par installation observée :

```text
1. ThrowIfCancellationRequested.
2. ProviderKindMapping.TryMap(installation.Provider, out catalogProvider).
   false -> continue.
3. FindGameIdByProviderRefAsync(installation.Provider, installation.ExternalId).
   null -> continue conservativement.
4. Construire GameIdentityObservation(
      catalogProvider,
      installation.ExternalId,
      installation.Title,
      installation.ObservedAtUtc).
5. resolver.ResolveAsync.
6. switch:
   MatchConfirmed:
       require CandidateContentId non-null;
       ReconcileAsync(
           gameId,
           candidate,
           evidence,
           observedAtUtc,
           token).

   New:
       GetOrCreateProvisionalAsync(
           gameId,
           evidence,
           observedAtUtc,
           token).

   MatchProbable:
       GetOrCreateProvisionalAsync(...)
       puis UpsertAsync avec le même provisional,
       State=MatchProbable,
       CandidateContentId=result.CandidateContentId.

   Ambiguous:
       GetOrCreateProvisionalAsync(...)
       puis UpsertAsync avec le même provisional,
       State=Ambiguous,
       CandidateContentId=null.
```

Le resolver 2A ne produit que `MatchConfirmed` et `New`; les deux autres branches préparent le modèle pour la suite sans heuristique.

## Branchement exact dans `LocalStartupPipeline`

Dans `RefreshAsync` :

```csharp
foreach (var result in results)
{
    cancellationToken.ThrowIfCancellationRequested();

    await _libraryStore.ApplySourceScanAsync(
        result,
        cancellationToken);

    if (_identityResolutionCoordinator is not null)
    {
        try
        {
            await _identityResolutionCoordinator.ResolveAfterScanAsync(
                result,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Phase 2A: identity sidecar is best-effort.
            // A successful Library scan remains successful.
        }
    }
}
```

Aucune exception non-cancellation du sidecar ne remonte au scan.

### Construction DI

`PlaySteadHost` enregistre :

```text
IGameIdentityResolver -> GameIdentityResolver
IIdentityResolutionStore -> SqliteIdentityResolutionStore
ILocalIdentityReconciler -> SqliteLocalIdentityReconciler
ILibraryGameLookup -> SqliteLibraryGameLookup
ILocalIdentityResolutionCoordinator -> LocalIdentityResolutionCoordinator
```

`LocalStartupPipeline` :
- conserve tous ses constructeurs actuels;
- ajoute le champ nullable `_identityResolutionCoordinator`;
- le constructeur production le plus riche reçoit `ILocalIdentityResolutionCoordinator` en dernier argument;
- les anciens tests utilisant les constructeurs plus courts continuent de fonctionner sans sidecar.

## RED

### Lookup

- [ ] `SqliteLibraryGameLookupTests` :
  - provider ref existante → GameId;
  - inconnue → null;
  - Steam ref après scan → GameId exact.

### Coordinateur Core

Fakes réels des interfaces.

Tests :
- exact provider match → Reconcile appelé 1 fois;
- `New` → `GetOrCreateProvisionalAsync` 1 fois;
- provider non mappé → resolver/store jamais appelés;
- GameId local introuvable → skip;
- cancellation propagée.

### Pipeline UI

Tests :
- après `ApplySourceScanAsync`, sidecar est appelé;
- sidecar qui throw `IOException` → `RefreshAsync` retourne quand même le snapshot Library;
- sidecar qui throw `OperationCanceledException` avec token annulé → cancellation propagée;
- ordre observé : persist Library avant sidecar.

- [ ] RED :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~LocalIdentityResolutionCoordinatorTests"

dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~SqliteLibraryGameLookupTests"

dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~IdentityResolutionStartupTests"
```

## GREEN

- [ ] Implémenter lookup, coordinateur, DI et wiring exact.
- [ ] Relancer les 3 suites ciblées.
- [ ] Régressions bootstrap/library :

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Bootstrap|FullyQualifiedName~HomeMediaIntegrationTests"

dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~SqliteLibraryStoreTests|FullyQualifiedName~Identity"
```

- [ ] Build solution + diff check.

- [ ] Commit :

```powershell
git add `
  "src/PlayStead.Core/Persistence/ILibraryGameLookup.cs" `
  "src/PlayStead.Core/Scanning" `
  "src/PlayStead.Data/Library/SqliteLibraryGameLookup.cs" `
  "src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs" `
  "src/PlayStead.UI/Bootstrap/PlaySteadHost.cs" `
  "tests/PlayStead.Core.Tests/Scanning/LocalIdentityResolutionCoordinatorTests.cs" `
  "tests/PlayStead.Data.Tests/Library/SqliteLibraryGameLookupTests.cs" `
  "tests/PlayStead.UI.Tests/Bootstrap/IdentityResolutionStartupTests.cs"

git commit -m "feat(identity): resolve identities after local scans"
```

---

# Task 7 — Gate final Phase 2A

**Aucun fichier production prévu.**

## Gate ciblé

- [ ] Core Identity :

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Identity|FullyQualifiedName~LocalIdentityResolutionCoordinatorTests"
```

- [ ] Data Identity :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Identity|FullyQualifiedName~SqliteLibraryGameLookupTests"
```

- [ ] UI wiring :

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~IdentityResolutionStartupTests|FullyQualifiedName~Bootstrap"
```

## Matrice de preuves obligatoire

- [ ] `PS-TEMP` format strict.
- [ ] parsing valide/invalide.
- [ ] deux générations distinctes.
- [ ] même GameId rescanné → même PS-TEMP.
- [ ] reload du store → même PS-TEMP.
- [ ] deux GameId → deux PS-TEMP.
- [ ] Steam exact connu → `MatchConfirmed`.
- [ ] Steam inconnu → `New`.
- [ ] titre identique sans exact ref → jamais `MatchConfirmed`.
- [ ] provider Library non mappé → sidecar ignore.
- [ ] match direct connu → aucun PS-TEMP créé.
- [ ] match confirmé après ancien PS-TEMP → temp conservé.
- [ ] deux GameId locaux peuvent partager le même `CatalogContentId`.
- [ ] réconciliation conserve GameId.
- [ ] réconciliation conserve installations.
- [ ] réconciliation conserve sessions.
- [ ] réconciliation idempotente.
- [ ] erreur sidecar identité → scan Library reste réussi.
- [ ] cancellation demandée → propagée.

## Suite complète

- [ ] Data full :

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --no-restore
```

- [ ] Solution full :

```powershell
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$Report = "D:\Dev\PlayStead\02_RAPPORTS\PLAYSTEAD_PHASE2A_FINAL_$Stamp.txt"

dotnet test ".\PlayStead.sln" `
  --configuration Release `
  --no-restore `
  *> $Report

Write-Host "FULL_TESTS=$(if ($LASTEXITCODE -eq 0) {'PASS'} else {'FAIL'})"
Write-Host "REPORT=$Report"
```

Les flakes SQLite cleanup préexistants suivent la même règle que Phase 1 : si l’échec est uniquement au `Dispose()` et que le test passe isolément, le documenter sans affaiblir la logique.

## Build strict

- [ ] :

```powershell
dotnet build ".\PlayStead.sln" `
  --configuration Release `
  --no-restore `
  /warnaserror
```

Attendu : 0 warning / 0 erreur.

## Scope guard Phase 2B/2C

- [ ] Vérifier absence de :
  - Notification Center;
  - cloche;
  - UserConfirmed/UserRejected UI;
  - HTTP catalogue;
  - CatalogServiceOptions;
  - IGDB;
  - SteamGridDB;
  - RAWG;
  - Epic/GOG production.

```powershell
git grep -n -E `
  "NotificationCenter|UserConfirmed|UserRejected|CatalogServiceOptions|IGDB|SteamGridDB|RAWG|HttpClient" `
  -- "src" 2>$null
```

Analyser toute correspondance existante antérieure; aucune nouvelle activation 2B/2C n’est permise.

## Final

- [ ] :

```powershell
git diff --check
git status --short
```

Le worktree doit être propre après les commits.

La Phase 2A peut alors être déclarée :

```text
PHASE2A_IDENTITY_RESOLUTION_FOUNDATION=GREEN
```

---

# Self-review du plan

## Types définis

Tous les types consommés sont définis dans une tâche antérieure ou existent en Phase 1 :
- `GameId`
- `ProviderKind`
- `CatalogProviderKind`
- `CatalogContentId`
- `ICanonicalCatalogStore`
- `SourceScanResult`
- `DiscoveredInstallation`
- modèles sessions/installations existants.

## Nullabilité cohérente

- `GameIdentityResolution.ProvisionalIdentityId` est nullable.
- `game_identity_resolutions.provisional_id` est nullable.
- un match direct confirmé n’en crée pas.
- un PS-TEMP existant est conservé lors d’une confirmation.
- `candidate_content_id` est nullable et non unique.
- `MatchConfirmed` exige un candidat.
- `New` interdit un candidat.

## Branchement runtime

Le caller est explicitement :
`LocalStartupPipeline.RefreshAsync`.

Le branchement est explicitement placé après :
`ILibraryStore.ApplySourceScanAsync`.

Le code actuel effectue bien la persistance des scans dans `RefreshAsync`, pas dans `InitializeAsync`. fileciteturn25file0

## Séparation des responsabilités

- resolver : lookup canonique exact uniquement;
- store identity : persistance locale/PS-TEMP;
- reconciler : rattachement confirmé transactionnel;
- library lookup : provider ref locale → GameId;
- coordinator : orchestration par scan;
- pipeline UI : branchement + isolation d’erreur.

## Hors périmètre

Aucun :
- serveur;
- réseau;
- IGDB/SteamGridDB/RAWG;
- Notification Center;
- cloche;
- UserConfirmed/UserRejected UI;
- fuzzy matching;
- Epic/GOG production;
- Phase 2B/2C.

## Critère de fin

Phase 2A est terminée uniquement quand un scan Steam réel ou simulé peut :

```text
ProviderRef connue dans catalog.db
→ MatchConfirmed
→ games.canonical_content_id renseigné
→ aucun PS-TEMP si aucun n’existait

ProviderRef inconnue
→ New
→ PS-TEMP stable
→ même PS-TEMP après rescan et redémarrage
```

sans changer le résultat métier du scan Library en cas de panne du sidecar identité.
