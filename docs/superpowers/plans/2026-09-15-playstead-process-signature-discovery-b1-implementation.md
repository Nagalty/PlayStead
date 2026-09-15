# PlayStead Process Signature Discovery B1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer les contrats, l'inventaire filesystem Windows et une politique pure de découverte, sans activer ni persister une signature.

**Architecture:** Les données immuables et la décision vivent dans `PlayStead.Core.Sessions.Discovery`. Platform produit un inventaire déterministe et explicite ses défauts de qualité. Des résumés synthétiques d'épisodes permettent de tester la politique ; leur collecte runtime et leur persistance ne font pas partie de B1.

**Tech Stack:** C# / .NET 10, Core `net10.0`, Platform `net10.0-windows`, xUnit, `System.IO`, PowerShell. Aucun package ajouté.

**Spec:** [design approuvé](../specs/2026-09-15-playstead-process-signature-discovery-design.md).

## Global Constraints

- Workspace exclusif : `D:\Dev\PlayStead\worktrees\0.4.1-media-foundation` ; branche `feat/0.4.1-media-foundation`.
- Baseline documentaire : `e7ae3c1c44fbb4bd5b2c9327fdf2dfa1834003d3` ; le commit de ce plan sera le point de départ de son exécution.
- Task 7 reste dans `stash@{0}: On feat/0.4.1-media-foundation: wip/task7-home-media-integration` ; objet initial `622b590181ed07283907190c342963489be60340`.
- **Silent learning** ; **False negative > false positive** ; aucune confirmation obligatoire, aucune UI.
- Aucun code Data/Providers/UI, aucune DB, migration, modification Library, `ProcessSignatureEntry`, matcher ou `SessionRuntime`.
- Aucun appel de capture processus, aucun exécutable de jeu lancé, aucun réseau, aucun service de fond.
- La génération d'inventaire est fournie par l'appelant, jamais créée par un compteur caché ou `Guid.NewGuid()` dans la politique. B2 gérera ses changements.
- `PromoteMain` est une proposition pure. L'acceptation atomique de la section 10.2.7 de la spec est exclusivement B2. Aucun test B1 ne prétend l'avoir exécutée.
- RED réellement exécuté avant chaque comportement GREEN. Un type absent peut donner un RED de compilation initial ; après apparition des contrats, exiger des assertions comportementales. Une erreur de test ou d'environnement n'est jamais une preuve RED.
- Release hérite de `TreatWarningsAsErrors=true` dans `Directory.Build.props`. Ne pas changer ce fichier.
- Les commandes de ce document sont à exécuter lors de l'implémentation, pas lors de la rédaction du plan.

## Inspection et décisions de structure

Inspection réalisée : tout `src/PlayStead.Core/Sessions/*.cs`, `GameId`, `InstallationId`, `GameInstallation`, `LibrarySnapshot`, `GameMediaIdentity`, tout le code Platform et les tests Sessions/Paths/Processes représentatifs. Aucun `AGENTS.md` applicable trouvé.

`GameId` et `InstallationId` sont des `readonly record struct` de namespace `PlayStead.Core.Library`. Les records Sessions sont simples ; `GameMediaIdentity` montre le pattern constructeur explicite + propriétés get-only + copie défensive. Réutiliser ce dernier pour les collections de preuves. `WindowsProcessSnapshotSource` possède des délégués injectables pour ses lectures système : appliquer ce pattern à l'inventaire, sans créer une abstraction filesystem générale. `UserDataLayout` utilise `Path.GetFullPath`, mais aucune protection de frontière/reparse point réutilisable n'existe.

**No automatic exclusions in B1.** Le repo ne fournit aucune attestation fiable qu'un fichier est uniquement un installateur/reporteur/serveur. Un nom ou substring ne suffit pas. B1.4 verrouille ce choix par des tests de politique ; aucune classe d'exclusion ni champ `IsExcluded` préparatoire. Un candidat non observé reste concurrent, même nommé `setup.exe`. C'est le sous-ensemble conservateur autorisé par la spec, avec davantage de faux négatifs assumés.

## File Structure

Tous les fichiers ci-dessous sont nouveaux, sauf les modifications explicitement indiquées entre tâches B1. Aucun `.csproj` à modifier : SDK globbing, références Core/Platform et import global xUnit existent déjà.

| Task | Production | Tests |
|---|---|---|
| B1.1 | `src/PlayStead.Core/Sessions/Discovery/` : `FileRevision.cs`, `InstallationScope.cs`, `ExecutableCandidate.cs`, `ExecutableInventory.cs`, `InventoryIssue.cs`, `SnapshotRange.cs`, `CandidateEpisodeEvidence.cs`, `LearningEpisodeSummary.cs`, `DiscoveryDecision.cs`, `DiscoveryReason.cs`, `DiscoveryEvaluation.cs` | `tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveryContractsTests.cs` |
| B1.2 | `src/PlayStead.Core/Sessions/Discovery/IExecutableInventorySource.cs` ; `src/PlayStead.Platform/Processes/Discovery/WindowsExecutablePath.cs` | `tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutablePathTests.cs` |
| B1.3 | `src/PlayStead.Platform/Processes/Discovery/WindowsExecutableInventorySource.cs` | `tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutableInventorySourceTests.cs` ; `ExecutableInventoryTestDirectory.cs` dans le même dossier |
| B1.4 | `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureDiscoveryPolicy.cs` | `tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveryNegativeExclusionTests.cs` |
| B1.5 | modifier uniquement `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureDiscoveryPolicy.cs` | `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureDiscoveryPolicyTests.cs` |
| B1.6 | renforcer uniquement `src/PlayStead.Platform/Processes/Discovery/WindowsExecutableInventorySource.cs` | `tests/PlayStead.Platform.Tests/Processes/Discovery/ExecutableInventoryPolicyIntegrationTests.cs` |

Les enums `InventoryCompleteness`, `EpisodeQuality`, `DiscoveryDecisionKind` sont placés respectivement avec `ExecutableInventory`, `LearningEpisodeSummary`, `DiscoveryDecision`, comme `SessionTransitionKind` avec `SessionTransition`. `InventoryIssueKind` accompagne `InventoryIssue`.

## Task B1.1 — Domain contracts

**Files:** créer les onze fichiers Core et `DiscoveryContractsTests.cs` listés pour B1.1 ci-dessus.

**Interfaces:** consomme uniquement les deux value objects Library et `ProcessSignatureOrigin` existants. Produit les contrats suivants dans `PlayStead.Core.Sessions.Discovery`. Les déclarations condensées ci-dessous fixent l'ordre et les types des paramètres ainsi que les propriétés PascalCase correspondantes. Les constructeurs publics effectifs emploient les mêmes noms en camelCase, comme le constructeur FileRevision montré en Step 3. Implémenter ces records avec constructeur explicite et propriétés **get-only**, afin de valider et copier les collections ; ne pas exposer de setters/init contournant les invariants.

```csharp
// using PlayStead.Core.Library;
// using PlayStead.Core.Sessions;
public sealed record FileRevision(long SizeBytes, DateTimeOffset LastWriteTimeUtc);
public sealed record InstallationScope(
    GameId GameId, InstallationId InstallationId, string RootPath,
    Guid GenerationId, bool IsPresent);
public sealed record ExecutableCandidate(
    string ExecutablePath, string ExecutableName, FileRevision Revision);

public enum InventoryCompleteness { Complete, Incomplete }
public enum InventoryIssueKind
{
    MissingRoot, InvalidRoot, AccessDenied, IoFailure,
    ReparsePoint, EscapedRoot, RevisionChanged
}
public sealed record InventoryIssue(string Path, InventoryIssueKind Kind);
public sealed record ExecutableInventory(
    InstallationScope Scope, InventoryCompleteness Completeness,
    IReadOnlyList<ExecutableCandidate> Candidates,
    IReadOnlyList<InventoryIssue> Issues);

public sealed record SnapshotRange(long First, long Last);
public sealed record CandidateEpisodeEvidence(
    string ExecutablePath, FileRevision? Revision,
    bool HasReliablePath, bool HasReliableIdentity,
    IReadOnlyList<SnapshotRange> PresenceRanges);

[Flags]
public enum EpisodeQuality
{
    Complete = 0, Partial = 1, CaptureGap = 2, UnknownProcessIdentity = 4
}
public sealed record LearningEpisodeSummary(
    Guid EpisodeId, long SequenceNumber, InstallationScope Scope,
    int PolicyVersion, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc,
    long FirstSnapshot, long LastSnapshot, EpisodeQuality Quality,
    IReadOnlyList<CandidateEpisodeEvidence> Candidates);

public enum DiscoveryDecisionKind { PromoteMain, InsufficientEvidence, Ambiguous }
public sealed record DiscoveryDecision(
    DiscoveryDecisionKind Kind, ExecutableCandidate? Main,
    IReadOnlyList<DiscoveryReason> Reasons);
public sealed record DiscoveryEvaluation(
    ExecutableInventory Inventory,
    IReadOnlyList<LearningEpisodeSummary> Episodes,
    bool HasAmbiguousInstallation,
    ProcessSignatureOrigin? ExistingSignatureOrigin);
```

`DiscoveryReason.cs` contient exactement :

```csharp
public enum DiscoveryReason
{
    RepeatedQualifiedEpisodes, NoCandidates, InstallationAbsent,
    IncompleteInventory, UnreliablePath, PartialEpisode, CaptureGap,
    UnknownProcessIdentity, UnobservedCompetitor, EquivalentCandidates,
    LateCompetitor, ReappearingCompetitor, ConflictingEpisodes,
    AmbiguousInstallation, RevisionChanged, ProtectedSignature,
    AwaitingIndependentEpisode, ScopeChanged, GenerationChanged,
    PolicyVersionChanged, DuplicateEpisode, OverlappingEpisodes,
    InsufficientPresence
}
```

**Invariants :**

| Type / donnée | Validation exacte |
|---|---|
| Toute référence/collection requise | `ArgumentNullException.ThrowIfNull` ; aucun élément null ; copie `Array.AsReadOnly(source.ToArray())` |
| Toute chaîne requise | `ArgumentException.ThrowIfNullOrWhiteSpace` ; préserver le texte, pas de trim silencieux de chemin |
| FileRevision | taille >= 0 ; normaliser date avec `ToUniversalTime()` ; égalité par valeurs |
| InstallationScope | `GameId.Value`, `InstallationId.Value`, génération non vides ; bool présence false autorisé ; Core ne canonise pas le chemin |
| ExecutableCandidate | révision obligatoire ; Core ne consulte ni existence ni format Windows |
| Inventory | enum valide ; Complete implique Issues vide, Incomplete implique au moins une Issue ; chemins candidats sans doublon OrdinalIgnoreCase ; ordre conservé |
| SnapshotRange | First >= 0, Last >= First ; intervalle inclusif |
| Candidate evidence | révision null et fiabilité false sont des preuves insuffisantes autorisées, pas des erreurs constructeur ; intervalles strictement ordonnés, disjoints et non adjacents (intervalles adjacents doivent être fusionnés par le producteur) |
| Episode | ID non vide, séquence > 0, version > 0, FirstSnapshot >= 0, LastSnapshot >= FirstSnapshot, StartedAtUtc <= EndedAtUtc ; UTC normalisé ; flags connus ; chaque intervalle dans les bornes ; un élément par chemin |
| Decision | enum valide ; Reasons non vide, distinctes ; Main non null si et seulement si PromoteMain ; tous les refus ont Main null |
| Evaluation | données requises non null ; origine nullable ou enum existant valide ; ordre des épisodes conservé, **aucun tri ni dédoublonnage** : la politique doit pouvoir refuser les répétitions/ruptures |

Les bornes sont les premières/dernières captures du résumé, y compris les captures d'absence. Ce sont des données finies de test, pas un buffer live ni une machine d'observation. La fiabilité PID+démarrage est déjà évaluée par le futur producteur B2 ; B1 ne crée pas de `ProcessObservation`.

- [ ] **Step 1 RED** — écrire `DiscoveryContractsTests.cs`, environ 18 cas fact/theory : valeurs conservées ; identité FileRevision ; rejet taille négative/IDs vides/versions invalides/chaînes vides/nulls ; copies défensives imbriquées ; erreur Complete avec issues ; erreur Incomplete sans issue ; refus avec Main interdit ; promotion sans Main interdite ; doublons chemins ; intervalles invalides, chevauchants/adjacents et hors bornes ; flags inconnus ; ordre des épisodes inchangé. Exemple complet :

```csharp
[Fact]
public void Revision_rejects_negative_size()
{
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        new FileRevision(-1, DateTimeOffset.UnixEpoch));
}

[Fact]
public void Evidence_keeps_a_defensive_copy_of_ranges()
{
    var ranges = new List<SnapshotRange> { new(2, 5) };
    var evidence = new CandidateEpisodeEvidence(
        @"C:\Games\Example\game.exe", new FileRevision(1, DateTimeOffset.UnixEpoch),
        true, true, ranges);
    ranges.Clear();
    Assert.Equal(new SnapshotRange(2, 5), Assert.Single(evidence.PresenceRanges));
    Assert.Throws<NotSupportedException>(() =>
        ((IList<SnapshotRange>)evidence.PresenceRanges).Clear());
}
```

- [ ] **Step 2 run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~DiscoveryContractsTests"
```

Attendu : échec sur les contrats absents ; noter les symboles exacts, puis obtenir les assertions comportementales sur les invariants au fur et à mesure de leur création.

- [ ] **Step 3 GREEN minimal** — implémenter uniquement les contrats et gardes ci-dessus ; aucun calcul de promotion. Exemple de style réel à appliquer :

```csharp
public sealed record FileRevision
{
    public FileRevision(long sizeBytes, DateTimeOffset lastWriteTimeUtc)
    {
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        SizeBytes = sizeBytes;
        LastWriteTimeUtc = lastWriteTimeUtc.ToUniversalTime();
    }
    public long SizeBytes { get; }
    public DateTimeOffset LastWriteTimeUtc { get; }
}
```

- [ ] **Step 4 run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~DiscoveryContractsTests"
```

Attendu : tous les cas de contrat PASS ; 0 warning / 0 erreur.

- [ ] **Step 5 regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release
```

Attendu : toute la suite Core PASS, aucun test historique modifié.

- [ ] **Step 6 diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

Attendu : seuls les fichiers B1.1 ; examiner aussi les nouveaux fichiers non suivis, absents de `git diff --stat`.

- [ ] **Step 7 commit** — après review des contrats et résultats :

```powershell
git add -- src/PlayStead.Core/Sessions/Discovery/FileRevision.cs src/PlayStead.Core/Sessions/Discovery/InstallationScope.cs src/PlayStead.Core/Sessions/Discovery/ExecutableCandidate.cs src/PlayStead.Core/Sessions/Discovery/ExecutableInventory.cs src/PlayStead.Core/Sessions/Discovery/InventoryIssue.cs src/PlayStead.Core/Sessions/Discovery/SnapshotRange.cs src/PlayStead.Core/Sessions/Discovery/CandidateEpisodeEvidence.cs src/PlayStead.Core/Sessions/Discovery/LearningEpisodeSummary.cs src/PlayStead.Core/Sessions/Discovery/DiscoveryDecision.cs src/PlayStead.Core/Sessions/Discovery/DiscoveryReason.cs src/PlayStead.Core/Sessions/Discovery/DiscoveryEvaluation.cs tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveryContractsTests.cs
git diff --cached --check
git diff --cached --name-status
git commit -m "feat(sessions): add discovery evidence contracts"
```

Attendu : 12 fichiers, aucun consommateur runtime.

## Task B1.2 — Path / filesystem inventory contract

**Files:** créer `src/PlayStead.Core/Sessions/Discovery/IExecutableInventorySource.cs`, `src/PlayStead.Platform/Processes/Discovery/WindowsExecutablePath.cs`, `tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutablePathTests.cs`.

**Interfaces:**

```csharp
// PlayStead.Core.Sessions.Discovery
public interface IExecutableInventorySource
{
    Task<ExecutableInventory> InventoryAsync(
        InstallationScope scope, CancellationToken cancellationToken);
}

// PlayStead.Platform.Processes.Discovery
public static class WindowsExecutablePath
{
    public static string NormalizeRoot(string rootPath);
    public static bool IsStrictlyUnderRoot(string canonicalRoot, string candidatePath);
}
```

Entrée inventory : scope dédié B1, pas `GameInstallation`. Sortie : mêmes IDs, génération et présence, RootPath normalisé lorsque possible. Racine syntaxiquement invalide : sortie Incomplete/InvalidRoot avec scope d'entrée conservé, aucun accès disque. Scope absent : Incomplete/MissingRoot. Scope null est une erreur d'appel ; cancellation prioritaire après ce guard, avant tout I/O. Aucun résultat partiel retourné sur cancellation.

Le helper est **lexical**, pas une attestation filesystem. Accepter un chemin de lecteur pleinement qualifié tel que `C:\Games\Example` ; normaliser `/` vers `\`, `Path.GetFullPath`, puis retirer les séparateurs finaux sauf la racine du volume. Refuser chemins relatifs, drive-relative `C:Games`, UNC/device paths, segments `.`/`..`, wildcard, NUL, caractères de nom Windows invalides (dont `:` hors préfixe de lecteur) et composants terminés par point/espace. Ces refus conservateurs évitent des alias non vérifiés ; aucune normalisation ne transforme une traversée en preuve. La vérification des attributs de la racine et de tous ses ancêtres appartient à B1.3.

`NormalizeRoot` lève ArgumentException pour ces entrées invalides. `IsStrictlyUnderRoot` retourne false si candidat invalide ou égal à la racine ; utilise un préfixe terminé par séparateur et OrdinalIgnoreCase, donc FooBar ne passe pas pour Foo. Aucun `Directory.Exists` / `File.Exists` dans le helper. Platform refusera tous les reparse points en B1, y compris ceux dont la cible serait interne : pas de résolution implicite ni de traversée de jonction.

- [ ] **Step 1 RED** — dans `WindowsExecutablePathTests.cs`, environ 12 cas, théories pour les refus :

```csharp
[Theory]
[InlineData(@"C:\Games\Foo\game.exe", true)]
[InlineData(@"c:\games\foo\bin\GAME.EXE", true)]
[InlineData(@"C:\Games\FooBar\game.exe", false)]
[InlineData(@"C:\Games\Foo\..\FooBar\game.exe", false)]
[InlineData(@"C:\Games\Foo", false)]
public void Boundary_is_a_directory_boundary(string candidate, bool expected)
{
    Assert.Equal(expected,
        WindowsExecutablePath.IsStrictlyUnderRoot(@"C:\Games\Foo", candidate));
}

[Fact]
public void Root_normalizes_separator_without_reading_the_filesystem()
{
    Assert.Equal(@"C:\Games\Foo",
        WindowsExecutablePath.NormalizeRoot("C:/Games/Foo/"));
}
```

Ajouter refus null/blanc, relatif, `C:Foo`, UNC, device path, segment `..`, nom avec point/espace final ; conservation racine `C:\`. Ne pas inventer de test comportemental pour une interface seule : son utilisation concrète commence en B1.3.

- [ ] **Step 2 run RED**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~WindowsExecutablePathTests"
```

Attendu : helper absent ; après création, assertions des frontières/normalisations avant leur GREEN.

- [ ] **Step 3 GREEN minimal** — validations lexicales explicites puis opérations `Path` dans Platform uniquement. Cœur de la comparaison après validation :

```csharp
var root = NormalizeRoot(canonicalRoot);
var candidate = NormalizeRoot(candidatePath);
var prefix = root.EndsWith('\\') ? root : root + "\\";
return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
```

`IsStrictlyUnderRoot` intercepte seulement les erreurs d'argument du candidat pour retourner false. Aucun catch global. Ajouter l'interface exacte, sans implémentation dummy.

- [ ] **Step 4 run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~WindowsExecutablePathTests"
```

Attendu : tous PASS.

- [ ] **Step 5 regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~DiscoveryContractsTests"
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~PlayStead.Platform.Tests.Paths|FullyQualifiedName~WindowsExecutablePathTests"
```

Attendu : Core contracts et anciens chemins Platform GREEN.

- [ ] **Step 6 diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

Attendu : trois nouveaux fichiers de B1.2 uniquement.

- [ ] **Step 7 commit**

```powershell
git add -- src/PlayStead.Core/Sessions/Discovery/IExecutableInventorySource.cs src/PlayStead.Platform/Processes/Discovery/WindowsExecutablePath.cs tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutablePathTests.cs
git diff --cached --check
git diff --cached --name-status
git commit -m "feat(platform): define bounded executable inventory paths"
```

Attendu : contrat et helper reviewables sans activation d'inventaire.

## Task B1.3 — Windows executable inventory implementation

**Files:** créer `src/PlayStead.Platform/Processes/Discovery/WindowsExecutableInventorySource.cs`, `tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutableInventorySourceTests.cs` et `tests/PlayStead.Platform.Tests/Processes/Discovery/ExecutableInventoryTestDirectory.cs`.

**Interfaces:** namespace production `PlayStead.Platform.Processes.Discovery`, tests `PlayStead.Platform.Tests.Processes.Discovery`.

```csharp
public sealed class WindowsExecutableInventorySource : IExecutableInventorySource
{
    public WindowsExecutableInventorySource();
    public WindowsExecutableInventorySource(
        Func<string, IEnumerable<string>> enumerateEntries,
        Func<string, FileAttributes> readAttributes,
        Func<string, FileRevision> readRevision);
    public Task<ExecutableInventory> InventoryAsync(
        InstallationScope scope, CancellationToken cancellationToken);
}
```

Les trois délégués sont obligatoires et validés. Le constructeur sans argument utilise `Directory.EnumerateFileSystemEntries`, `File.GetAttributes` et une méthode privée lisant taille/date. Pas de packages filesystem, de mock framework ni de nouveaux services. Les révisions ne sont pas des signatures cryptographiques.

**Contrat détaillé :**

- Annulation vérifiée avant l'énumération, avant chaque entrée, chaque lecture de métadonnées et avant retour. Les API filesystem synchrones ne sont pas interruptibles au milieu d'un appel ; ne pas promettre une latence d'annulation bornée. Retour `Task.FromResult` cohérent avec `WindowsProcessSnapshotSource`, sans `Task.Run` interne. B3 choisira son contexte d'appel hors UI.
- Vérifier les attributs de chaque composant de la racine, depuis le volume. Refuser racine ou ancêtre reparse. Parcourir manuellement les sous-dossiers ; pas de `SearchOption.AllDirectories` qui masque le point d'erreur.
- Pour chaque entrée : validation de frontière, attributs ; refuser fichier ou dossier reparse avant lecture/traversée. Filtrer seulement extension `.exe` OrdinalIgnoreCase. Tous les autres fichiers ordinaires sont ignorés ; un sous-arbre inaccessible rend l'ensemble incomplet car son contenu est inconnu.
- Lire chaque exécutable sans l'exécuter : ouvrir en lecture avec `FileShare.ReadWrite | FileShare.Delete`, lire Length et LastWriteTimeUtc, fermer. Un refus d'ouverture est un défaut de preuve même si certains attributs restent lisibles. Aucun octet n'est interprété comme code ou métadonnée PE.
- Conserver les candidats valides déjà lus si un fichier ou sous-arbre échoue. Complete + zéro candidat = répertoire entièrement parcouru et vide d'exécutables. MissingRoot, refus d'accès ou erreur ne deviennent jamais Complete + zéro candidat.
- Intercepter uniquement `UnauthorizedAccessException` (AccessDenied), `DirectoryNotFoundException`/`FileNotFoundException` (MissingRoot pour racine, IoFailure pour disparition descendante), `IOException` (IoFailure). Le helper signale les chemins invalides (InvalidRoot/EscapedRoot). Exceptions de programmation propagées ; cancellation jamais convertie en issue.
- Capturer aussi les erreurs différées `MoveNext()` de l'énumérateur. Continuer les autres branches connues après une erreur locale, sans déclarer l'inventaire complet.
- Trier candidats par ExecutablePath OrdinalIgnoreCase puis Ordinal, issues par Path dans le même ordre puis Kind ; dédupliquer les issues identiques. Des doublons/alias de chemin inattendus rendent le résultat Incomplete/InvalidRoot et ne produisent pas deux candidats. Nom = `Path.GetFileName(path)`.
- Dans cette tâche, chaque révision est lue au passage du fichier. La seconde vérification en fin d'inventaire est explicitement réservée à B1.6, pour son RED d'intégration. B1 n'est pas accepté avant cette dernière tâche.

- [ ] **Step 1 RED** — environ 18 cas filesystem/erreurs déterministes, dans `WindowsExecutableInventorySourceTests.cs` :

| Cas nommé | Arrangement | Assertion |
|---|---|---|
| Root_executable | `game.exe` racine, quelques octets | un candidat, Complete, IDs/génération conservés |
| Recursive_and_case_insensitive | `bin/Game.EXE`, fichier `.txt` | seulement l'exécutable, nom/path exacts |
| Empty_is_complete | dossier vide réel | Complete, Candidates/Issues vides |
| Missing_is_incomplete | chemin unique non créé | Incomplete/MissingRoot, aucune création de répertoire |
| File_denied_keeps_other_candidates | vrai dossier + delegate readRevision lève UnauthorizedAccessException pour un fichier | Incomplete/AccessDenied, autre candidat conservé |
| Subtree_denied | delegate enumerateEntries lève sur un enfant | Incomplete, parcours des branches accessibles |
| Deferred_enumeration_error | iterator yield un fichier puis IOException | fichier conservé, Incomplete/IoFailure |
| Root_denied | readAttributes refuse racine | Incomplete/AccessDenied |
| Canonical_result | `directory.Root.Replace('\\', '/') + "/"` | RootPath et paths normalisés, mêmes identités |
| Sibling_prefix_rejected | delegate énumère `FooBar/game.exe` depuis `Foo` | Incomplete/EscapedRoot, candidat absent |
| Traversal_rejected | entrée `Foo/../Other/game.exe` | Incomplete/EscapedRoot |
| Reparse_not_followed | attribut injecté sur dossier/fichier/ancêtre racine | Incomplete/ReparsePoint ; aucun appel sous le lien |
| Real_reparse_directory | lien de répertoire vers un sibling dans le fixture | Incomplete/ReparsePoint, aucune fuite du contenu cible |
| Cancel_before_access | token annulé, delegates espions | OperationCanceledException, zéro appel |
| Cancel_during_enumeration | delegate annule après première entrée | OperationCanceledException, pas de résultat partiel |
| Revision_values | bytes et date fixés via File.SetLastWriteTimeUtc | taille et date relues exactes, offset UTC |
| Revision_changes_between_calls | modifier bytes/date entre deux inventaires | les deux révisions diffèrent ; génération fournie conservée |
| Deterministic_order | ordre d'énumération inversé, casse variée | listes candidates/issues identiques dans l'ordre défini |

Fixture de test uniquement : créer une racine unique par `Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "Discovery", Guid.NewGuid().ToString("N"))`. Contrat exact du fichier `ExecutableInventoryTestDirectory.cs` :

```csharp
internal sealed class ExecutableInventoryTestDirectory : IDisposable
{
    public ExecutableInventoryTestDirectory();
    public string Root { get; }
    public string Write(string relativePath, byte[] bytes);
    public string CreateDirectoryLink(string relativeLink, string relativeTarget);
    public void Dispose();
}
```

Le constructeur crée uniquement Root. `Write` calcule `Path.GetFullPath(Path.Combine(Root, relativePath))`, vérifie la frontière avec le helper B1.2, crée son parent et écrit les octets, puis retourne le chemin. `CreateDirectoryLink` résout et vérifie les deux chemins sous Root, crée la cible, enregistre le chemin du lien dans une liste privée puis appelle `Directory.CreateSymbolicLink`. `Dispose` vérifie que Root est un descendant du dossier temporaire Discovery, retire chaque lien existant avec `Directory.Delete(link, recursive: false)` avant de supprimer Root récursivement. Les tests n'introduisent aucun autre lien ni cible extérieure. Fichiers `.exe` = octets de fixture, jamais lancés.

Le cas reparse réel utilise `Directory.CreateSymbolicLink(link, target)` sous cette racine ; les variantes fichier/ancêtre et le bit reparse d'une jonction sont testés via `readAttributes`, sans importer de P/Invoke. Si Windows interdit la création du lien, rapporter le prérequis d'environnement manquant : ni skip silencieux ni faux GREEN. Ne pas demander automatiquement une élévation ou modifier les ACL du système. Les refus d'accès sont injectés, comme dans les tests de `WindowsProcessSnapshotSource`, pour rester reproductibles sans toucher aux ACL utilisateur.

Exemple de test concret (using Core.Library et Core.Sessions.Discovery) :

```csharp
[Fact]
public async Task Empty_is_complete()
{
    using var directory = new ExecutableInventoryTestDirectory();
    var scope = new InstallationScope(
        GameId.New(), InstallationId.New(), directory.Root, Guid.NewGuid(), true);
    IExecutableInventorySource sut = new WindowsExecutableInventorySource();
    var result = await sut.InventoryAsync(scope, CancellationToken.None);
    Assert.Equal(InventoryCompleteness.Complete, result.Completeness);
    Assert.Empty(result.Candidates);
    Assert.Empty(result.Issues);
    Assert.Equal(scope.InstallationId, result.Scope.InstallationId);
}
```

- [ ] **Step 2 run RED**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~WindowsExecutableInventorySourceTests"
```

Attendu initial : classe absente ; ensuite RED par sous-cas énumération/qualité avant d'ajouter chaque comportement. Ne pas coder tout l'inventaire avant le premier run.

- [ ] **Step 3 GREEN minimal** — constructeur injecté et parcours explicite décrit ci-dessus. Portion de lecture privée :

```csharp
private static FileRevision ReadRevision(string path)
{
    using var stream = File.Open(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);
    var info = new FileInfo(path);
    info.Refresh();
    if (!info.Exists)
        throw new FileNotFoundException("Executable disappeared.", path);
    return new FileRevision(stream.Length,
        new DateTimeOffset(info.LastWriteTimeUtc));
}
```

Le parcours construit uniquement `ExecutableCandidate`/`InventoryIssue`, puis `ExecutableInventory`. Les captures processus, classifications, generation management et DB restent absents.

- [ ] **Step 4 run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~WindowsExecutableInventorySourceTests"
```

Attendu : tous les cas PASS, aucun skip ; sinon ne pas poursuivre le gate.

- [ ] **Step 5 regression tests**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~DiscoveryContractsTests"
```

Attendu : suites GREEN. La suite Platform historique peut capturer le processus testhost via son test existant ; B1 n'ajoute aucun test de capture ni nouvelle observation production.

- [ ] **Step 6 diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

Attendu : trois nouveaux fichiers B1.3 ; aucune modification à WindowsProcessSnapshotSource.

- [ ] **Step 7 commit**

```powershell
git add -- src/PlayStead.Platform/Processes/Discovery/WindowsExecutableInventorySource.cs tests/PlayStead.Platform.Tests/Processes/Discovery/WindowsExecutableInventorySourceTests.cs tests/PlayStead.Platform.Tests/Processes/Discovery/ExecutableInventoryTestDirectory.cs
git diff --cached --check
git diff --cached --name-status
git commit -m "feat(platform): inventory executable candidates conservatively"
```

Attendu : inventaire standalone, aucune inscription DI.

## Task B1.4 — Generic negative exclusions : verrouiller leur absence

**Files:** créer `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureDiscoveryPolicy.cs` et `tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveryNegativeExclusionTests.cs`.

**Interfaces:**

```csharp
public sealed class ProcessSignatureDiscoveryPolicy
{
    public const int CurrentPolicyVersion = 1;
    public DiscoveryDecision Evaluate(DiscoveryEvaluation evaluation);
}
```

Namespace `PlayStead.Core.Sessions.Discovery`. Aucun constructeur injecté : la classe est pure. Cette tâche livre le premier comportement de refus, pas la politique de promotion complète. Le résultat n'a aucun effet persistant. Le choix « pas d'exclusions » fait ainsi l'objet d'un RED réel, sans créer un classifier inutile qui retourne toujours false.

- [ ] **Step 1 RED** — ajouter une théorie pour `setup.exe`, `MySetupAdventure.exe`, `CrashReportClient.exe`, `server.exe`, `UEPrereqSetup_x64.exe`, `Game-Win64-Shipping.exe`. Inventaire de deux fichiers, deux épisodes où seul `game.exe` est observé ; le fichier nommé par le test n'est pas observé. Résultat obligatoire Ambiguous/UnobservedCompetitor, Main null. Exemple complet à répéter avec les six InlineData :

```csharp
[Theory]
[InlineData("setup.exe")]
[InlineData("MySetupAdventure.exe")]
[InlineData("CrashReportClient.exe")]
[InlineData("server.exe")]
[InlineData("UEPrereqSetup_x64.exe")]
[InlineData("Game-Win64-Shipping.exe")]
public void Filename_never_removes_an_unobserved_competitor(string name)
{
    var t0 = DateTimeOffset.UnixEpoch;
    var scope = new InstallationScope(GameId.New(), InstallationId.New(),
        @"C:\Games\Example", Guid.NewGuid(), true);
    var revision = new FileRevision(10, t0);
    var game = new ExecutableCandidate(@"C:\Games\Example\game.exe", "game.exe", revision);
    var other = new ExecutableCandidate(@"C:\Games\Example\" + name, name, revision);
    var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete,
        [game, other], []);
    LearningEpisodeSummary Episode(long sequence) => new(
        Guid.NewGuid(), sequence, scope, 1, t0.AddMinutes(sequence),
        t0.AddMinutes(sequence).AddSeconds(14), 0, 7, EpisodeQuality.Complete,
        [new CandidateEpisodeEvidence(game.ExecutablePath, revision, true, true,
            [new SnapshotRange(2, 5)])]);
    var result = new ProcessSignatureDiscoveryPolicy().Evaluate(
        new DiscoveryEvaluation(inventory, [Episode(1), Episode(2)], false, null));
    Assert.Equal(DiscoveryDecisionKind.Ambiguous, result.Kind);
    Assert.Contains(DiscoveryReason.UnobservedCompetitor, result.Reasons);
    Assert.Null(result.Main);
}
```

Ajouter deux refus de signatures protégées, un test null et un cas inventaire sans épisode jamais promu. Environ 10 cas. Utiliser `GameId`/`InstallationId` Library existants ; aucune constante Steam/GZW.

- [ ] **Step 2 run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~DiscoveryNegativeExclusionTests"
```

Attendu : classe politique absente. Ne pas modifier les tests historiques du matcher, qui conserve son rôle Excluded explicite.

- [ ] **Step 3 GREEN minimal** — guard null, refus d'origine protégée, aucun épisode => Insufficient/AwaitingIndependentEpisode ; inventaire incomplet => Insufficient/IncompleteInventory ; candidat absent des preuves d'un épisode => Ambiguous/UnobservedCompetitor. Autres preuves encore non traitées => Insufficient/AwaitingIndependentEpisode. Aucun filtre par nom :

```csharp
if (evaluation.ExistingSignatureOrigin is
    ProcessSignatureOrigin.Manual or ProcessSignatureOrigin.BuiltIn)
{
    return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
        null, [DiscoveryReason.ProtectedSignature]);
}
```

Ce refus par donnée fournie est testable en Core ; il ne remplace pas la protection transactionnelle B2. Ne pas inventer une branche de promotion dans cette tâche.

- [ ] **Step 4 run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~DiscoveryNegativeExclusionTests"
```

Attendu : les dix cas PASS.

- [ ] **Step 5 regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release
```

Attendu : Core GREEN ; la politique de promotion reste explicitement incomplète jusqu'à B1.5.

- [ ] **Step 6 diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

Attendu : seulement les deux fichiers B1.4.

- [ ] **Step 7 commit**

```powershell
git add -- src/PlayStead.Core/Sessions/Discovery/ProcessSignatureDiscoveryPolicy.cs tests/PlayStead.Core.Tests/Sessions/Discovery/DiscoveryNegativeExclusionTests.cs
git diff --cached --check
git diff --cached --name-status
git commit -m "feat(sessions): refuse discovery based on filename exclusions"
```

Attendu : refus autonome et testé ; aucune prétention B1_GREEN.

## Task B1.5 — Pure promotion policy

**Files:** modifier `src/PlayStead.Core/Sessions/Discovery/ProcessSignatureDiscoveryPolicy.cs` ; créer `tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureDiscoveryPolicyTests.cs`. Conserver les tests B1.4 sans les modifier.

**Interfaces:** conserver `public const int CurrentPolicyVersion = 1` et `public DiscoveryDecision Evaluate(DiscoveryEvaluation evaluation)`. Consomme les contrats B1.1, aucune nouvelle dépendance ni classe publique. Aucun `File`, `Directory`, `Path`, `Process`, horloge, RNG ou store dans la politique.

**Interprétation exacte des preuves :**

- La liste est la séquence chronologique fournie, pas les deux meilleurs épisodes sélectionnés. `SequenceNumber` est un ordinal d'épisode du scope, indépendant des indices locaux de captures ; il permet de refuser des épisodes manquants. B2 sera responsable de son attribution et de la continuité, pas B1.
- Chaque résumé inclut les captures d'absence aux extrémités. Avec FirstSnapshot=0, LastSnapshot=7 et présence [2,5], les captures 0/1 et 6/7 prouvent les deux absences. `EpisodeQuality.Complete` seul ne suffit pas : recalculer ces contraintes depuis les intervalles.
- Union des présences des candidats : `firstPositive >= FirstSnapshot + 2`, `lastPositive <= LastSnapshot - 2`. Deux captures globalement absentes entre deux présences délimiteraient deux épisodes : refuser un résumé qui les fusionne, avec PartialEpisode. Une preuve absente ou à PresenceRanges vide reste un candidat non observé. Exiger des indices sans rupture connue ; un gap de capture est signalé par EpisodeQuality, jamais traité comme absence observée. Comparer par soustractions contrôlées pour éviter les débordements de long.
- Le candidat C doit avoir au moins un intervalle de deux captures consécutives et sa dernière présence doit être la dernière capture positive de l'épisode. Plusieurs intervalles de C ne sont pas interdits par la spec ; les qualités et identités doivent rester fiables.
- Tout autre candidat doit avoir exactement un intervalle non vide [a,b] : a <= première présence de C, b < dernière présence de C ; après b, C doit avoir un intervalle contenant au moins deux captures consécutives. Une deuxième plage du compagnon prouve sa réapparition et le disqualifie comme compagnon. Aucun rôle Auxiliary n'est produit.
- Parmi tous les candidats, calculer ceux qui satisfont C et pour lesquels **tous** les autres satisfont compagnon. Un seul résultat permet un épisode qualifiant ; les autres situations sont des refus expliqués, jamais un classement par durée/taille/nom.
- Comparaisons des chemins normalisés OrdinalIgnoreCase ; vérification d'appartenance physique fournie par l'inventaire Platform complet. Core ne réinterprète pas les chemins en consultant Windows. Une preuve portant sur un chemin absent de l'inventaire n'est pas ignorée.

- [ ] **Step 1 RED** — ajouter d'abord les tests Promote, startup companion, concurrence coextensive et réapparition ; ils doivent échouer comportementalement sur la politique conservatrice B1.4, qui ne promeut pas encore. Ajouter ensuite les autres familles avant leur GREEN. Prévoir environ 45 cas, répartis en facts/theories :

| Test / variation | Résultat / raison attendue |
|---|---|
| Same_candidate_two_complete_episodes | PromoteMain, Main exact, RepeatedQualifiedEpisodes |
| Startup_companion_before_main | PromoteMain après deux épisodes |
| Startup_companion_with_main | PromoteMain après deux épisodes |
| Separate_ranges_of_main_with_valid_companion | PromoteMain si deux captures consécutives après disparition du compagnon et dernière présence finale |
| Path_case_only_difference | mêmes identités, PromoteMain |
| One_episode | Insufficient / AwaitingIndependentEpisode |
| No_episode | Insufficient / AwaitingIndependentEpisode |
| Empty_complete_inventory | Insufficient / NoCandidates |
| Absent_installation | Insufficient / InstallationAbsent |
| Partial_episode | Insufficient / PartialEpisode |
| Only_one_initial_absence | Insufficient / PartialEpisode |
| Only_one_final_absence | Insufficient / PartialEpisode |
| Two_internal_absences_cannot_merge_launches | Insufficient / PartialEpisode ; présences globales [2,3] puis [6,7] |
| Capture_gap | Insufficient / CaptureGap |
| Unknown_process_identity | Insufficient / UnknownProcessIdentity |
| Unreliable_candidate_identity | Insufficient / UnknownProcessIdentity |
| Unreliable_candidate_path | Insufficient / UnreliablePath |
| Evidence_outside_inventory | Insufficient / UnreliablePath |
| Incomplete_inventory_even_with_candidates | Insufficient / IncompleteInventory |
| Null_observed_revision | Insufficient / RevisionChanged |
| Size_changed | Insufficient / RevisionChanged |
| Write_time_changed | Insufficient / RevisionChanged |
| Main_only_one_capture | Insufficient / InsufficientPresence |
| Two_equivalent_candidates | Ambiguous / EquivalentCandidates |
| Anti_cheat_coextensive | Ambiguous / EquivalentCandidates ; utiliser des noms génériques |
| Unobserved_candidate | Ambiguous / UnobservedCompetitor |
| Late_competitor | Ambiguous / LateCompetitor |
| Companion_stays_alive | Ambiguous / EquivalentCandidates |
| Companion_reappears | Ambiguous / ReappearingCompetitor |
| Companion_only_one_absent_capture_with_main | Ambiguous / EquivalentCandidates |
| Multiple_installation_attribution | Ambiguous / AmbiguousInstallation |
| Two_qualifying_episodes_propose_different_candidates | Ambiguous / ConflictingEpisodes |
| Success_contradiction_success | pas Promote ; Insufficient/AwaitingIndependentEpisode après remise à zéro |
| Success_gap_success | pas Promote ; référence remise à zéro |
| Success_contradiction_success_success | Promote pour les deux nouveaux épisodes successifs, sans reprendre l'ancien |
| Duplicate_episode_id | Insufficient / DuplicateEpisode |
| Sequence_number_gap_or_reversal | Insufficient / CaptureGap |
| Overlapping_or_touching_episode_times | Insufficient / OverlappingEpisodes |
| Different_game_or_installation_or_root | Insufficient / ScopeChanged |
| Changed_generation | Insufficient / GenerationChanged |
| Changed_policy_version | Insufficient / PolicyVersionChanged |
| Manual_or_BuiltIn | Insufficient / ProtectedSignature, même avec bonnes preuves |
| Existing_Discovered | proposition autorisée si preuves complètes ; aucune écriture |
| Repeated_evaluation | mêmes valeurs résultat/raisons, entrées inchangées |
| Candidate_order_permuted | même résultat, aucune préférence de position |

Ces lignes définissent des cas, pas un nombre de tests imposé : relever les totaux réels incluant les théories. Les tests de `ScopeChanged`, génération et qualité doivent conserver des DTO valides et varier seulement la preuve ciblée.

Base de test à inclure dans la classe (using `PlayStead.Core.Library`, `PlayStead.Core.Sessions`, `PlayStead.Core.Sessions.Discovery`) :

```csharp
private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;
private static readonly FileRevision Revision = new(10, T0);
private static readonly InstallationScope Scope = new(
    new GameId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
    new InstallationId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
    @"C:\Games\Example", Guid.Parse("33333333-3333-3333-3333-333333333333"), true);
private static readonly ExecutableCandidate Game = new(
    @"C:\Games\Example\game.exe", "game.exe", Revision);
private static readonly ExecutableCandidate Companion = new(
    @"C:\Games\Example\companion.exe", "companion.exe", Revision);

private static LearningEpisodeSummary Episode(
    Guid id, long sequence, params CandidateEpisodeEvidence[] candidates) => new(
        id, sequence, Scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
        T0.AddMinutes(sequence), T0.AddMinutes(sequence).AddSeconds(18),
        0, 9, EpisodeQuality.Complete, candidates);

private static CandidateEpisodeEvidence Evidence(
    ExecutableCandidate candidate, params SnapshotRange[] ranges) => new(
        candidate.ExecutablePath, candidate.Revision, true, true, ranges);

[Fact]
public void Startup_companion_before_main_can_qualify_twice()
{
    var inventory = new ExecutableInventory(Scope, InventoryCompleteness.Complete,
        [Game, Companion], []);
    var first = Episode(Guid.NewGuid(), 1,
        Evidence(Game, new SnapshotRange(3, 7)),
        Evidence(Companion, new SnapshotRange(2, 4)));
    var second = Episode(Guid.NewGuid(), 2,
        Evidence(Game, new SnapshotRange(3, 7)),
        Evidence(Companion, new SnapshotRange(2, 4)));
    var result = new ProcessSignatureDiscoveryPolicy().Evaluate(
        new DiscoveryEvaluation(inventory, [first, second], false, null));
    Assert.Equal(DiscoveryDecisionKind.PromoteMain, result.Kind);
    Assert.Equal(Game, result.Main);
    Assert.Contains(DiscoveryReason.RepeatedQualifiedEpisodes, result.Reasons);
}
```

Mutations précises pour les contre-cas : compagnon [2,7] coextensif ; [4,5] tardif face à Game [3,7] ; [2,3] puis [5,5] réapparu ; [2,6] ne laisse qu'une capture de Game. Inverser Game/Companion comme survivant dans le second épisode pour ConflictingEpisodes. Pour les DTO get-only, reconstruire le résumé avec le champ changé ; pas de `with` affectant des propriétés non init.

- [ ] **Step 2 run RED**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~ProcessSignatureDiscoveryPolicyTests"
```

Attendu : compilation OK grâce à B1.1/B1.4 ; Promote et cas de classification nouvellement couverts FAIL pour comportement manquant. Les cas déjà satisfaits peuvent PASS ; ne pas fabriquer de FAIL ni altérer le GREEN B1.4.

- [ ] **Step 3 GREEN minimal** — compléter Evaluate dans cet ordre :

1. Guard null. Autorité protégée => ProtectedSignature. Attribution ambiguë => AmbiguousInstallation. Installation absente => InstallationAbsent. Inventaire incomplet => IncompleteInventory. Aucun candidat => NoCandidates. Aucun épisode => AwaitingIndependentEpisode.
2. Vérifier sans réordonner : IDs d'épisodes uniques ; séquences strictement successives ; chaque début > fin précédente. Un échec structurel retourne DuplicateEpisode, CaptureGap ou OverlappingEpisodes. Cela empêche d'omettre silencieusement un épisode du milieu.
3. Évaluer chaque épisode chronologiquement, avec une variable **locale** `reference` initialement null. Aucun état ne survit entre deux appels Evaluate. Scope/génération/version différents, qualité dégradée, chemin/révision non fiable : refus correspondant et reference = null.
4. Calculer la qualification de l'épisode depuis les intervalles selon le contrat ci-dessus. Refus de concurrence => Ambiguous et reference = null. Un seul épisode qualifiant => conserver sa proposition locale et retourner provisoirement AwaitingIndependentEpisode.
5. Un second épisode qualifiant successif proposant le même chemin/révision produit PromoteMain/RepeatedQualifiedEpisodes. Après cet accord, conserver le dernier épisode comme référence locale pour évaluer le suivant : trois bons épisodes successifs ne doivent pas retomber en attente. Deux propositions différentes produisent ConflictingEpisodes et remettent la référence à null. Une qualité faible ne permet jamais de sauter à un succès antérieur. Le résultat final est celui du dernier épisode évalué, pas une promotion trouvée plus tôt puis invalidée.
6. Construire les raisons distinctes dans l'ordre de l'enum. Dans un épisode : qualité insuffisante avant qualification ; puis UnobservedCompetitor, ReappearingCompetitor, LateCompetitor, EquivalentCandidates pour une concurrence non résolue. Les cas de plusieurs survivants ont priorité EquivalentCandidates sur LateCompetitor, afin qu'un coextensif reste identifié comme tel. Toujours Main=null en refus.

Noyau de comparaison de révision, indépendant du filesystem :

```csharp
if (evidence.Revision is null || evidence.Revision != candidate.Revision)
{
    return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
        null, [DiscoveryReason.RevisionChanged]);
}
```

La méthode privée de qualification peut retourner `(ExecutableCandidate? Candidate, DiscoveryDecision? Refusal)` ; elle ne doit pas retourner un PromoteMain public pour un seul épisode. Les seules collections temporaires sont locales à l'appel. Aucun coordinateur runtime ni machine à états des snapshots.

- [ ] **Step 4 run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~ProcessSignatureDiscoveryPolicyTests|FullyQualifiedName~DiscoveryNegativeExclusionTests"
```

Attendu : toutes les nouvelles classifications et les refus B1.4 GREEN. Répéter le cycle RED/GREEN par famille, pas une implémentation exhaustive avant les assertions.

- [ ] **Step 5 regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release
```

Attendu : toute la suite Core GREEN, aucune modification des contrats historiques Sessions.

- [ ] **Step 6 diff check**

```powershell
git diff --check
git status --short
git diff --stat
```

Attendu : politique modifiée et nouveau test B1.5 uniquement. Review spécifique des refus ambigus et de la pureté.

- [ ] **Step 7 commit**

```powershell
git add -- src/PlayStead.Core/Sessions/Discovery/ProcessSignatureDiscoveryPolicy.cs tests/PlayStead.Core.Tests/Sessions/Discovery/ProcessSignatureDiscoveryPolicyTests.cs
git diff --cached --check
git diff --cached --name-status
git commit -m "feat(sessions): evaluate repeated discovery evidence conservatively"
```

Attendu : politique complète en isolation ; aucune signature créée.

## Task B1.6 — B1 integration contracts

**Files:** créer `tests/PlayStead.Platform.Tests/Processes/Discovery/ExecutableInventoryPolicyIntegrationTests.cs` ; modifier seulement `src/PlayStead.Platform/Processes/Discovery/WindowsExecutableInventorySource.cs` pour la vérification finale décrite ci-dessous. Réutiliser le fixture `ExecutableInventoryTestDirectory.cs` sans le modifier.

**Interfaces:** chaîne exacte `IExecutableInventorySource.InventoryAsync(InstallationScope, CancellationToken)` -> `ExecutableInventory` -> `new DiscoveryEvaluation(inventory, episodes, false, null)` -> `ProcessSignatureDiscoveryPolicy.Evaluate(DiscoveryEvaluation evaluation)`. Aucun adaptateur public supplémentaire. Référence Core déjà présente dans le projet Platform.Tests.

Cette tâche ne force pas un faux RED si l'intégration nominale est déjà GREEN. Son comportement manquant réservé est la **relecture de révision avant publication de l'inventaire** : un fichier qui change pendant le parcours ne doit pas produire un inventaire complet utilisable par la politique. Cela complète le contrôle filesystem B1 ; les lectures immédiatement avant acceptation et matching restent B2/B3.

- [ ] **Step 1 RED** — environ 6 tests :

1. Inventaire réel stable d'un seul fichier + deux résumés synthétiques complets => proposition PromoteMain exacte ; aucun store.
2. Inventaire réel de deux fichiers, second non observé => Ambiguous/UnobservedCompetitor.
3. Inventaire partiel par refus d'un fichier + deux bons résumés pour l'autre => Insufficient/IncompleteInventory.
4. **Révision changée pendant le parcours** : reader injecté lit l'ancienne révision, ajoute un octet au fichier de fixture avant de retourner. Inventaire doit être Incomplete/RevisionChanged ; politique => Insufficient/IncompleteInventory, même avec des épisodes correspondant à l'ancienne révision.
5. Fichier disparu ou devenu inaccessible à la relecture finale => Incomplete/IoFailure ou AccessDenied, jamais PromoteMain.
6. Cancellation pendant cette relecture => OperationCanceledException propagée, aucun résultat utilisable.

Chaque test crée seulement des données dans un répertoire temporaire. Les résumés ne prétendent pas être capturés sur un jeu réel. Exemple du RED central, helper `TwoEpisodes` défini juste après :

```csharp
[Fact]
public async Task Changed_during_inventory_never_reaches_promotion()
{
    using var directory = new ExecutableInventoryTestDirectory();
    directory.Write("game.exe", new byte[] { 1, 2, 3 });
    var scope = new InstallationScope(GameId.New(), InstallationId.New(),
        directory.Root, Guid.NewGuid(), true);
    var reads = 0;
    var source = new WindowsExecutableInventorySource(
        Directory.EnumerateFileSystemEntries, File.GetAttributes,
        path =>
        {
            var info = new FileInfo(path);
            var revision = new FileRevision(info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc));
            if (++reads == 1)
            {
                using var append = new FileStream(path, FileMode.Append, FileAccess.Write);
                append.WriteByte(4);
            }
            return revision;
        });
    var inventory = await source.InventoryAsync(scope, CancellationToken.None);
    var candidate = Assert.Single(inventory.Candidates);
    var result = new ProcessSignatureDiscoveryPolicy().Evaluate(
        new DiscoveryEvaluation(inventory, TwoEpisodes(inventory.Scope, candidate), false, null));
    Assert.Equal(InventoryCompleteness.Incomplete, inventory.Completeness);
    Assert.Contains(inventory.Issues, x => x.Kind == InventoryIssueKind.RevisionChanged);
    Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, result.Kind);
    Assert.Null(result.Main);
}

private static IReadOnlyList<LearningEpisodeSummary> TwoEpisodes(
    InstallationScope scope, ExecutableCandidate candidate)
{
    LearningEpisodeSummary Episode(long sequence) => new(
        Guid.NewGuid(), sequence, scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
        DateTimeOffset.UnixEpoch.AddMinutes(sequence),
        DateTimeOffset.UnixEpoch.AddMinutes(sequence).AddSeconds(14),
        0, 7, EpisodeQuality.Complete,
        [new CandidateEpisodeEvidence(candidate.ExecutablePath, candidate.Revision,
            true, true, [new SnapshotRange(2, 5)])]);
    return [Episode(1), Episode(2)];
}
```

- [ ] **Step 2 run RED**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~ExecutableInventoryPolicyIntegrationTests"
```

Attendu : cas stables possiblement déjà PASS ; mutation pendant parcours FAIL car B1.3 publie encore Complete avec l'ancienne révision et la politique peut proposer PromoteMain. Les chemins/namespaces compilent. Noter le résultat réel ; ne pas casser un autre comportement pour obtenir le RED.

- [ ] **Step 3 GREEN minimal** — à la fin du parcours, revalider les attributs des répertoires traversés et des candidats (notamment reparse) puis relire les révisions candidates, avant construction du résultat. Tous ces chemins proviennent de la liste locale du parcours, aucun état persistant supplémentaire. Une différence ajoute RevisionChanged et rend Incomplete ; conserver l'ancien candidat à titre de diagnostic, sans lui substituer silencieusement une nouvelle révision non couverte par les preuves. Erreurs et cancellation suivent exactement B1.3.

```csharp
// Dans la boucle finale de validation des candidats :
cancellationToken.ThrowIfCancellationRequested();
var currentRevision = _readRevision(candidate.ExecutablePath);
if (currentRevision != candidate.Revision)
{
    issues.Add(new InventoryIssue(candidate.ExecutablePath,
        InventoryIssueKind.RevisionChanged));
}
```

Réutiliser les vérifications d'attributs et handlers locaux, sans transformer cette tâche en refactor. Pas de seconde énumération récursive obligatoire : cette passe relit les candidats/répertoires connus. Ce n'est pas un snapshot filesystem atomique ; une arrivée de fichier après son énumération reste une limite et B2/B3 doivent appliquer le lifecycle de la spec. Ne pas prétendre éliminer toute course disque.

- [ ] **Step 4 run GREEN**

```powershell
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release --filter "FullyQualifiedName~ExecutableInventoryPolicyIntegrationTests"
```

Attendu : tous les cas d'intégration PASS. Le test positif vérifie une décision seulement, jamais une ligne DB ni une session.

- [ ] **Step 5 regression tests**

```powershell
dotnet test ".\tests\PlayStead.Core.Tests\PlayStead.Core.Tests.csproj" --configuration Release
dotnet test ".\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj" --configuration Release
dotnet build ".\PlayStead.sln" --configuration Release --no-restore
```

Attendu : deux suites GREEN, 0 failed / 0 skipped pour les nouveaux tests ; build 0 warning / 0 erreur. Aucun élargissement de scope pour contourner un échec inattendu ; rapporter et suspendre le gate.

- [ ] **Step 6 diff check**

```powershell
git diff --check
git status --short
git diff --stat
git stash list
git rev-parse 'stash@{0}'
```

Attendu : deux fichiers B1.6 uniquement, stash Task 7 identique ; compléter le gate final ci-dessous avant commit.

- [ ] **Step 7 commit**

```powershell
git add -- src/PlayStead.Platform/Processes/Discovery/WindowsExecutableInventorySource.cs tests/PlayStead.Platform.Tests/Processes/Discovery/ExecutableInventoryPolicyIntegrationTests.cs
git diff --cached --check
git diff --cached --name-status
git commit -m "fix(platform): reject changed executable inventory before evaluation"
git status --short
git log -1 --oneline
git stash list
```

Attendu : worktree propre, commit limité aux deux fichiers, Task 7 intacte.

## B1 acceptance gate

Les commandes complètes de Step 5 B1.6 constituent les preuves fraîches finales ; ne pas les relancer sans modification/failure qui le justifie. Relever totaux exacts Core et Platform, nouveaux cas, warnings/errors ; ne pas inventer un total avant exécution.

- [ ] Inventaire déterministe, frontières correctes, aucun reparse suivi ; tests filesystem réels et erreurs injectées PASS, aucun exécutable de fixture lancé.
- [ ] Vide complet distinct d'incomplet ; changement de révision pendant parcours refusé.
- [ ] Politique pure : tous les cas section 10 couverts ; aucune ambiguïté connue ne produit PromoteMain ; aucun filtrage de bruit par nom.
- [ ] Pas de code Data/Providers/UI, changement SQLite, fichier DB, modèle Library ou source/test historique modifié ; aucune capture ajoutée ni runtime activé.
- [ ] Suites Core/Platform GREEN ; build global Release 0 warning / 0 erreur ; `git diff --check` et staged check propres.
- [ ] Review des différences cumulées limitée aux fichiers déclarés ; Task 7 toujours dans le même stash.

Inspection cumulative à partir de la baseline documentaire (le plan lui-même est la seule différence documentaire autorisée) :

```powershell
git diff --name-status e7ae3c1 -- src tests
git diff --check e7ae3c1
git status --short
git stash list
git rev-parse 'stash@{0}'
```

Seulement lorsque toutes ces preuves sont présentes :

```text
B1_GREEN=True
```

Ce plan, à lui seul, ne valide aucun GREEN. B1 ne doit pas annoncer `SESSION_DISCOVERY_FIXED=True`. Aucune validation runtime/visuelle B3 n'est simulée par les tests synthétiques. Le suivi réel, ses sessions et son historique restent à valider en B3.

## Report à B2 / B3

**B2 :** producteur fiable des résumés, observation/accumulation et état d'apprentissage ; ProcessSignatureEntry.ExecutablePath ; matching session par chemin ; revalidation des signatures ; persistance d'épisodes ; migration additive 006 ; acceptation conditionnelle protégeant Manual/BuiltIn. B1 ne fournit qu'une décision à accepter ou refuser sous contrôle transactionnel futur.

**B3 :** raccordement au cycle unique SessionRuntime/monitor, startup/scan/launch et éventuel LaunchIntent, lancement externe, inventaires hors UI et lifecycle, recovery/pending, gate réel d'apprentissage puis session/heartbeat/historique. Home/Task 7 et UI de correction manuelle restent hors B1/B2/B3.

## Self-review du plan

- Scope vérifié : aucun coordinateur, capture réelle nouvelle, store, migration ou service préparatoire. Les résumés/ordinal/ranges sont nécessaires à la politique pure dès B1.
- API cohérente : mêmes constructeurs, propriétés get-only et méthode Evaluate dans toutes les tâches ; snippets utilisent les types Library réels. Core ne dépend ni de Platform, ni de System.IO, ni de provider.
- Six tâches reviewables : contrats ; frontières ; inventaire ; refus des exclusions implicites ; promotion pure ; cohérence d'inventaire à la frontière d'intégration.
- RED explicite dans chacune ; B1.6 réserve une mutation filesystem réelle, sans imposer que les cas nominaux déjà fonctionnels échouent. Les tests historiques restent inchangés.
- Les douze décisions du design global restent respectées ; leurs parties persistantes/runtime sont explicitement reportées et ne sont pas revendiquées par B1.
- Exclusions : pas de règle sûre démontrée, donc aucune ; pas de version de classifier artificielle. CurrentPolicyVersion versionne réellement la politique.
- Environ 109 cas planifiés avant expansion des théories (18 + 12 + 18 + 10 + 45 + 6), sans transformer cette estimation en gate de comptage.
- Les commandes test/build ne sont pas exécutées pendant la création de ce document. Chaque futur commit comprend uniquement les fichiers de sa tâche et nécessite son GREEN et sa review.
