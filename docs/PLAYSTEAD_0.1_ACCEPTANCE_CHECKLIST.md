# PlayStead 0.1 — Acceptance Checklist

Version évaluée : **`0.1.0-dev`**

Cette checklist documente les critères réellement validés pour la fondation locale PlayStead 0.1. Elle ne constitue pas une promesse de fonctionnalités futures.

## Baseline Git

- [x] Branche de développement : `feat/0.1-local-foundation`.
- [x] Premier baseline commit : `82f7e6057470fe96260704a6f99026aec16da610`.
- [x] Scaffolds orphelins `PlayStead.System` et `PlayStead.System.Tests` retirés avant le baseline.
- [x] `.gitignore` actif pour `.vs`, `bin`, `obj`, résultats de tests et artefacts générés.
- [x] Working tree propre après le baseline.

## Build et tests

- [x] Restore de `PlayStead.sln` validé.
- [x] Build Release validé avec warnings traités comme erreurs.
- [x] Build Release : **0 warning / 0 erreur**.
- [x] Baseline automatisée : **89/89 tests PASS**.
- [x] Aucun test failed.
- [x] Aucun test skipped / not executed.
- [x] Gate TRX agrégé sur les cinq projets de tests.

## Smoke Windows réel

- [x] Démarrage WPF réel sans élévation UAC.
- [x] Détection Steam réelle : **28 installations** trouvées sur les bibliothèques locales de la machine de validation.
- [x] Redémarrage **cache-first** : bibliothèque disponible depuis l'état persistant avant le rafraîchissement.
- [x] Comportement **single-instance** validé.
- [x] Une seconde invocation est transférée à l'instance principale.
- [x] La fenêtre principale revient au **premier plan** lors de la seconde invocation.
- [x] Aucun timeout d'activation observé après correction du cycle de vie named pipe/WPF.
- [x] Fermeture propre : aucun processus zombie ni pipe résiduel.
- [x] Restauration de la **position et de la taille** normales validée après fermeture et relance.
- [x] Restauration de l'état **maximisé** validée après fermeture et relance.

## Persistance et données utilisateur

- [x] Base locale présente sous `%LOCALAPPDATA%\PlayStead\Data\playstead.db`.
- [x] Placement de fenêtre présent sous `%LOCALAPPDATA%\PlayStead\Data\window-placement.json`.
- [x] **Aucune donnée utilisateur à côté de l'exécutable**.
- [x] Aucun `playstead.db`, `window-placement.json`, WAL ou SHM utilisateur dans le répertoire Release de l'application.

## CI Windows

- [x] Workflow **Windows CI** présent sous `.github/workflows/windows-ci.yml`.
- [x] Déclenchement prévu sur `push` et `pull_request`.
- [x] Runner `windows-latest`.
- [x] SDK .NET 10 configuré.
- [x] Restore, build Release `/warnaserror` et tests Release configurés.
- [x] Résultats TRX produits et contrôlés.
- [x] La CI exige au moins 89 tests et zéro échec / zéro test non exécuté.
- [x] Les résultats de tests sont publiés comme artefact de CI.
- [x] Aucun chemin Windows spécifique au poste de développement n'est codé dans le workflow.

## Publish dev — Task 13E CLOSED

- [x] Publish dev **`0.1.0-dev`** généré en `win-x64`, framework-dependent.
- [x] Publish : **46 fichiers**, **3 988 909 octets** (≈ **3,8 MiB**).
- [x] `PlayStead.UI.exe`, `PlayStead.UI.runtimeconfig.json` et `PlayStead.UI.deps.json` présents.
- [x] `runtimeconfig` validé pour .NET 10.
- [x] Aucune donnée utilisateur embarquée dans le publish.
- [x] Aucune fuite de sources/projets dans le publish.
- [x] Smoke réel du binaire publié : fenêtre affichée, fermeture propre, `ExitCode=0`.

## Baseline performances — Task 13F CLOSED

Mesure réalisée sur le publish `0.1.0-dev-win-x64`. Le cache disque/page Windows n'a pas été forcé à froid : il s'agit d'une **baseline reproductible de développement**, pas d'un SLA.

- [x] Premier lancement “cold-ish” jusqu'à fenêtre prête : **507 ms**.
- [x] Lancements warm : **452 ms de moyenne**, **450 ms de médiane**, min **449 ms**, max **457 ms**.
- [x] Working set cold-ish après stabilisation : **158,23 MiB**.
- [x] Working set warm moyen : **152,94 MiB**.
- [x] Mémoire privée cold-ish : **191,23 MiB**.
- [x] Mémoire privée warm moyenne : **155,39 MiB**.
- [x] Publish de référence : **46 fichiers / 3,8 MiB**.

## Task 13G — Gate final

Le gate final doit confirmer une dernière fois l'état Git, le build Release, les **89/89 tests**, la CI Windows, la documentation, le publish dev et la présence de la baseline de performances. Une fois ce gate PASS, la fondation PlayStead **0.1** peut être déclarée complète.
