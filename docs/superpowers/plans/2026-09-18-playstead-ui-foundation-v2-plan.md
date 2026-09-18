# PlayStead UI Foundation v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Every task follows RED → GREEN → review → commit and remains independently usable.

**Goal:** Establish a coherent, reusable visual foundation for every PlayStead surface while preserving the validated Game Detail Hero and all domain behavior.

**Architecture:** First make the existing `PlaySteadTokens.xaml` and `PlaySteadControls.xaml` dictionaries the type-safe authority for fundamental appearance, backed by a versioned hard-coded-value baseline that cannot grow silently. Then add semantic typography, button and surface roles, introduce controls only where reusable structure or behavior warrants them (`NavigationItem`, status tone, shell game search), and migrate screens in vertical slices without a parallel design system.

**Tech Stack:** C#/.NET 10, WPF XAML, CommunityToolkit.Mvvm, xUnit, XML/XAML contract tests, STA control tests, existing `NavigationService` and `LibraryViewModel` projections.

**Spec:** The authoritative Phase 0 product brief dated 2026-09-18 is incorporated into this plan; no separate spec artifact is authorized for this task.

## Global constraints

- Preserve PlayStead's copper/orange identity and the current dark palette.
- Do not clone the NexusPlay reference pixel-for-pixel.
- Do not add network calls, providers, database migrations, fake metadata or unsupported search domains.
- Keep all existing `AppRoute` values, navigation history and keyboard accessibility.
- Keep appearance in styles/resources unless reusable structure or behavior justifies a control.
- Do not mutate source game names; uppercase is presentation-only.
- Do not redesign `PlaySplitButton` behavior.
- Core principle: **a screen chooses semantic roles; a screen does not redefine fundamental appearance.**
- Progressively eliminate hard-coded colors, arbitrary `FontSize`/`FontWeight`/`CornerRadius` values, repeated `Thickness` literals and page-local button/surface appearance.
- Literal values remain valid when they are explicit, tested structural contracts: data-specific geometry, fixed media dimensions such as the Hero cover 176x264 and its clip, and genuine responsive breakpoints.
- Never embed a `{DynamicResource ...}` inside a comma-separated `Thickness` string; recurring compound margins/padding use typed `Thickness` resources.
- Every slice must leave the application buildable, testable and manually reviewable.
- Manual WPF runtime and visual validation remains user-only.

---

## Current-state forensic audit

### Resource foundation

- `src/PlayStead.UI/App.xaml` merges `Themes/PlaySteadTokens.xaml` before `Themes/PlaySteadControls.xaml`, then owns scrollbar templates locally.
- `PlaySteadTokens.xaml` already defines six surface brushes (`Background`, `Surface`, `SurfaceRaised`, `SurfaceHover`, `SurfaceSubtle`, `SurfaceStrong`), two border brushes, four text brushes, copper normal/hover/pressed, five semantic/icon roles, six spacing steps, three radius steps and two motion durations.
- `PlaySteadControls.xaml` provides one implicit control base, implicit `Button`, `ToggleButton`, `TextBox` and `ComboBox` styles. It does not define named visual roles for buttons, text or surfaces.
- Seventeen literal UI colors remain in page/control XAML. The largest clusters are `SessionsView.xaml`, `LibraryView.xaml` and `PlaySplitButton.xaml`.
- Radius usage mixes the three tokens with literals `2`, `3`, `5`, `6`, `7`, `8`, `12` and `6,6,0,0`.

### Typography

- Current default: `Segoe UI Variable, Segoe UI` through `PlayStead.Font.Body`.
- Repo/system audit found no Geist font asset. `PlayStead.UI.csproj` contains no embedded font item.
- XAML uses 12 distinct literal sizes: `11, 12, 13, 14, 15, 16, 17, 20, 22, 24, 26, 30`.
- Explicit weights are `SemiBold` and `Bold`; normal body text relies on the WPF default.
- Page titles vary between 24 and 26, section titles between 16 and 17, and card titles between 13 and 16. These are visual conventions rather than named contracts.
- `Segoe MDL2 Assets` is already used for local glyphs. No external icon package is present.

### Buttons

Five button families exist in practice:

1. implicit generic button from `PlaySteadControls.xaml`;
2. toggle buttons for Library view mode;
3. local copper/strong actions, including Game Detail/Quick Panel play and detail actions;
4. icon-only actions such as Quick Panel close and split-button options;
5. `PlaySplitButton`, which combines a primary action and optional installation menu.

Hover/focus/disabled behavior exists in the generic base, but local primary buttons repeat backgrounds, borders, padding and weights. `PlaySplitButton` has its own disabled treatment and one literal copper-brown background. Pressed and focus behavior are therefore not authoritative across families.

### Surfaces, cards and status

- Six semantic surface brushes exist, but structural card markup is repeated across Home session rows, Library panels, Game Detail modules, Sessions, Settings and Notifications.
- Reusable structural controls already exist: `KpiCard`, `SectionHeader`, `EmptyState`, `StatusBadge`, `GameArtwork`, `GameCard`, `PlaySplitButton`.
- `StatusBadge` supports only a neutral visual. Provider, active session, Steam state and notification priority are rendered through a mix of `StatusBadge`, local `Border` markup and raw text.
- Five status roles are justified by existing tokens and real states: Neutral, Success, Warning, Danger and Info. Copper remains an accent/selection role, not a status meaning.

### Shell and navigation

- `MainWindow.xaml` owns a 72-DIP top shell with five large `Button` elements, a notification badge and a right-side notification panel.
- `ShellViewModel` exposes route commands and active flags for Home, Library, Attention and Settings. `NavigationService` owns current route, parameter and back history. Routes are `Home`, `Library`, `Attention`, `Settings`, `Sessions`, `GameDetail`, `SessionDetail`.
- The safest migration is a reusable visual `NavigationItem` wrapping one accessible button. It consumes the existing commands/active flags; `NavigationService`, route values and materialization remain untouched.
- Existing keyboard contracts protect focusability, `Ctrl+K`, Alt+Left and accessible names/tooltips for icon-only actions.

### Search

- `LibrarySearchService.Search` trims the query and performs culture-aware, case-insensitive title-prefix matching only.
- `LibraryViewModel` owns `SearchQuery`, `FilteredItems`, `IsSearchActive`, `SetSearchQuery` and `ClearSearch`; grid rows derive from filtered items.
- `LibraryView` updates on every text change and owns focus/placeholder code. There is no debounce because filtering is in-memory.
- `MainWindow` already maps `Ctrl+K` to Library navigation and `LibraryView.FocusSearch()`.
- The search algorithm is reusable for shell-level game search. Provider, genre and mod search are not supported and must not appear in V1 copy. Shell search should call the same service over `LibraryViewModel.Items`, select through `LibraryViewModel.SelectGame`, then navigate through the existing `NavigationService` path.

### Responsive behavior

- `MainWindow` has `MinWidth=960`, `MinHeight=640`; the top shell has fixed height 72 and no adaptive layout.
- Library recomputes virtualized grid columns from available width using 260-DIP cards and a 12-DIP gap; list/grid modes remain separate.
- Game Detail switches its lower modules at 1100 DIP while keeping the validated Hero outside adaptive columns.
- Home and metadata groups use `WrapPanel`; Quick Panel uses vertical scrolling. Several fixed widths remain: GameCard 220, Quick Panel 380, notification panel 360, Hero rail 520 and cover 176x264.
- UI Foundation v2 must preserve these known contracts and add shell/search overflow handling at the existing 960-DIP minimum.

### Test assessment

- Useful contracts: resource uniqueness/types, public dependency properties, navigation behavior, keyboard reachability, responsive projection, route preservation and XAML load/parse safety.
- Brittle contracts: substring assertions tied to formatting or exact local XAML nesting. New tests should parse XAML or instantiate controls on STA where practical.
- Missing coverage: named typography/button/surface roles, visual-state completeness, navigation active semantics, shell search behavior and XAML load smoke coverage for all major views.
- Pixel/screenshot automation is not introduced. The rounded-cover incident proves that structural tests must target the rendered property owner/geometry rather than infer runtime behavior from a parent style.

## Design principles

1. Semantic role before literal value: pages request `PageTitle`, `PrimaryButton` or `CardSurface`, not a size/color.
2. One visual authority: existing dictionaries are extended; no second theme tree is introduced.
3. Styles for appearance, controls for reusable structure/behavior.
4. Progressive migration: each task converts complete user-visible slices and keeps untouched screens working.
5. Accessibility is part of every component: focus visual, keyboard reachability, accessible names and text-backed status meaning.
6. Real data only: search/status presentation may expose only facts already represented by PlayStead.
7. The validated Game Detail Hero is a preservation boundary, not a redesign target.
8. Consistency is the goal, not tokenizing every number; exceptions are named in tests and reviewed as contracts.

## Explicit non-goals

- No domain, persistence, identity, notification lifecycle or media-resolution changes.
- No new provider, network search, genre/mod indexing or fuzzy ranking.
- No Game Detail content cards for technologies, achievements, community, screenshots or news.
- No external icon pack or downloaded font in this phase.
- No replacement of WPF navigation, no new router and no route renaming.
- No pixel-perfect clone and no automated runtime launch.

## Authoritative token architecture

### Typography

Keep `PlayStead.Font.Body` as the compatibility alias and introduce:

- `PlayStead.Font.Display`: `Geist Sans, Segoe UI Variable, Segoe UI`.
- `PlayStead.Font.Body`: `Geist Sans, Segoe UI Variable, Segoe UI`.
- `PlayStead.Font.Mono`: `Cascadia Mono, Consolas`.
- TextBlock styles: `PlayStead.Text.Display`, `PageTitle`, `SectionTitle`, `CardTitle`, `Body`, `BodySecondary`, `Caption`, `ButtonLabel`, `Technical`.

The first implementation uses fallback resolution because Geist is absent. A later font-asset change may bundle approved Geist files under `Assets/Fonts/Geist/` with license/provenance and WPF pack URI syntax; that asset adoption is not required to close UI Foundation v2.

Role values are fixed for the first migration: Display 30/Bold, PageTitle 26/SemiBold, SectionTitle 17/SemiBold, CardTitle 15/SemiBold, Body 14/Normal, BodySecondary 13/Normal, Caption 12/Normal, ButtonLabel 14/SemiBold, Technical 13/Normal. Existing Hero title remains 30/Bold and uppercase via presentation binding only.

### Spacing, radius and borders

- Preserve `PlayStead.Spacing.1..6` as 4/8/16/24/32/48.
- Preserve `PlayStead.Radius.Small/Medium/Large` as 8/12/16.
- Add `PlayStead.Border.Default` (`Thickness=1`) and `PlayStead.Border.None` (`Thickness=0`).
- Keep geometry-specific radii where required by media clipping; do not replace the Hero cover's `RectangleGeometry` with a style inference.

### Buttons

Named styles in `PlaySteadControls.xaml`:

- `PlayStead.Button.Primary`: copper background, primary text, copper hover/pressed and explicit muted disabled state.
- `PlayStead.Button.Secondary`: dark raised surface, border, copper focus/hover.
- `PlayStead.Button.Icon`: square minimum 36x36, transparent/subtle surface, tooltip and automation name required by tests.
- `PlayStead.Button.Ghost`: transparent default, surface hover, used only for low-emphasis navigation or dismissal.

All derive from `PlayStead.ControlBase`, use the existing focus visual and share normal/hover/pressed/disabled/focused contracts. No Danger family is created until a real destructive action needs it. `PlaySplitButton` consumes Primary/Icon visual roles without changing commands, menu behavior or launch semantics.

### Surfaces and statuses

Named Border styles:

- `PlayStead.Surface.Card`: Surface, Border, radius Medium, spacing 3.
- `PlayStead.Surface.CardNested`: SurfaceSubtle, Border, radius Small, spacing 2.
- `PlayStead.Surface.Metric`: SurfaceRaised, Border, radius Small, spacing 3.
- `PlayStead.Surface.Selected`: SurfaceHover, BorderStrong; selection indicator remains copper.
- `PlayStead.Surface.Panel`: SurfaceStrong, Border, radius Medium, spacing 3.

`StatusBadge` gains a `StatusBadgeTone` dependency property with exact values `Neutral`, `Success`, `Warning`, `Danger`, `Info`. The template maps tone to existing semantic brushes while retaining readable text. Provider labels stay Neutral; only proven states use semantic tones.

### Iconography

Retain `Segoe MDL2 Assets` through semantic glyph resources in one dictionary section (`PlayStead.Glyph.Home`, `Library`, `Attention`, `Settings`, `Notifications`, `Search`, `More`, `Close`). Do not scatter Unicode literals or provider logos. Steam branding remains media/provider-owned and is not synthesized by the icon system.

## Target architecture

### Navigation

Create `Controls/NavigationItem.xaml(.cs)` with dependency properties `string Label`, `string Glyph`, `ICommand Command`, `bool IsActive`, and `string AutomationName`. The internal button applies `PlayStead.Navigation.Item`, shows glyph+label, and renders a copper bottom indicator when active. MainWindow binds existing `ShellViewModel` commands/flags. No route or navigation service changes are allowed.

### Global game search

Create `Search/GlobalGameSearchViewModel.cs` and `Search/GlobalGameSearchView.xaml(.cs)`. The ViewModel consumes the existing `LibraryViewModel`, `NavigationService` and `LibrarySearchService`; it exposes `Query`, `Results`, `IsOpen`, `SetQuery`, `Clear`, and `SelectResultCommand`. V1 copy is `Rechercher un jeu…`. Selecting a result calls `LibraryViewModel.SelectGame(item)` and navigates to `AppRoute.Library`, opening/updating the existing Quick Panel path. `Ctrl+K` focuses this persistent shell field. No debounce, genre/mod claims, provider matching or new index is added.

### Responsive shell

At widths down to the existing 960-DIP minimum, navigation items may reduce label spacing but remain keyboard reachable; search has a bounded width and may collapse results, never routes. Below 1100 DIP Game Detail keeps its existing lower-module stacking. Library grid calculation, Quick Panel scrolling, Hero full-bleed layout and 176x264 cover remain untouched.

## Preservation contracts

Every task must keep these tests/contracts green:

- Game Detail Hero composition, `HeroPath`, local readability rail and full-bleed backdrop.
- Hero cover 176x264 with actual `RectangleGeometry Rect="0,0,176,264" RadiusX="12" RadiusY="12"`.
- Game Detail live session refresh and play CTA behavior.
- Library single-click selection/Quick Panel and cover double-click Game Detail navigation.
- Existing identity, decision, notification lifecycle and database schema.
- All current routes, back navigation and `Ctrl+K` behavior until Task 6 deliberately relocates focus.
- Steam direct, legacy and nested SHA-1 artwork resolution.

## Vertical-slice tasks

### Task 1: Design tokens foundation

**Files:**
- Modify: `src/PlayStead.UI/Themes/PlaySteadTokens.xaml`
- Modify: `src/PlayStead.UI/Themes/PlaySteadControls.xaml`
- Representative migration only: `src/PlayStead.UI/Controls/PlaySplitButton.xaml`
- Representative migration only: `src/PlayStead.UI/Controls/EmptyState.xaml`
- Representative migration only: `src/PlayStead.UI/Controls/GameCard.xaml`
- Create: `tests/PlayStead.UI.Tests/Themes/PlaySteadDesignTokenTests.cs`
- Create: `tests/PlayStead.UI.Tests/Themes/PlaySteadXamlResourceSmokeTests.cs`
- Create: `tests/PlayStead.UI.Tests/Themes/HardcodedUiValueGuardTests.cs`
- Create: `tests/PlayStead.UI.Tests/Themes/HardcodedUiBaseline.txt`
- Modify: `tests/PlayStead.UI.Tests/PlayStead.UI.Tests.csproj` only if the baseline requires explicit copy metadata.

**Authoritative resources:**
- Preserve stable brush keys and add semantic compatibility aliases only where an existing literal needs a role: `PlayStead.Brush.Accent`, `AccentHover`, `AccentPressed`, `AccentMuted`, `SurfaceElevated`, `SurfaceNested`, `Error`, `Neutral`.
- Preserve `PlayStead.Spacing.1..6`; add typed `Thickness` roles `PlayStead.Inset.Page=24`, `PlayStead.Inset.Card=16`, `PlayStead.Inset.Control=8`, `PlayStead.Gap.Block.Small=0,8,0,0`, `PlayStead.Gap.Inline.XSmall=2,0,0,0`.
- Preserve `PlayStead.Radius.Small/Medium/Large`; the Hero clip's numeric RadiusX/RadiusY 12 remains an explicit geometry exception.
- Add typed `Thickness` keys `PlayStead.Border.None=0`, `PlayStead.Border.Thin=1`, `PlayStead.Border.Emphasis=2`.
- Add `PlayStead.Font.Sans` with exact fallback `Geist Sans, Segoe UI Variable, Segoe UI`; keep `PlayStead.Font.Body` as a compatibility key with the same chain. No font file is downloaded or bundled.
- Add reusable scalar `system:Double` primitives only for the 12 audited sizes; Task 2 assigns semantic text roles. Add reusable control metrics `PlayStead.Control.Height=36`, `PlayStead.Control.IconSize=36`, `PlayStead.Control.Radius=8` and typed control padding.

**Hard-coded guard format:** `HardcodedUiBaseline.txt` is sorted and human-readable. Each non-comment row is `relative/path.xaml|Category|Value|OccurrenceCount`. Categories are `HexColor`, `FontSize`, `FontWeight`, `CornerRadius`, `Spacing`, and `LocalButtonAppearance`. Token definition files are excluded from literal-debt scanning; validated geometry/responsive exceptions are explicit comment records. The test requires exact equality with the baseline, rejects unknown rows, rejects increases, and uses synthetic XAML cases to prove new hex/radius values fail. Any deliberate reduction updates the baseline and fixed category totals in the same reviewed change.

**RED contract:**
- Token tests fail because the aliases, typed thickness/border/font/control metrics do not exist.
- Smoke tests fail until representative consumers load the new typed resources.
- Guard tests fail until the committed baseline matches current production XAML and the scanner rejects synthetic additions.

**GREEN acceptance:**
- Dictionaries load in their existing order and every key has the expected WPF type (`SolidColorBrush`, `Thickness`, `CornerRadius`, `FontFamily`, `Double`).
- No composite DynamicResource/Thickness syntax exists.
- Representative migrations prove four categories without redesign: one literal color to brush, one inset/gap to typed Thickness, one radius literal to token, and control/default font use to `PlayStead.Font.Sans`.
- Existing debt is recorded, not expanded; pages are not broadly migrated.
- Game Detail Hero, cover geometry, media, live refresh, CTA and responsive contracts remain unchanged.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~PlaySteadDesignTokenTests|FullyQualifiedName~PlaySteadXamlResourceSmokeTests|FullyQualifiedName~HardcodedUiValueGuardTests"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GameDetailHero|FullyQualifiedName~GameDetail"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~Library"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
git diff --check
```

**Commit after user review only:** `feat(ui): add authoritative design tokens`

### Task 2: Typography foundation

**Files:**
- Modify: `src/PlayStead.UI/Themes/PlaySteadTokens.xaml`
- Modify: `src/PlayStead.UI/Themes/PlaySteadControls.xaml`
- Modify: `src/PlayStead.UI/Controls/SectionHeader.xaml`
- Modify: `src/PlayStead.UI/Controls/KpiCard.xaml`
- Modify: `src/PlayStead.UI/Controls/EmptyState.xaml`
- Test: `tests/PlayStead.UI.Tests/Themes/PlaySteadTypographyContractTests.cs`
- Test: `tests/PlayStead.UI.Tests/Themes/PlaySteadThemeContractTests.cs`

**RED contract:** Assert all font keys and nine named text styles exist exactly once, have the fixed role values above, retain Segoe fallbacks, and load through `Application.Resources`. Assert the three reusable controls consume roles rather than literal sizes.

**GREEN acceptance:** Tokens/styles exist, default body compatibility remains, controls render with the same data contracts, and Game Detail Hero title remains 30/Bold. No font binary is added.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~PlaySteadTypographyContractTests|FullyQualifiedName~PlaySteadThemeContractTests"
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

**Commit:** `feat(ui): add semantic typography foundation`

### Task 3: Button foundation

**Files:**
- Modify: `src/PlayStead.UI/Themes/PlaySteadControls.xaml`
- Modify: `src/PlayStead.UI/Controls/PlaySplitButton.xaml`
- Modify: `src/PlayStead.UI/Settings/SettingsView.xaml`
- Modify: `src/PlayStead.UI/Library/LibraryView.xaml`
- Test: `tests/PlayStead.UI.Tests/Themes/PlaySteadButtonStyleTests.cs`
- Test: `tests/PlayStead.UI.Tests/Library/PlaySplitButtonDisabledVisualTests.cs`
- Test: `tests/PlayStead.UI.Tests/Accessibility/KeyboardNavigationContractTests.cs`

**RED contract:** Assert Primary/Secondary/Icon/Ghost styles derive from the common base and define normal, hover, pressed, disabled and focus behavior. Assert icon-only uses keep tooltip/automation names. Assert `PlaySplitButton` keeps its public dependency properties, commands and installation menu while consuming shared visual roles.

**GREEN acceptance:** Settings Save uses Primary, Library low-emphasis actions use Secondary/Ghost/Icon as appropriate, literal button palette duplication is removed from touched surfaces, and launch behavior is byte-for-byte unchanged in C#.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~PlaySteadButtonStyleTests|FullyQualifiedName~PlaySplitButton|FullyQualifiedName~KeyboardNavigationContractTests"
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

**Commit:** `feat(ui): add semantic button styles`

### Task 4: Surface, card and status foundation

**Files:**
- Modify: `src/PlayStead.UI/Themes/PlaySteadTokens.xaml`
- Modify: `src/PlayStead.UI/Themes/PlaySteadControls.xaml`
- Modify: `src/PlayStead.UI/Controls/StatusBadge.xaml`
- Modify: `src/PlayStead.UI/Controls/StatusBadge.xaml.cs`
- Create: `src/PlayStead.UI/Controls/StatusBadgeTone.cs`
- Modify: `src/PlayStead.UI/Controls/KpiCard.xaml`
- Modify: `src/PlayStead.UI/Controls/EmptyState.xaml`
- Test: `tests/PlayStead.UI.Tests/Themes/PlaySteadSurfaceContractTests.cs`
- Test: `tests/PlayStead.UI.Tests/Controls/StatusBadgeToneTests.cs`

**RED contract:** Assert five named surface styles and border thickness tokens exist; assert `StatusBadgeTone` has exactly Neutral/Success/Warning/Danger/Info and maps to existing brushes; assert status meaning remains textual.

**GREEN acceptance:** Reusable cards consume named surface styles, StatusBadge remains source-compatible through default Neutral, no new product state is invented, and no page-specific control is created for pure appearance.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~PlaySteadSurfaceContractTests|FullyQualifiedName~StatusBadgeToneTests|FullyQualifiedName~ReusableSessionControlsTests|FullyQualifiedName~KeyboardNavigationContractTests"
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

**Commit:** `feat(ui): add shared surface and status roles`

### Task 5: Shell navigation polish

**Files:**
- Create: `src/PlayStead.UI/Controls/NavigationItem.xaml`
- Create: `src/PlayStead.UI/Controls/NavigationItem.xaml.cs`
- Modify: `src/PlayStead.UI/Themes/PlaySteadTokens.xaml`
- Modify: `src/PlayStead.UI/Themes/PlaySteadControls.xaml`
- Modify: `src/PlayStead.UI/MainWindow.xaml`
- Test: `tests/PlayStead.UI.Tests/Navigation/NavigationItemTests.cs`
- Modify: `tests/PlayStead.UI.Tests/MainWindowShellTests.cs`
- Modify: `tests/PlayStead.UI.Tests/Accessibility/KeyboardNavigationContractTests.cs`

**Interfaces:** `NavigationItem.Label`, `Glyph`, `Command`, `IsActive`, `AutomationName`; existing Shell commands/flags are consumed unchanged.

**RED contract:** Assert each primary route uses one NavigationItem, active state binds to the matching flag, the copper indicator is exclusive, commands still reach exact routes, and every item is keyboard/focus accessible.

**GREEN acceptance:** Shell is compact with icon+label and active underline; notification toggle remains functional; all route/history tests pass; `NavigationService`, `AppRoute` and materialization C# remain unchanged.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~NavigationItemTests|FullyQualifiedName~MainWindowShellTests|FullyQualifiedName~NavigationServiceTests|FullyQualifiedName~ShellViewModelTests|FullyQualifiedName~KeyboardNavigationContractTests"
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

**Commit:** `feat(shell): add compact semantic navigation`

### Task 6: Persistent global game search foundation

**Files:**
- Create: `src/PlayStead.UI/Search/GlobalGameSearchViewModel.cs`
- Create: `src/PlayStead.UI/Search/GlobalGameSearchView.xaml`
- Create: `src/PlayStead.UI/Search/GlobalGameSearchView.xaml.cs`
- Modify: `src/PlayStead.UI/MainWindow.xaml`
- Modify: `src/PlayStead.UI/MainWindow.xaml.cs`
- Modify: `src/PlayStead.UI/Bootstrap/PlaySteadHost.cs`
- Test: `tests/PlayStead.UI.Tests/Search/GlobalGameSearchViewModelTests.cs`
- Test: `tests/PlayStead.UI.Tests/Search/GlobalGameSearchContractTests.cs`
- Modify: `tests/PlayStead.UI.Tests/Library/LibrarySearchUiContractTests.cs`

**Interfaces:** Constructor `GlobalGameSearchViewModel(LibraryViewModel library, NavigationService navigation)`; properties `string Query`, `IReadOnlyList<LibraryItemViewModel> Results`, `bool IsOpen`, `ICommand SelectResultCommand`; methods `void SetQuery(string query)`, `void Clear()`.

**RED contract:** Assert title-prefix behavior is exactly the Library service, empty input closes results, selection selects the exact item and navigates to Library, `Ctrl+K` focuses shell search, and copy claims only game search.

**GREEN acceptance:** One persistent shell field reuses `LibrarySearchService`; Library search continues to work independently; no fuzzy/provider/genre/mod search, debounce, network or second filtering algorithm exists.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~GlobalGameSearch|FullyQualifiedName~LibrarySearch"
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

**Commit:** `feat(shell): add global game search foundation`

### Task 7: Global screen migration

**Files:**
- Modify: `src/PlayStead.UI/Home/HomeView.xaml`
- Modify: `src/PlayStead.UI/Library/LibraryView.xaml`
- Modify: `src/PlayStead.UI/Library/GameDetailView.xaml`
- Modify: `src/PlayStead.UI/Controls/GameCard.xaml`
- Modify: `src/PlayStead.UI/Sessions/SessionsView.xaml`
- Modify: `src/PlayStead.UI/Sessions/SessionDetailView.xaml`
- Modify: `src/PlayStead.UI/Attention/AttentionView.xaml`
- Modify: `src/PlayStead.UI/Settings/SettingsView.xaml`
- Modify: `src/PlayStead.UI/Notifications/NotificationPanel.xaml`
- Test: `tests/PlayStead.UI.Tests/Home/HomeFoundationVisualTests.cs`
- Test: `tests/PlayStead.UI.Tests/Library/LibraryFoundationVisualTests.cs`
- Test: `tests/PlayStead.UI.Tests/Themes/RemainingPagesFoundationTests.cs`
- Modify: existing `tests/PlayStead.UI.Tests/Library/GameDetail*ContractTests.cs` only where assertions reference migrated style names.
- Modify: existing Sessions/Attention/Settings/Notifications contract tests only where assertions reference migrated style names.

**RED contract:** Assert every page uses semantic typography, shared surface/button roles, and no page-local fundamental appearance. Assert touched hard-coded baseline entries are removed, notification/session/settings behavior remains bound to the same commands/state, and Game Detail preservation contracts remain structurally exact.

**GREEN acceptance:** All screens use the shared hierarchy; Game Detail lower cards migrate while Hero backdrop, rail, cover geometry, metadata, CTA bindings and responsive code remain unchanged. No ViewModel, notification lifecycle, session correction, identity or persistence behavior changes.

**Commands:**
```powershell
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~Home|FullyQualifiedName~Library|FullyQualifiedName~GameDetail"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --filter "FullyQualifiedName~Sessions|FullyQualifiedName~Attention|FullyQualifiedName~Settings|FullyQualifiedName~Notification"
dotnet test ".\tests\PlayStead.Providers.Tests\PlayStead.Providers.Tests.csproj" --configuration Release --filter "FullyQualifiedName~SteamLocalMediaLocatorTests"
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
```

**Commit:** `refactor(ui): apply foundation across product screens`

### Task 8: Responsive, accessibility and runtime acceptance gate

**Files:**
- Modify only if a failing contract proves a foundation defect: UI XAML/styles introduced by Tasks 1–7.
- Create: `tests/PlayStead.UI.Tests/Themes/UiFoundationXamlLoadTests.cs`
- Modify: `tests/PlayStead.UI.Tests/Accessibility/KeyboardNavigationContractTests.cs`
- Evidence outside repo: `D:\Dev\PlayStead\02_RAPPORTS\PLAYSTEAD_UI_FOUNDATION_V2\`

**Automated RED/GREEN gate:** Add XAML load smoke coverage for App/MainWindow and every major view; verify tab reachability, visible focus, icon accessible names, 960x640 shell contract, Library responsive grid, Quick Panel scroll, Game Detail 1100-DIP behavior and no horizontal content scroll.

**Commands:**
```powershell
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"
dotnet test ".\tests\PlayStead.UI.Tests\PlayStead.UI.Tests.csproj" --configuration Release --no-restore -m:1
dotnet test ".\PlayStead.sln" --configuration Release --no-restore -m:1
dotnet build ".\PlayStead.sln" --configuration Release --no-restore -m:1 /warnaserror
git diff --check
```

**Manual user-only acceptance:** Inspect Home, Library grid/list/Quick Panel, Game Detail, Sessions/detail, Attention, Settings and Notifications at 960x640, 1100-wide and maximized. Verify typography hierarchy, all button states, active navigation, global search, status meaning, keyboard focus and reduced-motion behavior. Reconfirm the full Game Detail Hero, 176x264 rounded cover, live session refresh, Library click behavior and Steam nested Hero display.

**GREEN acceptance:** Automated suites and strict build pass; manual `RUNTIME_GREEN`, `VISUAL_GREEN`, `ACCESSIBILITY_GREEN`, `RESPONSIVE_GREEN` and `PRODUCT_COHERENCE_GREEN` are all true before closure.

**Commit:** `docs(ui): close UI foundation v2 acceptance`

## Final Definition of Done

- All eight tasks are independently reviewed and committed.
- The semantic typography, button, surface, status and icon roles are authoritative and documented by tests.
- Every current page uses the foundation without a parallel local palette.
- Navigation routes/history and domain behavior are unchanged.
- Global search searches games only and reuses Library matching.
- All preservation contracts pass, including Game Detail Hero/media/live refresh and Steam nested artwork.
- Full solution tests pass and Release `/warnaserror` reports 0 warnings / 0 errors.
- Manual runtime validation confirms normal, minimum and wide layouts.
- No network dependency, database migration, external provider, unsupported data claim or unapproved font binary is introduced.
