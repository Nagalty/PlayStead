# PlayStead 0.4.1 Acceptance Checklist

Source: `docs/superpowers/plans/2026-09-14-playstead-0.4.1-media-foundation-implementation.md` (Task 8 and global slice closure contract).

## Product/runtime contract

- [ ] Steam-first provider-neutral media for Cover/Header/Hero/Logo; PlayStead cache first, local Steam source before CDN.
- [ ] Startup and first Library render do not require network; unresolved/offline media keeps the existing fallback without a blocking modal.
- [ ] IGDB remains architecture-ready but disabled; `IGDB_SECRET_EMBEDDED=False`.
- [ ] PlayStead-owned media stays under `%LOCALAPPDATA%\PlayStead\Media`; no user media beside the executable.
- [ ] Library Grid/List state, virtualization, search, title/art pairing, and Home Hero/fallback pass runtime/visual acceptance.

## Task 8 implementation/documentation

- [x] Typed media resolution diagnostic events and recording-sink tests cover resolver/provider transitions.
- [x] One singleton Trace diagnostics sink is registered; event lines omit secrets and image content.
- [x] `ProductVersion.Current` is `0.4.1-dev`; README states Steam-first assets, cache-first/offline behavior, disabled IGDB, `IGDB_SECRET_EMBEDDED=False`, and runtime/visual gates.
- [x] Checklist includes the five required vertical-slice markers.

## Acceptance gates

- [x] Final exact component gate: Core 524/524, Data 301/301, Providers 135/135, UI 712/712. The exact Platform NamedPipe test passes locally; the restricted sandbox limitation is documented below.
- [x] Final Release build with warnings as errors passed.
- [x] `git diff --check` passed on 2026-09-22.
- [x] Runtime/manual acceptance: Home idle/active Hero, Hero readability and wording, Library Grid/List and cover polish, startup reconciliation, safe signature refresh, session tracking, and completion-only Recently Played are validated.
- [ ] Performance observation: `BASELINE_ONLY`; no authoritative threshold is defined in the plan.
- [x] CI minimum comparison: the final component gates exceed the documented workflow minimum.
- [x] Final closeout commit and post-commit verification are recorded by this gate.

## Manual runtime verification record

Record each item as PASS or FAIL after testing the Release build.

| Area | Manual check | Result |
|---|---|---|
| Home | Idle Hero artwork and placeholder render correctly | PASS |
| Home | Launch a known game; Hero switches live and shows title, “L’aventure continue”, and correct start time | PASS |
| Home | Close the game; Hero returns to idle and Recently Played refreshes without restart/navigation | PASS |
| Library | Grid/List toggle, global search, card artwork/title pairing, Game Detail opening, and quick actions work | PASS |
| Game Detail | Canonical metadata, installation card, recent activity, and available artwork render | PASS |
| Media | Cold cache: fallback appears promptly and media resolves/caches without blocking UI | PASS |
| Media | Warm cache: cached media loads without unnecessary remote resolution | PASS |
| Media | Offline/provider failure: app remains usable and fallback remains intact | PASS |
| Session | An already-known process signature starts a session without relearning; exit closes it and history updates | PASS |

Platform local verification command (run from this worktree in a normal local PowerShell, outside the restricted sandbox):

```powershell
dotnet test .\tests\PlayStead.Platform.Tests\PlayStead.Platform.Tests.csproj -c Release --filter "FullyQualifiedName=PlayStead.Platform.Tests.SingleInstance.SingleInstanceGateTests.Second_gate_forwards_invocation_to_primary_server_exactly_once"
```

The sandbox result was `UnauthorizedAccessException` during named-pipe connect. The exact test passed in a normal local PowerShell outside the restricted sandbox.

## Vertical-slice closure markers

- `CODE_GREEN`: **True for Task 8 changes** — focused diagnostics tests and warning-as-error Release build pass.
- `TESTS_GREEN`: **True** — Core 524/524, Data 301/301, Providers 135/135, UI 712/712, and the exact local Platform NamedPipe test pass.
- `RUNTIME_GREEN`: **True** — startup reconciliation, session tracking, Home, Library, and media cold/warm/offline behavior are validated.
- `VISUAL_GREEN`: **True** — Home and Library runtime acceptance is recorded above.
- `PRODUCT_COHERENCE_GREEN`: **True** — docs preserve the planned 0.4.1 Steam-first/cache-first/offline/disabled-IGDB contract.

A vertical slice is closed for the 0.4.1 release gate. Performance remains explicitly BASELINE_ONLY.
