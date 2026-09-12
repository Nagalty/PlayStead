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

## Critère de sortie Task 13

Task 13 ne sera fermée qu'après validation supplémentaire du publish dev `0.1.0-dev`, de la baseline de performances et du gate final. Cette checklist documente l'état accepté avant ces dernières étapes.
