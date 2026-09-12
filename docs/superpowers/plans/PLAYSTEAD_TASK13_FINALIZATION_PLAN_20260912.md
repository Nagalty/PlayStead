# PlayStead 0.1 Finalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finaliser PlayStead 0.1 avec un dépôt Git sain, un gate Release reproductible, une CI Windows, une documentation publique minimale, une checklist d’acceptation, un publish dev 0.1 et une baseline de performances mesurée.

**Architecture:** La finalisation reste extérieure au cœur fonctionnel déjà validé. Les contrôles sont centralisés dans `04_OUTILS`, les rapports dans `02_RAPPORTS`, la CI appelle le même gate que le poste local, et les documents de release restent dans le repo. Aucun changement fonctionnel UI/scan/persistence n’est prévu dans Task 13 sauf anomalie révélée par les gates.

**Tech Stack:** Windows 10/11, C#, .NET 10, WPF, PowerShell 7, Git, GitHub Actions.

**Spec:** Cahier des charges PlayStead 0.1 validé + état de projet validé après Tasks 10/11/12.

## Global Constraints

- Version de développement : `0.1.0-dev`.
- Première release publique complète : `1.0`.
- Windows uniquement pour 0.1.
- Build Release : 0 erreur.
- Gate tests actuel validé : 89/89.
- Données utilisateur : `%LOCALAPPDATA%\PlayStead`.
- Aucun état utilisateur à côté de l’exécutable.
- Pas de changement fonctionnel hors nécessité de gate.
- Les scripts PowerShell nouveaux utilisent des noms uniques ; aucun remplacement silencieux d’un ancien script.

---

### Task 13A: Repository Baseline & Source Control Safety

**Files:**
- Create: `04_OUTILS/PLAYSTEAD_TASK13_PRECHECK_PS7.ps1`
- Create after audit if absent: `.gitignore`
- No source modification before the audit is reviewed.

**Interfaces:**
- Consumes: repo `D:\Dev\PlayStead\00_PROJET`.
- Produces: état Git autoritaire, liste des fichiers générés à ignorer, inventaire CI/docs/versioning.

- [ ] Run Task 13 precheck.
- [ ] Confirm current branch `feat/0.1-local-foundation`.
- [ ] Confirm whether HEAD exists.
- [ ] Confirm tracked/untracked state and generated directories.
- [ ] Create or correct `.gitignore` only from evidence.
- [ ] Establish a clean initial/baseline commit before release finalization.

### Task 13B: Reproducible Release Gate

**Files:**
- Create: `04_OUTILS/PLAYSTEAD_TASK13_RELEASE_GATE_PS7.ps1`
- Create: `02_RAPPORTS/PLAYSTEAD_TASK13_RELEASE_GATE_<timestamp>.txt` at runtime.

**Interfaces:**
- Consumes: `PlayStead.sln`.
- Produces: deterministic Release restore/build/test result and acceptance summary.

- [ ] Gate checks .NET SDK/runtime context.
- [ ] `dotnet restore PlayStead.sln`.
- [ ] `dotnet build PlayStead.sln --configuration Release --no-restore`.
- [ ] `dotnet test PlayStead.sln --configuration Release --no-build`.
- [ ] Require total tests >= 89 and zero failed/skipped unless explicitly approved.
- [ ] Report PASS/FAIL with command exit codes.

### Task 13C: Windows CI

**Files:**
- Create: `.github/workflows/windows-ci.yml`.

**Interfaces:**
- Consumes: same solution and Release commands as local gate.
- Produces: Windows CI on push and pull request.

- [ ] Use `windows-latest`.
- [ ] Install .NET 10 SDK.
- [ ] Restore solution.
- [ ] Build Release.
- [ ] Test Release.
- [ ] Upload TRX/test artifacts on failure or always if useful.
- [ ] Keep CI free of user-specific absolute paths.

### Task 13D: README & Acceptance Checklist

**Files:**
- Create or update: `README.md`.
- Create: `docs/PLAYSTEAD_0.1_ACCEPTANCE_CHECKLIST.md`.

**Interfaces:**
- Consumes: validated behavior from Tasks 1–12.
- Produces: public-facing project summary and auditable 0.1 acceptance record.

- [ ] README states current version `0.1.0-dev`.
- [ ] README states Windows-only 0.1 scope.
- [ ] README explains local-first Steam detection, SQLite persistence, single-instance behavior, and LocalAppData storage.
- [ ] README does not claim unimplemented integrations/features.
- [ ] Acceptance checklist records 89/89 gate baseline.
- [ ] Acceptance checklist records real Windows smoke: Steam 28 installations, cache-first, foreground activation, clean shutdown, window placement normal/maximized, no user data beside exe.

### Task 13E: Dev Publish 0.1

**Files:**
- Create: `04_OUTILS/PLAYSTEAD_TASK13_PUBLISH_DEV_PS7.ps1`.
- Output: `02_RAPPORTS/PUBLISH/PlayStead-0.1.0-dev-win-x64/`.

**Interfaces:**
- Consumes: `src/PlayStead.UI/PlayStead.UI.csproj`.
- Produces: self-contained or framework-dependent dev publish chosen explicitly and documented.

- [ ] Publish `Release`, `win-x64`.
- [ ] Do not embed LocalAppData user state.
- [ ] Verify executable starts from publish directory.
- [ ] Verify package contains runtime files only.
- [ ] Record file count and total size.

### Task 13F: Performance Baseline

**Files:**
- Create: `04_OUTILS/PLAYSTEAD_TASK13_PERF_BASELINE_PS7.ps1`.
- Create runtime report in `02_RAPPORTS`.

**Interfaces:**
- Consumes: dev publish output.
- Produces: reproducible baseline for startup/process memory and publish size.

- [ ] Measure cold process launch-to-window-ready best-effort.
- [ ] Measure warm launch-to-window-ready best-effort.
- [ ] Record working set after stabilization.
- [ ] Record publish directory total bytes/files.
- [ ] Treat measurements as baseline, not SLA, unless the spec defines thresholds.

### Task 13G: Final Acceptance Gate

**Files:**
- No new production files unless a gate exposes a defect.
- Final report in `02_RAPPORTS`.

**Interfaces:**
- Consumes: 13A–13F outputs.
- Produces: Task 13 CLOSED evidence.

- [ ] Git working tree clean after intended commits.
- [ ] Windows CI workflow present and syntactically valid.
- [ ] Local Release gate PASS.
- [ ] 89+ tests, zero failures.
- [ ] README + acceptance checklist complete.
- [ ] Dev publish generated and smoke-tested.
- [ ] Performance baseline recorded.
- [ ] Tag/release decision remains separate from public `1.0`.
- [ ] Close Task 13 only after evidence is reviewed.

## Completion Rule

Task 13 becomes **CLOSED** only when the local Release gate, documentation, CI definition, dev publish, performance baseline, Git baseline and final acceptance evidence all pass. At that point PlayStead reaches **13/13 = 100 %** for the 0.1 foundation scope.
