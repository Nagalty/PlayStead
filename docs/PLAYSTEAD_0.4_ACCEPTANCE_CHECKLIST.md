# PlayStead 0.4 — Acceptance Checklist

Version évaluée : **`0.4.0-dev`**

Cette checklist documente les critères réellement validés pour PlayStead 0.4. Elle n'invente ni backend, ni donnée, ni fonctionnalité future pour compléter l'interface.

## Baseline Git / portée

- [x] Branche : `feat/0.4-visual-redesign`.
- [x] HEAD de référence avant finalisation Task 12 : `578b2ec89cbd21d8e22af1e8ffde282a96c43784`.
- [x] Les fondations 0.1–0.3 restent préservées.
- [x] La refonte 0.4 reste principalement dans `PlayStead.UI`.
- [x] `git diff --check` pré-final : **PASS**.

## Build et tests automatisés

- [x] Gate automatisé pré-final Release exécuté.
- [x] Baseline fraîche : **507/507 tests PASS**.
- [x] Tests failed : **0**.
- [x] Tests skipped / not executed : **0**.
- [x] Le seuil minimal CI Windows doit être relevé de **349** à **507** à partir de ce comptage frais.
- [x] Gate final Release `/warnaserror` : **PASS**.
- [x] Gate final : **0 warning / 0 erreur**.
- [x] Post-commit verification complète : **PASS — 507/507 tests**.

## UX / runtime 0.4

- [x] Shell horizontal 0.4 visible.
- [x] Accueil visible et fonctionnel.
- [x] Navigation principale testée.
- [x] Bibliothèque **Grille** fonctionnelle.
- [x] Bibliothèque **Liste** fonctionnelle.
- [x] Panneau rapide fonctionnel.
- [x] Fiche jeu fonctionnelle.
- [x] Retour vers la Bibliothèque en conservant le contexte observé.
- [x] `Ctrl+K` focalise la recherche.
- [x] Recherche dynamique à chaque frappe.
- [x] Recherche limitée au **début du titre affiché** (`StartsWith`), insensible à la casse.
- [x] Recherche identique en Liste et en Grille.
- [x] Placeholder **« Rechercher un jeu »** visible au repos et masqué au focus.
- [x] Réduction des animations disponible dans Paramètres.
- [x] Fermeture/restauration via systray validée pendant le smoke runtime.
- [x] Sessions reste accessible comme surface contextuelle.

## Launch smoke

- [x] `GameLaunchService` dépend de `IExternalUriLauncher`.
- [x] Smoke automatisé avec launcher factice : **3/3 PASS**.
- [x] URI Steam vérifiée : `steam://rungameid/<AppId>`.
- [x] Aucun lancement réel de Steam ou d'un jeu n'est nécessaire au smoke automatisé.
- [x] Les installations non supportées ou absentes ne sont pas lancées.

## Accessibilité / feedback

- [x] Politique de réduction des animations couverte par tests.
- [x] Noms accessibles / tooltips ajoutés aux actions icon-only concernées.
- [x] Erreur de vérification Steam exposée localement sans effacer les données de bibliothèque.
- [x] Aucun spinner plein écran n'est requis pour ces opérations.
- [x] Les statuts ne reposent pas uniquement sur la couleur cuivre.

## Media preview

- [x] `MEDIA_PREVIEW_IMPLEMENTED=False`.
- [x] Aucun aperçu média fictif n'est affiché.
- [x] Aucune source média future n'est simulée ou annoncée comme disponible.

## Baseline performance 0.4

Rapport : `D:\Dev\PlayStead\02_RAPPORTS\PLAYSTEAD_TASK12_PERF_BASELINE_20260914-163533.txt`

- [x] `TASK12_PERF_BASELINE=PASS`.
- [x] Warm window readiness : **645,134 ms**.
- [x] Idle shell : working set moyen **173,274 Mio**, private moyen **131,267 Mio**.
- [x] Bibliothèque Grille : working set moyen **234,919 Mio**, private moyen **190,742 Mio**.
- [x] Sessions : working set moyen **240,489 Mio**, private moyen **194,515 Mio**.
- [x] Fixture synthétique : **5000 jeux**.
- [x] Recherche `A` : p95 **1,775 ms**.
- [x] Recherche `AR` : p95 **1,818 ms**.
- [x] Recherche `ARM` : p95 **2,156 ms**.
- [x] Projection grille : p95 **0,043 ms**.
- [x] Aucun SLA artificiel n'est introduit ; ces mesures constituent une baseline de développement.

## Données / comportement produit

- [x] Pas de donnée provider inventée.
- [x] Pas de future section environnement/mod/community simulée.
- [x] Recherche utilisateur centrée sur le nom affiché du jeu.
- [x] PlayStead reste local-first / cache-first.
- [x] Données utilisateur conservées sous `%LOCALAPPDATA%\PlayStead`.
- [x] Les checklists 0.1, 0.2 et 0.3 restent des références historiques.

## Finalisation Task 12

- [x] Documentation 0.4 appliquée au repo.
- [x] Seuil CI relevé à **507**.
- [x] Final gate complet PASS.
- [x] Commit final créé : `feat(ui): complete PlayStead 0.4 acceptance`.
- [x] Post-commit verification PASS.
- [x] Worktree final propre.
- [x] `PLAYSTEAD_04_STATUS=COMPLETE`.
- [x] Progression globale : **100 %**.

