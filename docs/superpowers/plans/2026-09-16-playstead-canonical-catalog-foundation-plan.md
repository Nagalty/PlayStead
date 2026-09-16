# PlayStead — Fondation du catalogue canonique — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduire la fondation locale du catalogue canonique PlayStead : contrats Core, lien canonique nullable dans `playstead.db`, nouveau `catalog.db` versionné et store SQLite de lecture, sans modifier le comportement actuel de la bibliothèque et sans dépendance réseau.

**Architecture:** La Phase 1 conserve `GameId` comme identité locale utilisée par la bibliothèque, les sessions et le lancement. Elle ajoute une identité canonique distincte (`CatalogContentId` + `PlaySteadPublicId`), un lien nullable depuis `LogicalGame`, et une base `catalog.db` séparée, initialisée localement et consommée en lecture seule. Aucun resolver, `PS-TEMP`, serveur, notification, IGDB, SteamGridDB, RAWG, Epic ou GOG production n’est activé dans cette phase.

**Tech Stack:** .NET 10, C#, WPF, Microsoft.Extensions.DependencyInjection, Microsoft.Data.Sqlite, SQLite, xUnit, PowerShell 7.

**Spec:** `docs/superpowers/specs/2026-09-16-playstead-canonical-game-catalog-design.md`

## Global Constraints

- Workspace autoritaire : `D:\Dev\PlayStead\worktrees\0.4.1-media-foundation`.
- Branche : `feat/0.4.1-media-foundation`.
- Baseline documentaire : commit `fec7963` — architecture du catalogue canonique validée.
- `GameId` reste l’identité locale existante ; il ne devient pas un identifiant mondial.
- Aucun `PlaySteadGameId` définitif n’est généré par le client en Phase 1.
- Aucun `PS-TEMP` n’est introduit en Phase 1.
- Aucun accès réseau, endpoint, serveur OVH ou synchronisation catalogue n’est introduit en Phase 1.
- Aucun Notification Center n’est introduit en Phase 1.
- Aucun resolver de correspondance cross-store n’est activé en Phase 1.
- Le scan Steam actuel continue à retrouver/créer les jeux par `(ProviderKind, ExternalId)` comme avant.
- `catalog.db` est séparé de `playstead.db` et ne contient aucune donnée utilisateur.
- Le store client du catalogue est en lecture seule.
- Les migrations historiques `001` à `006` de `playstead.db` ne sont jamais modifiées.
- La nouvelle migration `playstead.db` est la version `7`.
- Le premier schéma de `catalog.db` est la version `1`.
- Les connexions SQLite de production utilisent `Pooling=False`, conformément aux stores existants.
- Toute migration reste transactionnelle et sauvegardée avant mutation d’une base existante.
- `catalog.db` ne contient aucun artwork binaire.
- Les nouveaux modèles Core restent indépendants de SQLite, WPF, Windows et des providers concrets.
- Build final obligatoire : `0 warning / 0 error` avec `/warnaserror`.
- Les tests SQLite ont un flake de cleanup déjà connu sur Windows : si un test existant échoue uniquement dans `Dispose()` avec un fichier `.db` verrouillé mais passe isolément, le documenter comme flake préexistant ; ne pas le mélanger au périmètre Phase 1.

---

## Cartographie des fichiers

### Fichiers Core à créer

- `src/PlayStead.Core/Catalog/CatalogContentId.cs` — identifiant technique canonique interne.
- `src/PlayStead.Core/Catalog/PlaySteadPublicId.cs` — identifiant public validé `PlayStead-...` / `PlayStead-DLC-...`.
- `src/PlayStead.Core/Catalog/CatalogContentKind.cs` — `Game`, `Dlc`.
- `src/PlayStead.Core/Catalog/CatalogContentStatus.cs` — `Active`, `Redirected`, `Retired`.
- `src/PlayStead.Core/Catalog/CatalogProviderKind.cs` — providers catalogue, distincts des providers locaux.
- `src/PlayStead.Core/Catalog/CatalogProvenance.cs` — provenance des assertions.
- `src/PlayStead.Core/Catalog/CatalogConfidence.cs` — niveau de confiance.
- `src/PlayStead.Core/Catalog/CatalogRelationKind.cs` — relations de contenu validées par la spec.
- `src/PlayStead.Core/Catalog/CatalogContent.cs` — contenu canonique.
- `src/PlayStead.Core/Catalog/CatalogProviderRef.cs` — référence provider canonique.
- `src/PlayStead.Core/Catalog/CatalogAlias.cs` — alias et provenance.
- `src/PlayStead.Core/Catalog/CatalogContentRelation.cs` — relation canonique.
- `src/PlayStead.Core/Catalog/CatalogMetadata.cs` — version de schéma/catalogue.
- `src/PlayStead.Core/Persistence/ICanonicalCatalogStore.cs` — contrat de lecture du catalogue.

### Fichiers Core à modifier

- `src/PlayStead.Core/Library/LogicalGame.cs` — ajoute `CatalogContentId? CanonicalContentId`.

### Fichiers Data à créer

- `src/PlayStead.Data/Database/Migrations/007_canonical_catalog_link.sql` — lien nullable `games.canonical_content_id`.
- `src/PlayStead.Data/Catalog/CatalogDatabaseOptions.cs` — chemins de `catalog.db`.
- `src/PlayStead.Data/Catalog/CatalogDatabaseInitializer.cs` — bootstrap/migrations du catalogue.
- `src/PlayStead.Data/Catalog/Migrations/001_catalog_initial.sql` — schéma initial du catalogue.
- `src/PlayStead.Data/Catalog/SqliteCanonicalCatalogStore.cs` — store lecture seule.

### Fichiers Data à modifier

- `src/PlayStead.Data/Database/DatabaseInitializer.cs` — `TargetVersion = 7`, migration `007`.
- `src/PlayStead.Data/Library/SqliteLibraryStore.cs` — lit le lien canonique nullable.
- `src/PlayStead.Data/PlayStead.Data.csproj` — embarque les migrations du catalogue.

### Fichiers UI/composition à modifier

- `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs` — enregistre le catalogue local.
- `src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs` — initialise `catalog.db` avant le chargement de la bibliothèque.

### Tests à créer

- `tests/PlayStead.Core.Tests/Catalog/PlaySteadPublicIdTests.cs`
- `tests/PlayStead.Core.Tests/Catalog/CatalogModelTests.cs`
- `tests/PlayStead.Data.Tests/Database/DatabaseCanonicalCatalogLinkMigrationTests.cs`
- `tests/PlayStead.Data.Tests/Catalog/CatalogDatabaseInitializerTests.cs`
- `tests/PlayStead.Data.Tests/Catalog/SqliteCanonicalCatalogStoreTests.cs`
- `tests/PlayStead.UI.Tests/Bootstrap/CanonicalCatalogStartupTests.cs`

### Tests à modifier

- `tests/PlayStead.Data.Tests/Library/SqliteLibraryStoreTests.cs` — prouve que le scan legacy garde `CanonicalContentId = null`.

---

# Task 1 — Contrats Core du catalogue canonique

**Files:**
- Create: `src/PlayStead.Core/Catalog/CatalogContentId.cs`
- Create: `src/PlayStead.Core/Catalog/PlaySteadPublicId.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogContentKind.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogContentStatus.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogProviderKind.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogProvenance.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogConfidence.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogRelationKind.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogContent.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogProviderRef.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogAlias.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogContentRelation.cs`
- Create: `src/PlayStead.Core/Catalog/CatalogMetadata.cs`
- Create: `src/PlayStead.Core/Persistence/ICanonicalCatalogStore.cs`
- Test: `tests/PlayStead.Core.Tests/Catalog/PlaySteadPublicIdTests.cs`
- Test: `tests/PlayStead.Core.Tests/Catalog/CatalogModelTests.cs`

**Interfaces:**
- Consumes: aucun nouveau contrat.
- Produces:
  - `CatalogContentId`
  - `PlaySteadPublicId`
  - enums du catalogue
  - `CatalogContent`
  - `CatalogProviderRef`
  - `CatalogAlias`
  - `CatalogContentRelation`
  - `CatalogMetadata`
  - `ICanonicalCatalogStore`

## Modèle exact à introduire

```csharp
namespace PlayStead.Core.Catalog;

public readonly record struct CatalogContentId(Guid Value)
{
    public static CatalogContentId New() => new(Guid.NewGuid());

    public override string ToString() =>
        Value.ToString("D");
}
```

```csharp
namespace PlayStead.Core.Catalog;

public enum CatalogContentKind
{
    Game = 1,
    Dlc = 2
}

public enum CatalogContentStatus
{
    Active = 1,
    Redirected = 2,
    Retired = 3
}

public enum CatalogProviderKind
{
    Steam = 1,
    Epic = 2,
    Gog = 3,
    Microsoft = 4,
    ItchIo = 5,

    Igdb = 100,
    SteamGridDb = 101,
    Rawg = 102
}

public enum CatalogProvenance
{
    ProviderDirect = 1,
    Igdb = 2,
    SteamGridDb = 3,
    Rawg = 4,
    UserSubmitted = 5,
    UserConfirmed = 6,
    UserRejected = 7,
    AdminConfirmed = 8,
    CatalogMigration = 9
}

public enum CatalogConfidence
{
    Unverified = 0,
    Probabilistic = 1,
    Deterministic = 2
}

public enum CatalogRelationKind
{
    RequiresBaseGame = 1,
    StandaloneExpansionOf = 2,
    RemasterOf = 3,
    RemakeOf = 4,
    DemoOf = 5,
    PrologueOf = 6,
    TestClientOf = 7,
    DedicatedServerOf = 8,
    ToolFor = 9
}
```

`PlaySteadPublicId` accepte uniquement :
- `PlayStead-` + au moins 6 chiffres ASCII ;
- `PlayStead-DLC-` + au moins 6 chiffres ASCII.

```csharp
namespace PlayStead.Core.Catalog;

public readonly record struct PlaySteadPublicId
{
    private const string GamePrefix = "PlayStead-";
    private const string DlcPrefix = "PlayStead-DLC-";

    private PlaySteadPublicId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PlaySteadPublicId Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException(
                $"Invalid PlayStead public id: '{value}'.");
        }

        return result;
    }

    public static bool TryParse(
        string? value,
        out PlaySteadPublicId result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        var digits =
            trimmed.StartsWith(
                DlcPrefix,
                StringComparison.Ordinal)
                ? trimmed[DlcPrefix.Length..]
                : trimmed.StartsWith(
                    GamePrefix,
                    StringComparison.Ordinal)
                    ? trimmed[GamePrefix.Length..]
                    : string.Empty;

        if (digits.Length < 6 ||
            !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        result = new PlaySteadPublicId(trimmed);
        return true;
    }

    public override string ToString() =>
        Value;
}
```

Modèles :

```csharp
namespace PlayStead.Core.Catalog;

public sealed record CatalogContent(
    CatalogContentId Id,
    PlaySteadPublicId PublicId,
    CatalogContentKind Kind,
    string CanonicalTitle,
    string NormalizedTitle,
    DateOnly? ReleaseDate,
    string? Developer,
    string? Publisher,
    CatalogContentStatus Status,
    CatalogContentId? RedirectTargetId);

public sealed record CatalogProviderRef(
    CatalogContentId ContentId,
    CatalogProviderKind Provider,
    string ExternalId,
    string? ExternalType,
    CatalogProvenance Provenance,
    CatalogConfidence Confidence,
    DateTimeOffset ObservedAtUtc);

public sealed record CatalogAlias(
    CatalogContentId ContentId,
    string Alias,
    string NormalizedAlias,
    CatalogProvenance Provenance);

public sealed record CatalogContentRelation(
    CatalogContentId SourceContentId,
    CatalogRelationKind RelationKind,
    CatalogContentId TargetContentId,
    CatalogProvenance Provenance,
    CatalogConfidence Confidence);

public sealed record CatalogMetadata(
    int SchemaVersion,
    long CatalogVersion,
    DateTimeOffset GeneratedAtUtc);
```

Contrat de lecture :

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Persistence;

public interface ICanonicalCatalogStore
{
    Task<CatalogMetadata> GetMetadataAsync(
        CancellationToken cancellationToken);

    Task<CatalogContent?> GetByIdAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken);

    Task<CatalogContent?> GetByPublicIdAsync(
        PlaySteadPublicId publicId,
        CancellationToken cancellationToken);

    Task<CatalogContent?> FindByProviderRefAsync(
        CatalogProviderKind provider,
        string externalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(
        CatalogContentId sourceContentId,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: écrire les tests RED de `PlaySteadPublicId`**

Créer `PlaySteadPublicIdTests.cs` avec au minimum :

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Tests.Catalog;

public sealed class PlaySteadPublicIdTests
{
    [Theory]
    [InlineData("PlayStead-000001")]
    [InlineData("PlayStead-123456")]
    [InlineData("PlayStead-123456789")]
    [InlineData("PlayStead-DLC-000001")]
    [InlineData("PlayStead-DLC-987654")]
    public void Parse_accepts_valid_ids(string value)
    {
        var id = PlaySteadPublicId.Parse(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(value, id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("PlayStead-1")]
    [InlineData("PlayStead-12345")]
    [InlineData("playstead-123456")]
    [InlineData("PlayStead-ABCDEF")]
    [InlineData("Steam-123456")]
    [InlineData("PlayStead-DLC-12345")]
    public void Parse_rejects_invalid_ids(string value)
    {
        Assert.Throws<FormatException>(
            () => PlaySteadPublicId.Parse(value));
    }
}
```

- [ ] **Step 2: lancer les tests pour confirmer RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~PlaySteadPublicIdTests"
```

Attendu : échec de compilation car les types n’existent pas encore.

- [ ] **Step 3: implémenter les types Core ci-dessus**

Créer les fichiers avec les signatures exactes de cette tâche.

- [ ] **Step 4: ajouter un test de cohérence des enums et modèles**

Créer `CatalogModelTests.cs` :

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Tests.Catalog;

public sealed class CatalogModelTests
{
    [Fact]
    public void Canonical_content_supports_active_game_without_redirect()
    {
        var id =
            new CatalogContentId(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"));

        var content = new CatalogContent(
            id,
            PlaySteadPublicId.Parse("PlayStead-000001"),
            CatalogContentKind.Game,
            "Gray Zone Warfare",
            "gray zone warfare",
            new DateOnly(2024, 4, 30),
            "MADFINGER Games",
            "MADFINGER Games",
            CatalogContentStatus.Active,
            null);

        Assert.Equal(id, content.Id);
        Assert.Equal(CatalogContentKind.Game, content.Kind);
        Assert.Null(content.RedirectTargetId);
    }

    [Fact]
    public void Catalog_provider_values_are_independent_from_library_provider_values()
    {
        Assert.Equal(1, (int)CatalogProviderKind.Steam);
        Assert.Equal(100, (int)CatalogProviderKind.Igdb);
        Assert.Equal(101, (int)CatalogProviderKind.SteamGridDb);
        Assert.Equal(102, (int)CatalogProviderKind.Rawg);
    }
}
```

- [ ] **Step 5: lancer les tests Core ciblés**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Catalog"
```

Attendu : PASS.

- [ ] **Step 6: vérifier les warnings et le diff**

```powershell
dotnet build ".\src\PlayStead.Core\PlayStead.Core.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

Attendu : build propre et aucun défaut whitespace.

- [ ] **Step 7: commit atomique**

```powershell
git add `
  "src/PlayStead.Core/Catalog" `
  "src/PlayStead.Core/Persistence/ICanonicalCatalogStore.cs" `
  "tests/PlayStead.Core.Tests/Catalog"

git commit -m "feat(catalog): add canonical catalog contracts"
```

---

# Task 2 — Lien canonique nullable dans `playstead.db` sans changer le scan actuel

**Files:**
- Create: `src/PlayStead.Data/Database/Migrations/007_canonical_catalog_link.sql`
- Create: `tests/PlayStead.Data.Tests/Database/DatabaseCanonicalCatalogLinkMigrationTests.cs`
- Modify: `src/PlayStead.Data/Database/DatabaseInitializer.cs`
- Modify: `src/PlayStead.Core/Library/LogicalGame.cs`
- Modify: `src/PlayStead.Data/Library/SqliteLibraryStore.cs`
- Modify: `tests/PlayStead.Data.Tests/Library/SqliteLibraryStoreTests.cs`

**Interfaces:**
- Consumes: `CatalogContentId` de Task 1.
- Produces:
  - `LogicalGame.CanonicalContentId`
  - schéma `playstead.db` v7 avec `games.canonical_content_id`
- Le scan actuel ne consomme pas encore le catalogue et garde ce champ `NULL`.

## Migration exacte

`007_canonical_catalog_link.sql` :

```sql
ALTER TABLE games
ADD COLUMN canonical_content_id TEXT NULL;

CREATE INDEX IF NOT EXISTS
    ix_games_canonical_content_id
ON games(canonical_content_id);
```

Aucune FK SQLite n’est ajoutée : la cible se trouve dans une autre base (`catalog.db`).

- [ ] **Step 1: écrire le test RED de migration v6 → v7**

Créer `DatabaseCanonicalCatalogLinkMigrationTests.cs`.

Le test construit une base minimale représentant la version 6, avec :
- `schema_migrations` contenant les versions 1 à 6 ;
- une table `games` pré-v7 ;
- une ligne existante.

Exemple de préparation :

```csharp
private static async Task CreateVersion6FixtureAsync(
    string databasePath)
{
    await using var connection = new SqliteConnection(
        $"Data Source={databasePath};Pooling=False");

    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE schema_migrations(
            version INTEGER PRIMARY KEY,
            applied_utc TEXT NOT NULL
        );

        CREATE TABLE games(
            game_id TEXT PRIMARY KEY,
            title TEXT NOT NULL,
            is_hidden INTEGER NOT NULL,
            created_utc TEXT NOT NULL,
            updated_utc TEXT NOT NULL
        );

        INSERT INTO schema_migrations(version, applied_utc)
        VALUES
            (1, '2026-09-01T00:00:00.0000000+00:00'),
            (2, '2026-09-01T00:00:00.0000000+00:00'),
            (3, '2026-09-01T00:00:00.0000000+00:00'),
            (4, '2026-09-01T00:00:00.0000000+00:00'),
            (5, '2026-09-01T00:00:00.0000000+00:00'),
            (6, '2026-09-01T00:00:00.0000000+00:00');

        INSERT INTO games(
            game_id,
            title,
            is_hidden,
            created_utc,
            updated_utc)
        VALUES(
            '11111111-1111-1111-1111-111111111111',
            'Existing Game',
            0,
            '2026-09-01T00:00:00.0000000+00:00',
            '2026-09-01T00:00:00.0000000+00:00');
        """;

    await command.ExecuteNonQueryAsync();
}
```

Assertions après `DatabaseInitializer.InitializeAsync` :

```csharp
Assert.Equal(
    7,
    await ReadSchemaVersionAsync(databasePath));

Assert.True(
    await ColumnExistsAsync(
        databasePath,
        "games",
        "canonical_content_id"));

Assert.Equal(
    "Existing Game",
    await ReadTitleAsync(databasePath));

Assert.Null(
    await ReadCanonicalContentIdAsync(databasePath));
```

- [ ] **Step 2: lancer le test pour confirmer RED**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~DatabaseCanonicalCatalogLinkMigrationTests"
```

Attendu : FAIL car `TargetVersion` est encore 6 et la colonne n’existe pas.

- [ ] **Step 3: ajouter la migration v7 au bootstrap**

Dans `DatabaseInitializer.cs` :

```csharp
private const int TargetVersion = 7;

private static readonly IReadOnlyDictionary<int, string> MigrationFiles =
    new Dictionary<int, string>
    {
        [1] = "001_initial.sql",
        [2] = "002_steam_evidence.sql",
        [3] = "003_sessions.sql",
        [4] = "004_session_corrections.sql",
        [5] = "005_session_corrections_traceable.sql",
        [6] = "006_process_signature_discovery.sql",
        [7] = "007_canonical_catalog_link.sql"
    };
```

Créer le SQL exact indiqué plus haut.

Ne modifier aucune migration `001` à `006`.

- [ ] **Step 4: modifier `LogicalGame` de manière rétrocompatible**

```csharp
using PlayStead.Core.Catalog;

namespace PlayStead.Core.Library;

public sealed record LogicalGame(
    GameId Id,
    string Title,
    bool IsHidden,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    CatalogContentId? CanonicalContentId = null);
```

Le paramètre est final et optionnel afin que les constructions historiques restent valides sans réécriture massive.

- [ ] **Step 5: lire le champ nullable dans `SqliteLibraryStore`**

Modifier la requête `LoadGamesAsync` pour sélectionner :

```sql
SELECT
    game_id,
    title,
    is_hidden,
    created_utc,
    updated_utc,
    canonical_content_id
FROM games
ORDER BY title COLLATE NOCASE, game_id;
```

Construire :

```csharp
new LogicalGame(
    new GameId(
        Guid.Parse(reader.GetString(0))),
    reader.GetString(1),
    reader.GetInt64(2) != 0,
    ParseUtc(reader.GetString(3)),
    ParseUtc(reader.GetString(4)),
    reader.IsDBNull(5)
        ? null
        : new CatalogContentId(
            Guid.Parse(reader.GetString(5))));
```

Ajouter :

```csharp
using PlayStead.Core.Catalog;
```

Ne modifier ni `GetOrCreateGameAsync`, ni l’algorithme `(provider, external_id)` en Phase 1.

- [ ] **Step 6: prouver que le scan actuel crée toujours une identité locale sans lien canonique**

Dans `SqliteLibraryStoreTests.First_complete_scan_creates_game_provider_ref_and_installation` ajouter :

```csharp
Assert.Null(game.CanonicalContentId);
```

Dans `Second_identical_scan_reuses_game_and_installation_identity`, ajouter :

```csharp
Assert.Null(
    Assert.Single(second.Games)
        .CanonicalContentId);
```

- [ ] **Step 7: lancer les tests Data ciblés**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~DatabaseCanonicalCatalogLinkMigrationTests|FullyQualifiedName~SqliteLibraryStoreTests"
```

Attendu : PASS.

Si un test échoue uniquement pendant `Dispose()` avec `.db` verrouillé, le relancer isolément et confirmer le flake connu avant toute conclusion.

- [ ] **Step 8: build Core + Data et diff check**

```powershell
dotnet build ".\src\PlayStead.Data\PlayStead.Data.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] **Step 9: commit atomique**

```powershell
git add `
  "src/PlayStead.Core/Library/LogicalGame.cs" `
  "src/PlayStead.Data/Database/DatabaseInitializer.cs" `
  "src/PlayStead.Data/Database/Migrations/007_canonical_catalog_link.sql" `
  "src/PlayStead.Data/Library/SqliteLibraryStore.cs" `
  "tests/PlayStead.Data.Tests/Database/DatabaseCanonicalCatalogLinkMigrationTests.cs" `
  "tests/PlayStead.Data.Tests/Library/SqliteLibraryStoreTests.cs"

git commit -m "feat(catalog): link local games to canonical content"
```

---

# Task 3 — Créer `catalog.db` et son schéma versionné

**Files:**
- Create: `src/PlayStead.Data/Catalog/CatalogDatabaseOptions.cs`
- Create: `src/PlayStead.Data/Catalog/CatalogDatabaseInitializer.cs`
- Create: `src/PlayStead.Data/Catalog/Migrations/001_catalog_initial.sql`
- Create: `tests/PlayStead.Data.Tests/Catalog/CatalogDatabaseInitializerTests.cs`
- Modify: `src/PlayStead.Data/PlayStead.Data.csproj`

**Interfaces:**
- Consumes: enums Core de Task 1.
- Produces:
  - `catalog.db` schéma v1
  - `CatalogDatabaseOptions`
  - `CatalogDatabaseInitializer`
- Aucun store de lecture n’est encore requis dans cette Task.

## Options exactes

```csharp
namespace PlayStead.Data.Catalog;

public sealed record CatalogDatabaseOptions(
    string CatalogPath,
    string BackupsDirectory);
```

## Schéma initial exact

`001_catalog_initial.sql` :

```sql
PRAGMA foreign_keys = ON;

CREATE TABLE catalog_schema_migrations(
    version INTEGER PRIMARY KEY,
    applied_utc TEXT NOT NULL
);

CREATE TABLE catalog_metadata(
    singleton_id INTEGER PRIMARY KEY
        CHECK(singleton_id = 1),
    catalog_version INTEGER NOT NULL
        CHECK(catalog_version >= 0),
    generated_at_utc TEXT NOT NULL
);

INSERT INTO catalog_metadata(
    singleton_id,
    catalog_version,
    generated_at_utc)
VALUES(
    1,
    0,
    '1970-01-01T00:00:00.0000000+00:00');

CREATE TABLE catalog_contents(
    internal_content_id TEXT PRIMARY KEY,
    public_id TEXT NOT NULL UNIQUE,
    content_kind INTEGER NOT NULL
        CHECK(content_kind IN (1, 2)),
    canonical_title TEXT NOT NULL,
    normalized_title TEXT NOT NULL,
    release_date TEXT NULL,
    developer TEXT NULL,
    publisher TEXT NULL,
    status INTEGER NOT NULL
        CHECK(status IN (1, 2, 3)),
    redirect_target_id TEXT NULL,
    FOREIGN KEY(redirect_target_id)
        REFERENCES catalog_contents(internal_content_id),
    CHECK(
        (status = 2 AND redirect_target_id IS NOT NULL)
        OR
        (status IN (1, 3) AND redirect_target_id IS NULL)),
    CHECK(
        redirect_target_id IS NULL
        OR redirect_target_id <> internal_content_id)
);

CREATE INDEX ix_catalog_contents_normalized_title
ON catalog_contents(normalized_title);

CREATE INDEX ix_catalog_contents_redirect_target
ON catalog_contents(redirect_target_id);

CREATE TABLE catalog_provider_refs(
    provider INTEGER NOT NULL,
    external_id TEXT NOT NULL,
    content_id TEXT NOT NULL,
    external_type TEXT NULL,
    provenance INTEGER NOT NULL,
    confidence INTEGER NOT NULL,
    observed_at_utc TEXT NOT NULL,
    PRIMARY KEY(provider, external_id),
    FOREIGN KEY(content_id)
        REFERENCES catalog_contents(internal_content_id)
        ON DELETE CASCADE
);

CREATE INDEX ix_catalog_provider_refs_content
ON catalog_provider_refs(content_id);

CREATE TABLE catalog_aliases(
    content_id TEXT NOT NULL,
    alias TEXT NOT NULL,
    normalized_alias TEXT NOT NULL,
    provenance INTEGER NOT NULL,
    PRIMARY KEY(content_id, alias),
    FOREIGN KEY(content_id)
        REFERENCES catalog_contents(internal_content_id)
        ON DELETE CASCADE
);

CREATE INDEX ix_catalog_aliases_normalized
ON catalog_aliases(normalized_alias);

CREATE TABLE catalog_relations(
    source_content_id TEXT NOT NULL,
    relation_kind INTEGER NOT NULL,
    target_content_id TEXT NOT NULL,
    provenance INTEGER NOT NULL,
    confidence INTEGER NOT NULL,
    PRIMARY KEY(
        source_content_id,
        relation_kind,
        target_content_id),
    FOREIGN KEY(source_content_id)
        REFERENCES catalog_contents(internal_content_id)
        ON DELETE CASCADE,
    FOREIGN KEY(target_content_id)
        REFERENCES catalog_contents(internal_content_id)
        ON DELETE CASCADE,
    CHECK(source_content_id <> target_content_id)
);

CREATE INDEX ix_catalog_relations_target
ON catalog_relations(target_content_id);
```

Le `catalog_version = 0` représente un catalogue local vide de fondation. Les futures phases d’ingestion/synchronisation publieront des versions > 0.

- [ ] **Step 1: écrire les tests RED de bootstrap `catalog.db`**

Créer `CatalogDatabaseInitializerTests.cs` avec :
- création de base vide ;
- version de schéma = 1 ;
- version catalogue = 0 ;
- présence des 5 tables métier ;
- réexécution idempotente ;
- contraintes provider refs.

Exemple :

```csharp
[Fact]
public async Task Initialize_creates_empty_versioned_catalog()
{
    var options = CreateOptions();

    await new CatalogDatabaseInitializer(options)
        .InitializeAsync(CancellationToken.None);

    Assert.True(File.Exists(options.CatalogPath));

    Assert.Equal(
        1,
        await ReadSchemaVersionAsync(
            options.CatalogPath));

    Assert.Equal(
        0L,
        await ReadCatalogVersionAsync(
            options.CatalogPath));

    Assert.True(
        await TableExistsAsync(
            options.CatalogPath,
            "catalog_contents"));

    Assert.True(
        await TableExistsAsync(
            options.CatalogPath,
            "catalog_provider_refs"));

    Assert.True(
        await TableExistsAsync(
            options.CatalogPath,
            "catalog_aliases"));

    Assert.True(
        await TableExistsAsync(
            options.CatalogPath,
            "catalog_relations"));
}
```

- [ ] **Step 2: lancer pour confirmer RED**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~CatalogDatabaseInitializerTests"
```

Attendu : FAIL, types et migration absents.

- [ ] **Step 3: embarquer les migrations catalogue dans `PlayStead.Data.csproj`**

Ajouter dans l’`ItemGroup` d’EmbeddedResource existant :

```xml
<EmbeddedResource Include="Catalog\Migrations\*.sql" />
```

Ne retirer aucune ressource existante de `Database\Migrations`.

- [ ] **Step 4: implémenter `CatalogDatabaseInitializer`**

Suivre le même pattern que `DatabaseInitializer` existant :
- `TargetVersion = 1`;
- `MigrationFiles[1] = "001_catalog_initial.sql"`;
- `Pooling=False`;
- backup si `catalog.db` existe avant migration ;
- transaction par migration ;
- restauration du backup sur erreur ;
- lecture de ressource embarquée sous :
  `PlayStead.Data.Catalog.Migrations.<filename>`.

Le nom de backup est :

```csharp
$"catalog.db.pre-migration-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.bak"
```

La lecture de version interroge :

```sql
SELECT COALESCE(MAX(version), 0)
FROM catalog_schema_migrations;
```

Si la table n’existe pas encore, traiter l’erreur `no such table` comme version `0`, exactement comme le bootstrap initial de `playstead.db` doit gérer une base vierge.

- [ ] **Step 5: ajouter le test de contrainte globale provider ref**

Après initialisation, insérer deux contenus puis tenter deux références avec le même `(provider, external_id)`.

Exemple de contrainte attendue :

```csharp
await Assert.ThrowsAsync<SqliteException>(
    async () =>
    {
        await InsertProviderRefAsync(
            options.CatalogPath,
            secondContent,
            provider: 1,
            externalId: "2479810");
    });
```

Cela prouve l’invariant :
> une référence provider canonique ne peut désigner qu’un seul contenu actif enregistré dans le catalogue.

- [ ] **Step 6: lancer les tests ciblés**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~CatalogDatabaseInitializerTests"
```

Attendu : PASS.

- [ ] **Step 7: build Data + diff check**

```powershell
dotnet build ".\src\PlayStead.Data\PlayStead.Data.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] **Step 8: commit atomique**

```powershell
git add `
  "src/PlayStead.Data/Catalog/CatalogDatabaseOptions.cs" `
  "src/PlayStead.Data/Catalog/CatalogDatabaseInitializer.cs" `
  "src/PlayStead.Data/Catalog/Migrations/001_catalog_initial.sql" `
  "src/PlayStead.Data/PlayStead.Data.csproj" `
  "tests/PlayStead.Data.Tests/Catalog/CatalogDatabaseInitializerTests.cs"

git commit -m "feat(catalog): add local canonical catalog database"
```

---

# Task 4 — Store SQLite de lecture du catalogue canonique

**Files:**
- Create: `src/PlayStead.Data/Catalog/SqliteCanonicalCatalogStore.cs`
- Create: `tests/PlayStead.Data.Tests/Catalog/SqliteCanonicalCatalogStoreTests.cs`

**Interfaces:**
- Consumes:
  - `ICanonicalCatalogStore`
  - `CatalogDatabaseOptions`
  - modèles Core du catalogue
- Produces:
  - implémentation SQLite strictement lecture seule
- Aucune méthode d’écriture n’est exposée au client.

## Connexion obligatoire

Toutes les lectures utilisent :

```csharp
await using var connection = new SqliteConnection(
    $"Data Source={_options.CatalogPath};Mode=ReadOnly;Pooling=False");
```

Après ouverture :

```csharp
await connection.OpenAsync(cancellationToken);
```

- [ ] **Step 1: écrire les tests RED**

Créer `SqliteCanonicalCatalogStoreTests.cs`.

Le fixture :
1. crée un répertoire temporaire ;
2. lance `CatalogDatabaseInitializer`;
3. seed les données directement par `SqliteConnection` dans le test ;
4. instancie `SqliteCanonicalCatalogStore`.

Seed minimal :

```text
PlayStead-001284
Gray Zone Warfare
Steam 2479810

PlayStead-DLC-000417
Supporter Pack
RequiresBaseGame → PlayStead-001284
```

Tests obligatoires :
- metadata v1 / catalog version 0 ;
- `GetByIdAsync`;
- `GetByPublicIdAsync`;
- `FindByProviderRefAsync(Steam, "2479810")`;
- provider inconnu → null ;
- `GetProviderRefsAsync`;
- `GetAliasesAsync`;
- `GetRelationsFromAsync`;
- redirect retourné tel qu’il est stocké, sans résolution automatique en Phase 1.

- [ ] **Step 2: lancer pour confirmer RED**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~SqliteCanonicalCatalogStoreTests"
```

Attendu : FAIL car le store n’existe pas.

- [ ] **Step 3: implémenter `GetMetadataAsync`**

Requête :

```sql
SELECT
    (SELECT COALESCE(MAX(version), 0)
     FROM catalog_schema_migrations),
    catalog_version,
    generated_at_utc
FROM catalog_metadata
WHERE singleton_id = 1;
```

Retour :

```csharp
new CatalogMetadata(
    reader.GetInt32(0),
    reader.GetInt64(1),
    ParseUtc(reader.GetString(2)));
```

- [ ] **Step 4: implémenter la lecture d’un contenu**

Requête commune :

```sql
SELECT
    internal_content_id,
    public_id,
    content_kind,
    canonical_title,
    normalized_title,
    release_date,
    developer,
    publisher,
    status,
    redirect_target_id
FROM catalog_contents
```

Mapping :

```csharp
new CatalogContent(
    new CatalogContentId(
        Guid.Parse(reader.GetString(0))),
    PlaySteadPublicId.Parse(
        reader.GetString(1)),
    (CatalogContentKind)reader.GetInt32(2),
    reader.GetString(3),
    reader.GetString(4),
    reader.IsDBNull(5)
        ? null
        : DateOnly.ParseExact(
            reader.GetString(5),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture),
    reader.IsDBNull(6)
        ? null
        : reader.GetString(6),
    reader.IsDBNull(7)
        ? null
        : reader.GetString(7),
    (CatalogContentStatus)reader.GetInt32(8),
    reader.IsDBNull(9)
        ? null
        : new CatalogContentId(
            Guid.Parse(reader.GetString(9))));
```

`GetByIdAsync` ajoute :

```sql
WHERE internal_content_id = $id
```

`GetByPublicIdAsync` ajoute :

```sql
WHERE public_id = $publicId
```

- [ ] **Step 5: implémenter `FindByProviderRefAsync`**

Requête exacte :

```sql
SELECT
    c.internal_content_id,
    c.public_id,
    c.content_kind,
    c.canonical_title,
    c.normalized_title,
    c.release_date,
    c.developer,
    c.publisher,
    c.status,
    c.redirect_target_id
FROM catalog_provider_refs r
JOIN catalog_contents c
    ON c.internal_content_id = r.content_id
WHERE r.provider = $provider
  AND r.external_id = $externalId;
```

Le lookup reste exact ; aucun fuzzy match ou fallback de titre n’est autorisé ici.

- [ ] **Step 6: implémenter les listes de refs, alias et relations**

Provider refs :

```sql
SELECT
    content_id,
    provider,
    external_id,
    external_type,
    provenance,
    confidence,
    observed_at_utc
FROM catalog_provider_refs
WHERE content_id = $contentId
ORDER BY provider, external_id;
```

Alias :

```sql
SELECT
    content_id,
    alias,
    normalized_alias,
    provenance
FROM catalog_aliases
WHERE content_id = $contentId
ORDER BY alias COLLATE NOCASE;
```

Relations :

```sql
SELECT
    source_content_id,
    relation_kind,
    target_content_id,
    provenance,
    confidence
FROM catalog_relations
WHERE source_content_id = $sourceId
ORDER BY relation_kind, target_content_id;
```

- [ ] **Step 7: lancer les tests ciblés**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~SqliteCanonicalCatalogStoreTests"
```

Attendu : PASS.

- [ ] **Step 8: ajouter un test explicite “aucune API d’écriture”**

Dans `SqliteCanonicalCatalogStoreTests` :

```csharp
[Fact]
public void Consumer_contract_exposes_no_mutation_method()
{
    var methodNames =
        typeof(ICanonicalCatalogStore)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

    Assert.DoesNotContain(
        methodNames,
        name =>
            name.StartsWith(
                "Insert",
                StringComparison.Ordinal) ||
            name.StartsWith(
                "Update",
                StringComparison.Ordinal) ||
            name.StartsWith(
                "Delete",
                StringComparison.Ordinal) ||
            name.StartsWith(
                "Upsert",
                StringComparison.Ordinal));
}
```

- [ ] **Step 9: build et diff check**

```powershell
dotnet build ".\src\PlayStead.Data\PlayStead.Data.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] **Step 10: commit atomique**

```powershell
git add `
  "src/PlayStead.Data/Catalog/SqliteCanonicalCatalogStore.cs" `
  "tests/PlayStead.Data.Tests/Catalog/SqliteCanonicalCatalogStoreTests.cs"

git commit -m "feat(catalog): add read-only canonical catalog store"
```

---

# Task 5 — Intégrer `catalog.db` au bootstrap production

**Files:**
- Modify: `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`
- Modify: `src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs`
- Create: `tests/PlayStead.UI.Tests/Bootstrap/CanonicalCatalogStartupTests.cs`

**Interfaces:**
- Consumes:
  - `CatalogDatabaseOptions`
  - `CatalogDatabaseInitializer`
  - `ICanonicalCatalogStore`
  - `SqliteCanonicalCatalogStore`
- Produces:
  - `catalog.db` existe après startup production ;
  - store canonique disponible via DI ;
  - aucun changement du snapshot Library.
- Les anciens constructeurs de `LocalStartupPipeline` restent disponibles pour les tests existants.

## Composition DI exacte

Dans `PlaySteadHost.Build`, après validation de `layout` :

```csharp
var dataRoot =
    Path.GetDirectoryName(
        layout.DatabasePath)
    ?? throw new InvalidOperationException(
        "PlayStead database path has no parent directory.");
```

Enregistrer :

```csharp
builder.Services.AddSingleton(
    new CatalogDatabaseOptions(
        Path.Combine(
            dataRoot,
            "catalog.db"),
        Path.Combine(
            layout.BackupsDirectory,
            "Catalog")));

builder.Services.AddSingleton<
    CatalogDatabaseInitializer>();

builder.Services.AddSingleton<
    ICanonicalCatalogStore,
    SqliteCanonicalCatalogStore>();
```

Ajouter les `using` nécessaires :
- `PlayStead.Data.Catalog`
- `PlayStead.Core.Persistence` existe déjà.

- [ ] **Step 1: écrire le test RED de bootstrap**

Créer `CanonicalCatalogStartupTests.cs`.

Structure :

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Persistence;
using PlayStead.Data.Catalog;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class CanonicalCatalogStartupTests
{
    [Fact]
    public async Task Production_host_initializes_catalog_without_network_dependency()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout =
            UserDataLayout.FromRoot(root);

        layout.EnsureDirectoriesExist();

        using var host =
            PlaySteadHost.Build(layout);

        try
        {
            var pipeline =
                host.Services.GetRequiredService<
                    LocalStartupPipeline>();

            var state =
                await pipeline.InitializeAsync(
                    CancellationToken.None);

            var catalogOptions =
                host.Services.GetRequiredService<
                    CatalogDatabaseOptions>();

            var catalogStore =
                host.Services.GetRequiredService<
                    ICanonicalCatalogStore>();

            Assert.True(
                File.Exists(
                    layout.DatabasePath));

            Assert.True(
                File.Exists(
                    catalogOptions.CatalogPath));

            Assert.NotNull(catalogStore);

            Assert.Equal(
                1,
                (await catalogStore.GetMetadataAsync(
                    CancellationToken.None))
                    .SchemaVersion);

            Assert.NotNull(state.Snapshot);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
```

Si `UserDataLayout` n’est pas dans `PlayStead.Platform.Paths` dans le checkout actuel, utiliser son namespace existant sans déplacer le type.

- [ ] **Step 2: lancer pour confirmer RED**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~CanonicalCatalogStartupTests"
```

Attendu : FAIL car le service catalogue n’est pas enregistré/initialisé.

- [ ] **Step 3: ajouter le champ nullable dans `LocalStartupPipeline`**

```csharp
private readonly CatalogDatabaseInitializer?
    _catalogDatabaseInitializer;
```

Ajouter :

```csharp
using PlayStead.Data.Catalog;
```

Conserver les constructeurs actuels 4, 5 et 6 paramètres.

- [ ] **Step 4: ajouter un constructeur production 7 paramètres**

```csharp
public LocalStartupPipeline(
    DatabaseInitializer databaseInitializer,
    DatabaseHealthChecker databaseHealthChecker,
    ILibraryStore libraryStore,
    LocalScanCoordinator scanCoordinator,
    ISteamReferenceRuntime steamReferenceRuntime,
    DiscoveryInventoryManager discoveryInventory,
    CatalogDatabaseInitializer catalogDatabaseInitializer)
    : this(
        databaseInitializer,
        databaseHealthChecker,
        libraryStore,
        scanCoordinator,
        steamReferenceRuntime,
        discoveryInventory)
{
    _catalogDatabaseInitializer =
        catalogDatabaseInitializer
        ?? throw new ArgumentNullException(
            nameof(catalogDatabaseInitializer));
}
```

Le DI Microsoft choisit le constructeur public le plus riche dont toutes les dépendances sont résolues. Les tests existants qui instancient explicitement les anciens constructeurs ne sont pas forcés à changer.

- [ ] **Step 5: initialiser `catalog.db` pendant le startup**

Dans `InitializeAsync`, immédiatement après `playstead.db` :

```csharp
await _databaseInitializer.InitializeAsync(
    cancellationToken);

if (_catalogDatabaseInitializer is not null)
{
    await _catalogDatabaseInitializer.InitializeAsync(
        cancellationToken);
}
```

Puis conserver exactement le flux existant :
- quick check `playstead.db`;
- load snapshot ;
- inventory discovery ;
- Steam cached.

Ne pas faire de scan réseau du catalogue.

- [ ] **Step 6: enregistrer les services dans `PlaySteadHost`**

Ajouter les services exacts indiqués ci-dessus.

Ne pas modifier :
- `HttpClient` existant ;
- Steam media ;
- Steam reference runtime ;
- SessionRuntime ;
- DiscoveryInventoryManager.

- [ ] **Step 7: lancer le test bootstrap ciblé**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~CanonicalCatalogStartupTests"
```

Attendu : PASS.

- [ ] **Step 8: lancer les tests bootstrap/sessions sensibles à la composition DI**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Bootstrap|FullyQualifiedName~ProcessDiscoveryProductionPipelineTests|FullyQualifiedName~HomeMediaIntegrationTests"
```

Attendu : PASS.

- [ ] **Step 9: build UI `/warnaserror`**

```powershell
dotnet build ".\src\PlayStead.UI\PlayStead.UI.csproj" `
  --configuration Release `
  --no-restore `
  /warnaserror

git diff --check
```

- [ ] **Step 10: commit atomique**

```powershell
git add `
  "src/PlayStead.UI/Bootstrap/PlaySteadHost.cs" `
  "src/PlayStead.UI/Bootstrap/LocalStartupPipeline.cs" `
  "tests/PlayStead.UI.Tests/Bootstrap/CanonicalCatalogStartupTests.cs"

git commit -m "feat(catalog): initialize local catalog at startup"
```

---

# Task 6 — Gate Phase 1 et non-régression

**Files:**
- No production files expected unless a gate reveals a genuine Phase 1 defect.
- Test changes are allowed only when they express the already-approved Phase 1 contract; do not weaken unrelated assertions.

**Interfaces:**
- Consumes: all deliverables Tasks 1–5.
- Produces: evidence that Phase 1 is independently shippable and does not activate Phase 2+ behavior.

- [ ] **Step 1: vérifier le status et l’historique**

```powershell
git status --short
git log -6 --oneline
```

Attendu : worktree propre avant le gate final.

- [ ] **Step 2: tests Core catalogue**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Catalog"
```

Attendu : PASS.

- [ ] **Step 3: tests Data catalogue + migration locale**

```powershell
dotnet test ".\tests\PlayStead.Data.Tests\PlayStead.Data.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~Catalog|FullyQualifiedName~DatabaseCanonicalCatalogLinkMigrationTests|FullyQualifiedName~SqliteLibraryStoreTests"
```

Attendu : PASS.

Si `SqliteLibraryStoreTests` échoue uniquement dans `Dispose()` avec `IOException` sur `.db`, relancer le test exact isolément. S’il passe isolément, noter le flake connu et ne pas altérer la logique catalogue pour le masquer.

- [ ] **Step 4: tests UI/bootstrap**

```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~CanonicalCatalogStartupTests|FullyQualifiedName~ProcessDiscoveryProductionPipelineTests|FullyQualifiedName~HomeMediaIntegrationTests"
```

Attendu : PASS.

- [ ] **Step 5: suite complète**

Écrire la sortie dans un rapport court côté console :

```powershell
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$Report = "D:\Dev\PlayStead\02_RAPPORTS\PLAYSTEAD_PHASE1_CATALOG_FINAL_$Stamp.txt"

dotnet test ".\PlayStead.sln" `
  --configuration Release `
  --no-restore `
  *> $Report

$TestsExit = $LASTEXITCODE

Write-Host "FULL_TESTS=$(if ($TestsExit -eq 0) {'PASS'} else {'FAIL'})"
Write-Host "REPORT=$Report"
```

Attendu idéal : `FULL_TESTS=PASS`.

Si la suite complète échoue uniquement sur les locks SQLite préexistants :
1. relever les noms exacts ;
2. lancer chaque test isolément ;
3. confirmer `PASS` isolé ;
4. conserver le rapport dans `02_RAPPORTS`;
5. ne pas déclarer un gate GREEN si une nouvelle régression fonctionnelle est présente.

- [ ] **Step 6: build solution strict**

```powershell
$BuildReport = "$Report.build.txt"

dotnet build ".\PlayStead.sln" `
  --configuration Release `
  --no-restore `
  /warnaserror `
  *> $BuildReport

Write-Host "BUILD=$(if ($LASTEXITCODE -eq 0) {'PASS'} else {'FAIL'})"
```

Attendu : `BUILD=PASS`.

- [ ] **Step 7: vérifier le périmètre Phase 1**

Exécuter :

```powershell
git grep -n "PS-TEMP" -- "src" ":!docs" 2>$null
git grep -n "MATCH_CONFIRMED" -- "src" ":!docs" 2>$null
git grep -n "Notification Center" -- "src" ":!docs" 2>$null
git grep -n "CatalogServiceOptions" -- "src" ":!docs" 2>$null
```

Attendu : aucune activation Phase 2/3.

Vérifier aussi qu’aucun nouveau code réseau catalogue n’a été introduit :

```powershell
git diff "fec7963..HEAD" -- "src/PlayStead.Data/Catalog" "src/PlayStead.Core/Catalog" |
    Select-String -Pattern "HttpClient|HttpRequest|https://|http://"
```

Attendu : aucune correspondance.

- [ ] **Step 8: vérifier les deux bases séparées**

Le test bootstrap doit avoir prouvé :
- `playstead.db` existe ;
- `catalog.db` existe ;
- `playstead.db.games.canonical_content_id` est nullable ;
- aucun scan Steam ne fabrique de canonical ID ;
- `catalog.db` démarre vide à `catalog_version = 0`.

Si besoin de preuve manuelle additionnelle, utiliser une fixture de test, jamais la DB utilisateur réelle.

- [ ] **Step 9: `git diff --check` final**

```powershell
git diff --check
git status --short
```

Attendu : aucun whitespace error et worktree propre après les commits.

- [ ] **Step 10: tag logique de fermeture dans le rapport de travail**

Le résultat Phase 1 peut être déclaré :

```text
PHASE1_CANONICAL_CATALOG_FOUNDATION=GREEN
```

uniquement si :
- contrats Core PASS ;
- migration `playstead.db` v7 PASS ;
- `catalog.db` v1 PASS ;
- store lecture seule PASS ;
- bootstrap production PASS ;
- build strict PASS ;
- aucune fonctionnalité Phase 2+ activée ;
- aucune régression fonctionnelle non expliquée.

Aucun commit de code supplémentaire n’est nécessaire uniquement pour écrire cette chaîne ; elle appartient au rapport de validation, pas à la production.

---

# Contrats inter-Tasks récapitulatifs

## Contrat A — identité locale

`GameId` reste inchangé :

```csharp
public readonly record struct GameId(Guid Value)
```

Les sessions, installations, signatures et ViewModels continuent de l’utiliser en Phase 1.

## Contrat B — identité canonique

Le lien local est :

```csharp
CatalogContentId? LogicalGame.CanonicalContentId
```

Il reste `null` tant qu’aucune phase future ne le résout explicitement.

## Contrat C — base canonique

Le chemin runtime est :

```text
<DataRoot>\catalog.db
```

et les backups de migration :

```text
<BackupsDirectory>\Catalog\
```

## Contrat D — store client

`ICanonicalCatalogStore` est lecture seule.

Phase 1 n’expose aucun :
- Insert ;
- Update ;
- Delete ;
- Upsert ;
- Merge ;
- Split ;
- Resolve.

## Contrat E — catalogue initial

Le catalogue Phase 1 est valide mais vide :

```text
schema_version = 1
catalog_version = 0
generated_at_utc = 1970-01-01T00:00:00.0000000+00:00
```

Cela permet de tester la fondation sans inventer de `PlaySteadGameId` global localement.

---

# Explicitement différé après Phase 1

Les éléments suivants ne doivent pas apparaître dans l’implémentation de ce plan :

- `PS-TEMP`
- `GameIdentityResolver`
- `MATCH_CONFIRMED`
- `MATCH_PROBABLE`
- `AMBIGUOUS`
- `NEW`
- Notification Center
- cloche UI
- `UserConfirmed`
- `UserRejected`
- résolution cross-store active
- serveur OVH
- `CatalogServiceOptions.BaseUri`
- téléchargement de catalogue
- deltas
- hash/signature réseau
- soumissions clientes
- API admin
- IGDB
- SteamGridDB
- RAWG
- scan Epic
- scan GOG
- agrégation du temps de jeu multi-store
- UI « Identités & relations »
- UI « Voir tous les DLC »
- merge/split runtime
- redirects suivis automatiquement par le store

Le schéma Phase 1 contient les structures nécessaires pour permettre ces évolutions, mais aucun de ces comportements n’est activé.

---

# Self-review du plan

## 1. Couverture de la spec pour la Phase 1

Couvert :
- séparation `playstead.db` / `catalog.db` ;
- identité locale distincte de l’identité canonique ;
- identifiant canonique public validé ;
- Game / DLC ;
- provider refs ;
- aliases ;
- relations prévues par la spec ;
- provenance et confiance ;
- redirects représentables au niveau modèle/schéma ;
- version de schéma et version de catalogue ;
- catalogue vide initial offline ;
- lien nullable depuis le jeu local ;
- intégration startup sans réseau ;
- migration transactionnelle ;
- non-régression du scan/library/session existant.

Différé conformément à la spec :
- resolver ;
- provisoires ;
- notifications ;
- service central ;
- sync ;
- ingestion externe ;
- relations avancées actives ;
- Epic/GOG production.

## 2. Placeholder scan

Le plan ne contient volontairement aucun marqueur de travail incomplet, aucune étape laissée à compléter, aucune méthode ou type sans définition dans le périmètre Phase 1.

Les éléments explicitement différés sont listés comme hors périmètre et non comme placeholders d’implémentation.

## 3. Cohérence des types

Vérifié :
- `CatalogContentId` est utilisé par `CatalogContent`, `CatalogProviderRef`, `CatalogAlias`, `CatalogContentRelation` et `LogicalGame`.
- `PlaySteadPublicId` est utilisé par `CatalogContent` et par les recherches du store.
- `CatalogProviderKind` est distinct de `Library.ProviderKind`.
- `CatalogDatabaseOptions` alimente l’initializer et le store Data.
- `ICanonicalCatalogStore` ne dépend que des types Core.
- `SqliteCanonicalCatalogStore` implémente exactement les méthodes du contrat.
- le bootstrap enregistre les mêmes types que ceux consommés par le nouveau constructeur de `LocalStartupPipeline`.

## 4. Risques identifiés

1. **SQLite cleanup flake préexistant**
   Ne pas confondre un `IOException` de suppression de DB dans `Dispose()` avec une régression catalogue. Toujours confirmer le test concerné isolément.

2. **DI constructor selection**
   Le constructeur 7 paramètres de `LocalStartupPipeline` doit être résolvable en production. Les anciens constructeurs restent publics pour préserver les tests et usages directs existants.

3. **Deux espaces d’identité provider**
   `CatalogProviderKind` et `Library.ProviderKind` sont intentionnellement distincts. Aucun cast implicite entre les deux ne doit être ajouté en Phase 1.

4. **Pas de fausse donnée canonique**
   Ne jamais backfiller les jeux Steam actuels avec de faux `PlaySteadGameId`. Tous les liens locaux existants restent `NULL` jusqu’à une future résolution officielle.

5. **Pas de FK cross-database**
   `playstead.db.games.canonical_content_id` n’a volontairement aucune FK SQLite vers `catalog.db`.

---

# Critère de fin

La Phase 1 est terminée lorsque PlayStead peut démarrer avec deux bases locales distinctes :

```text
playstead.db
→ comportement actuel inchangé
→ GameId local
→ CanonicalContentId nullable

catalog.db
→ schéma canonique v1
→ catalogue version 0
→ store lecture seule
→ aucune donnée personnelle
→ aucune dépendance réseau
```

et que toute la solution compile sans warning, les gates ciblés passent, et aucune fonctionnalité des phases suivantes n’est activée.
