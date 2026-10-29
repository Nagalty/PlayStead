# PlayStead Agent Rules (V1)

These rules apply to work in this repository. Follow any narrower task instructions first when they explicitly set a different boundary.

## Autonomy and scope

- Work autonomously within the user-authorized task: inspect, design, implement, test, and correct local changes. Local refactoring is allowed when justified by the task.
- Do not ask for approval for routine edits or checks. Surface genuine blockers and decisions outside the authorized scope.
- Local commits are allowed when requested or when the task explicitly authorizes them. Never push without explicit authorization.

## Git safety

- Never force-push, run destructive resets, or run destructive `git clean` commands.
- Never use `git add .` or `git add -A`; stage only reviewed, explicitly named files.
- Preserve pre-existing work. Inspect status and diffs before editing or staging, and do not overwrite unrelated changes.
- Keep temporary files and non-versioned reports out of commits. Do not create branches or worktrees without agreement.

## Avoiding artifact accumulation

- Do not make full project copies as routine precaution and do not duplicate worktrees or backups without a concrete need.
- Keep at most one relevant reference backup per scope unless there is a stated reason for more. Never accumulate names such as `backup_old`, `backup_v2`, or `final_backup`.
- Git is the source of truth for code history. Never archive `bin`, `obj`, `.git`, or NuGet caches.
- Before replacing a backup, create and verify the replacement. Ask before deleting the previous backup.
- Deleting backups or user data requires explicit authorization.

## Temporary artifacts

- Put disposable logs, dumps, scripts, and audit artifacts under `D:\Dev\PlayStead\05_TEMP`.
- Do not scatter temporary files at the repository root or turn `05_TEMP` into a permanent archive.
- Do not commit `05_TEMP`. Ask before deleting sensitive or potentially useful temporary data.

## User data and game files

- Never perform destructive operations on the real PlayStead database or actual game installations, configurations, saves, or mods.
- Use fixtures and isolated environments for tests. Version and test database migrations.
- Verify backups before a genuinely risky live migration. Do not perform destructive restores without explicit authorization.

## PlayStead architecture

- Preserve the local-first design.
- Keep canonical `GameId` separate from provider installations and use reliable provider identities.
- Do not automatically link games by title alone. Keep provenance and confidence explicit.
- Prefer provider-neutral services where appropriate and isolate installations.
- Do not replace official launchers or add abstractions without a concrete need.

## User experience

- Design for players, with French as the primary language.
- Reuse existing styles and design tokens. Do not redesign the application globally unless requested.
- Treat validated Game Detail Hero and section designs as protected unless the task explicitly covers them.
- Avoid unnecessary technical language in player-facing UI, dead buttons, and sections with no useful content.

## Quality and reporting

- Run targeted tests, a Release build, `git diff --check`, and relevant regression checks for implementation tasks.
- Distinguish pre-existing failures from failures introduced by the current work.
- Never report runtime validation as passing unless it was tested in the real runtime.
- Fix bugs introduced by the task. Prefer a general fix when the cause is general.
- Report the change, checks and results, limits or unverified behavior, any local commit, and final Git state. Keep reports concise.
