# Game Detail Foundation & Product Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Every task is executed RED → GREEN → review → commit.

**Goal:** Transformer la fiche jeu actuelle en une page produit extensible, lisible et responsive en utilisant uniquement les données locales déjà disponibles.

**Architecture:** `GameDetailView` reste la composition root de la fiche. `GameDetailViewModel` expose les données locales, l’état de disponibilité des modules et les commandes existantes; il ne lit aucune base directement. Les zones V1 sont composées avec `GameArtwork`, `PlaySplitButton`, `SectionHeader`, `StatusBadge` et `EmptyState`; aucun second design system ni neuf contrôles spécialisés ne sont introduits.

**Tech Stack:** C#/.NET 10, WPF, CommunityToolkit.Mvvm, xUnit, XAML contract tests, SQLite stores déjà enregistrés par le composition root.

**Spec:** `docs/superpowers/specs/2026-09-17-playstead-game-detail-foundation-product-layout-spec.md` n’existe pas dans le dépôt; le présent plan reprend exclusivement le cahier produit fourni pour ce chantier et les contrats réellement présents dans le code.

## Global Constraints

- Aucune donnée fictive, aucun placeholder permanent et aucun bouton pour une fonction non implémentée.
- Aucune nouvelle source réseau, aucun provider externe et aucun accès SQLite dans la View.
- `GameDetailViewModel` consomme les projections locales existantes et conserve les constructeurs publics actuels.
- `GameArtwork` reste la source unique du rendu cover/fallback local; aucun nouveau système d’images n’est créé.
- `PlaySplitButton` reste la source unique de l’action Jouer et du choix d’installation.
- Les modules sans données exposent `HasData == false` et sont retirés du layout par `Visibility=Collapsed`.
- Le démarrage runtime est actuellement opérationnel en Release et a été vérifié manuellement sur Home, Library, Game Detail, Sessions, Reports, Settings et Notifications. L’incident intermittent de sauvegarde pré-migration dans `DatabaseInitializer.InitializeAsync` reste une dette technique à investiguer séparément; il ne constitue ni un prérequis ni un bloqueur de ce chantier.
- Chaque tâche produit un cycle de tests indépendant et un commit dédié; aucun commit ne mélange une tâche avec la suivante.

## Audit initial du code réel

### Navigation et vues actuelles

- `src/PlayStead.UI/MainWindow.xaml.cs` traite `AppRoute.GameDetail`, retrouve le `GameId` dans `LibraryViewModel.Items`, construit `GameDetailViewModel`, puis place `GameDetailView` dans `MainContent`.
- `src/PlayStead.UI/Library/LibraryView.xaml` expose le `GameQuickPanel`, le bouton `OpenGameDetailButton` et `PlaySplitButton`.
- `src/PlayStead.UI/Library/LibraryView.xaml.cs` lève `GameDetailsRequested`; aucune logique métier de fiche n’est dans la View.
- `src/PlayStead.UI/Navigation/AppRoute.cs` contient `GameDetail`; `NavigationService` conserve le paramètre `GameId`.

### Données réellement disponibles

| Donnée | Source réelle | Disponible | Fiabilité | Utilisable V1 |
|---|---|---:|---|---:|
| Nom du jeu | `LogicalGame.Title` projeté par `LibraryItemViewModel.Title` | Oui | locale, persistée | Oui |
| Source/plateforme | `GameInstallation.Provider`, `ProviderLabel` | Oui | locale, provider connu | Oui |
| Cover | `LibraryItemViewModel.CoverPath`, alimenté par `IGameMediaResolver` et cache fichier | Oui, conditionnelle | locale/cache; absente si non résolue | Oui avec fallback |
| Backdrop | aucune propriété dans `LibraryItemViewModel` ou `GameDetailViewModel`; `HomeViewModel.HeroPath` est limité à Home | Non pour la fiche | aucune projection locale | Non |
| Taille installée | `GameInstallation.InstalledSizeBytes` → `InstalledSizeLabel` | Oui, conditionnelle | scan local | Oui |
| Chemin d’installation | `GameInstallation.InstallPath` → `InstallPath` | Oui, peut être vide/invalide | scan local | Oui avec état absent |
| Présence/statut installation | `GameInstallation.IsPresent`, `SteamUpdateState`, labels Library | Oui | locale/provider | Oui |
| État de session | `LibraryItemViewModel.IsSessionActive`, `SessionStatusLabel` | Oui | `SessionMonitor` local | Oui |
| Dernière activité | `GameQuickPanelViewModel.LoadSessionSummaryAsync` via `ISessionStore` | Oui, conditionnelle | sessions persistées/corrections appliquées | Oui |
| Dernière session | même projection QuickPanel | Oui, conditionnelle | sessions persistées | Oui |
| Temps total | même projection QuickPanel (`TotalPlayTimeLabel`) | Oui, conditionnelle | sessions terminées + corrections | Oui |
| Nombre de sessions | même projection QuickPanel (`SessionCountLabel`) | Oui, conditionnelle | sessions persistées | Oui |
| Session active | `SessionViewModel.ActiveSessions`/`SessionMonitor.LatestSnapshot` | Oui, conditionnelle | observation locale | Oui |
| Identité canonique | `LogicalGame.CanonicalContentId` en Core/Data | Oui dans la persistance | rattachement confirmé, non enrichi dans l’UI | Non en V1; ne pas afficher l’ID |
| Métadonnées canoniques | `ICanonicalCatalogStore` expose titre/date/développeur/éditeur, mais aucun ViewModel UI ne les charge | Oui dans Data, non projetée | dépend du contenu catalog.db | Non en V1 |
| Genre | aucun champ dans `CatalogContent` | Non | aucune | Non |
| Développeur/éditeur/date | champs `CatalogContent` présents, aucun raccord fiche | Partiel | store local, non affiché | Non en V1 |
| Langue/build/version | aucun champ UI/Core correspondant | Non | aucune | Non |
| Captures | aucun modèle/cache/UI | Non | aucune | Non |
| Technologies graphiques | aucune source locale | Non | aucune | Non |
| Succès/news/communauté/mods/saves/config/logs | aucun contrat de données local pour la fiche | Non | aucune | Non |

### Composants réutilisables

- `src/PlayStead.UI/Controls/GameArtwork.xaml(.cs)` fournit `SourcePath`, `Title`, `ProviderLabel`, `HasArtwork`, `Stretch=Uniform` et un fallback local.
- `src/PlayStead.UI/Controls/PlaySplitButton.xaml(.cs)` fournit Jouer, `CanPlay` et le choix des installations.
- `SectionHeader`, `StatusBadge`, `EmptyState` et les ressources `PlayStead.Brush.*`, `PlayStead.Spacing.*`, `PlayStead.Radius.*` sont déjà utilisés par Home, Library et Sessions.
- `GameQuickPanelViewModel` calcule déjà l’activité et applique `SessionCorrectionPolicy`; la fiche doit réutiliser cette projection au lieu de recalculer les sessions.

## Composition V1 retenue

`GameDetailView` reste un seul UserControl avec quatre zones réelles:

1. **Hero**: `GameArtwork` en tête, titre, fournisseur, présence installation, activité condensée et `PlaySplitButton`.
2. **Ton jeu / activité**: les valeurs de `GameQuickPanelViewModel` (`LastActivityLabel`, `LastSessionDateLabel`, `LastSessionDurationLabel`, `TotalPlayTimeLabel`, `SessionCountLabel`) lorsqu’un résumé est disponible.
3. **Installation**: chemin, taille, provider et état présent/absent; l’action dossier n’est ajoutée que si une commande réelle existe déjà.
4. **Informations locales**: session active et état Steam, uniquement si la propriété correspondante a une valeur.

Les modules canonical metadata, screenshots, technologies, communauté et news restent absents du visual tree V1. Ils ne sont pas représentés par des cartes vides.

## Tasks

### Task 1: Game detail data/view-model foundation

**Files:**
- Modify: `src/PlayStead.UI/Library/GameDetailViewModel.cs`
- Modify: `src/PlayStead.UI/MainWindow.xaml.cs` (création du modèle au routeur GameDetail)
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailViewModelTests.cs`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailFoundationTests.cs`

**Interfaces:**
- Preserve: `GameDetailViewModel(LibraryItemViewModel game)` and `GameDetailViewModel(LibraryItemViewModel game, GameLaunchViewModel? launch)`.
- Add: `GameDetailViewModel(LibraryItemViewModel game, GameLaunchViewModel? launch, GameQuickPanelViewModel? activity)`.
- Produce: `LibraryItemViewModel Game`, `GameLaunchViewModel? Launch`, `GameQuickPanelViewModel? Activity`, `bool HasCover`, `string? CoverPath`, `bool HasInstallPath`, `bool HasInstalledSize`, `bool HasSteamStatus`, `Task LoadAsync(CancellationToken cancellationToken)`.

- [ ] RED: tester writes assertions for preservation of the existing constructors, projection of `Game`, cover flags, installation flags and `LoadAsync` delegation.
- [ ] Run: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailFoundationTests|FullyQualifiedName~GameDetailViewModelTests"`.
- [ ] GREEN: add only the properties/constructor/delegation above; keep existing labels and launch model unchanged.
- [ ] Regression: run `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetail|FullyQualifiedName~LibraryQuickPanel"`.
- [ ] Commit: `feat(library): add game detail presentation foundation`.

### Task 2: Hero and primary action

**Files:**
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml`
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml.cs` only for view-loaded delegation to `GameDetailViewModel.LoadAsync`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailHeroContractTests.cs`

**Interfaces:**
- Consume: `GameDetailViewModel.CoverPath`, `HasCover`, `Title`, `ProviderLabel`, `Launch`.
- Produce: a hero binding using `controls:GameArtwork`; one `controls:PlaySplitButton` bound to `Launch`; no remote URI.

- [ ] RED: assert the XAML contains `GameArtwork`, bindings for `CoverPath`, `HasCover`, `Title`, `ProviderLabel`, and one `PlaySplitButton` bound to `Launch`.
- [ ] Run: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailHeroContractTests"` and confirm failure only for the missing hero contract.
- [ ] GREEN: compose the hero with existing controls, dynamic PlayStead resources, a readable overlay/fallback and the existing Jouer action; do not add an image resolver.
- [ ] Verify: same command must pass; then run `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetail|FullyQualifiedName~Launching"`.
- [ ] Commit: `feat(library): add game detail hero`.

### Task 3: Activity and recent-session summary

**Files:**
- Modify: `src/PlayStead.UI/MainWindow.xaml.cs` (construct the activity projection with existing session stores/policy)
- Modify: `src/PlayStead.UI/Library/GameDetailViewModel.cs`
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailActivityTests.cs`

**Interfaces:**
- Consume: `ISessionStore`, `ISessionCorrectionStore`, `SessionCorrectionPolicy`, and existing `GameQuickPanelViewModel.LoadSessionSummaryAsync`.
- Produce: `GameDetailViewModel.Activity` and activity bindings for `HasSessionHistory`, `LastActivityLabel`, `LastSessionDateLabel`, `LastSessionDurationLabel`, `TotalPlayTimeLabel`, `SessionCountLabel`.

- [ ] RED: assert that the detail route supplies an activity projection and that the XAML hides the activity module when `HasSessionHistory` is false.
- [ ] Run: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailActivityTests"`.
- [ ] GREEN: construct the existing QuickPanel projection for the detail page, call `GameDetailViewModel.LoadAsync` from the view Loaded event, and bind only its existing labels.
- [ ] Verify: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailActivityTests|FullyQualifiedName~GameQuickPanelViewModelTests|FullyQualifiedName~SessionHistory"`.
- [ ] Commit: `feat(library): show game activity on detail`.

### Task 4: Installation and local information modules

**Files:**
- Modify: `src/PlayStead.UI/Library/GameDetailViewModel.cs`
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailInstallationTests.cs`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailViewContractTests.cs`

**Interfaces:**
- Consume: `InstallPath`, `InstalledSizeLabel`, `ProviderLabel`, `SteamStatusLabel`, `SessionStatusLabel`, `HasInstallPath`, `HasInstalledSize`, `HasSteamStatus`.
- Produce: conditional installation/status panels; absent path, size and Steam state collapse independently.

- [ ] RED: add tests for present installation, missing size, absent path, missing Steam state and active-session label.
- [ ] Run: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailInstallationTests|FullyQualifiedName~GameDetailViewContractTests"`.
- [ ] GREEN: add bindings and visibility triggers only; do not display `CanonicalContentId` or fabricate values.
- [ ] Verify: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetail|FullyQualifiedName~LibraryItemPresentation"`.
- [ ] Commit: `feat(library): add detail installation and local info panels`.

### Task 5: Responsive/adaptive detail layout

**Files:**
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml`
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml.cs`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailResponsiveContractTests.cs`

**Interfaces:**
- Consume: `FrameworkElement.SizeChanged` and the existing `LibraryView` layout pattern.
- Produce: named `DetailColumns` with two columns at width `>= 1100` device-independent pixels and one column below `1100`; hero always spans the full width; no business state is changed by resizing.

- [ ] RED: assert the named two-column layout, the `SizeChanged` handler, the `1100` threshold and one-column fallback contract.
- [ ] Run: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailResponsiveContractTests"`.
- [ ] GREEN: implement only layout column changes in code-behind and keep all data/commands in the ViewModel.
- [ ] Verify: same test plus `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~LibraryResponsiveGridTests|FullyQualifiedName~GameDetailResponsiveContractTests"`.
- [ ] Commit: `feat(library): make game detail layout responsive`.

### Task 6: Visual polish and component reuse

**Files:**
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml`
- Test: `tests/PlayStead.UI.Tests/Library/GameDetailVisualContractTests.cs`

**Interfaces:**
- Consume: existing `PlayStead.Brush.*`, `PlayStead.Spacing.*`, `PlayStead.Radius.*`, `PlayStead.Font.Body`, `SectionHeader`, `StatusBadge`, `EmptyState`, `GameArtwork`, `PlaySplitButton`.
- Produce: consistent surface/border/accent hierarchy, no hard-coded alternate palette, no empty future cards, no internal IDs.

- [ ] RED: assert resource-based surfaces/typography, exactly one primary Jouer action, and absence of forbidden network URIs, placeholder cards and raw internal IDs.
- [ ] Run: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailVisualContractTests"`.
- [ ] GREEN: adjust only XAML composition and resource usage; preserve Task 2–5 behavior.
- [ ] Verify: `dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetail|FullyQualifiedName~Home|FullyQualifiedName~Sessions"`.
- [ ] Commit: `refactor(library): polish game detail product layout`.

### Task 7: Runtime acceptance gate

**Files:**
- No production file changes are permitted by this gate.
- Evidence: screenshots and notes stored outside the repository, under `D:\Dev\PlayStead\02_RAPPORTS\PLAYSTEAD_GAME_DETAIL_FOUNDATION\`.

**Interfaces:**
- Consume: the completed `GameDetailView`, `MainWindow` route, existing local database/media/session state and `GameLaunchViewModel`.
- Produce: a manual acceptance record with one result per scenario.

- [ ] Build: `dotnet build ".\\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror` and require `0 warning / 0 error`.
- [ ] Launch: run `src\\PlayStead.UI\\bin\\Release\\net10.0-windows\\PlayStead.UI.exe` from the authoritative worktree and confirm that startup is operational; record any recurrence of the intermittent pre-migration backup incident separately without changing this scope.
- [ ] Scenario A: open Library, select a game with cached cover and installation, open its detail, verify hero, Jouer, installation and status.
- [ ] Scenario B: select a game with no stored size or Steam status, verify those modules collapse without `—` flood or empty cards.
- [ ] Scenario C: select a game with session history, verify activity totals, last session and count match the existing QuickPanel projection.
- [ ] Scenario D: select a game without session history, verify the activity module is absent.
- [ ] Scenario E: resize to `>=1100` and `<1100` device-independent pixels, then maximize; verify two-column/one-column layout and readable hero.
- [ ] Scenario F: navigate back to Library and Home, reopen another game, and verify selection/navigation remain correct.
- [ ] Scenario G: inspect Jouer without activating it; no game process may be launched by the acceptance procedure.
- [ ] Gate: `RUNTIME_GREEN=True`, `VISUAL_GREEN=True`, `PRODUCT_COHERENCE_GREEN=True` only if every scenario is observed and recorded. A startup failure leaves Task 7 RED.
- [ ] Commit: `test(library): accept game detail product layout` only after all gates are green.

## Final gate

Run:

```powershell
dotnet test ".\\tests\\PlayStead.UI.Tests\\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1 --filter "FullyQualifiedName~GameDetail|FullyQualifiedName~Library|FullyQualifiedName~Home|FullyQualifiedName~Sessions"
dotnet build ".\\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
git diff --check
git status --short
```

Acceptance is closed only when:

- all targeted tests pass;
- solution build reports `0 warning / 0 error`;
- no new network/provider dependency exists;
- no empty future module is rendered;
- normal and narrow layouts are manually verified;
- Release startup is operational during the final acceptance run;
- `RUNTIME_GREEN=True`, `VISUAL_GREEN=True`, and `PRODUCT_COHERENCE_GREEN=True` are all recorded.
