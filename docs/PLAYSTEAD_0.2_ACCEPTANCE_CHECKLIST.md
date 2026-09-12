# PlayStead 0.2 — Acceptance Checklist

Version validée : `0.2.0-dev`

Branche : `feat/0.2-steam-reference`

Baseline code avant documentation Task 10 :

```text
edcb2ed6c042ab4760d7dc887da934998981fdd0
```

## 1. Build et tests automatisés

- [x] Solution .NET 10 / WPF construite en `Release`.
- [x] Build observé : **0 erreur / 0 warning**.
- [x] Suite complète observée : **186/186 tests PASS**.
- [x] **0 failed / 0 skipped** sur la suite complète.
- [x] Tests UI observés : **65/65 PASS**.
- [x] Les tests de la référence Steam n'exigent pas de réseau Steam réel.
- [x] Le TTL de fraîcheur distant est testé à **exactement 6 heures**.
- [x] Le démarrage non bloquant est couvert par les tests d'intégration runtime.

## 2. Contrat de référence Steam

- [x] Les preuves locales contiennent branche Steam et manifests de dépôts.
- [x] Les preuves locales et distantes sont persistées séparément.
- [x] L'évaluateur est pur et ne réalise aucun I/O.
- [x] `BuildID` reste un signal secondaire.
- [x] `BuildID` seul ne produit jamais `UpdateAvailable`.
- [x] Une divergence de manifests de dépôts peut produire `UpdateAvailable`.
- [x] Une preuve insuffisante produit un état conservateur `Unknown`.
- [x] Une branche distante absente ne déclenche aucun fallback silencieux vers `public`.
- [x] Les états supportés sont `UpToDate`, `UpdateAvailable`, `NewVersionDetected`, `Unknown` et `Checking`.
- [x] Une seule campagne distante est autorisée à la fois.
- [x] Un échec distant conserve la dernière preuve valide déjà en cache.

## 3. Cache et démarrage

- [x] Le cache distant est considéré frais pendant **6 h**.
- [x] Le démarrage charge d'abord la bibliothèque et les preuves persistées.
- [x] La campagne distante n'est pas attendue sur le chemin critique de démarrage.
- [x] Un rafraîchissement automatique ne cible que les preuves périmées.
- [x] Le rafraîchissement manuel force la campagne globale.
- [x] L'absence de SteamCMD ne fait pas disparaître la bibliothèque locale.
- [x] L'absence de SteamCMD ne fait pas échouer le démarrage de PlayStead.

## 4. UI minimale 0.2

- [x] Les jeux Steam exposent un état de référence.
- [x] Les jeux non-Steam n'affichent pas de faux état Steam.
- [x] Un jeu Steam sans preuve distante suffisante est présenté comme **« État inconnu »**.
- [x] Le bouton global **« Vérifier Steam »** est présent.
- [x] Le bouton est désactivé pendant une campagne et réactivé ensuite.
- [x] Une seconde demande concurrente n'ouvre pas une seconde campagne.
- [x] Le bouton de rescan local historique reste distinct du nouveau refresh Steam.

## 5. Smoke Windows réel

Environnement observé le `2026-09-12` :

```text
Windows / .NET SDK 10.0.401
28 installations Steam détectées
SteamCMD local : absent
SteamCMD PATH  : absent
```

- [x] Fenêtre principale PlayStead ouverte avec succès.
- [x] Bibliothèque réelle chargée.
- [x] Processus stable après 10 secondes.
- [x] Bouton **« Vérifier Steam »** détecté via UI Automation.
- [x] Invocation de **« Vérifier Steam »** réussie.
- [x] Bouton réactivé dans la fenêtre de validation.
- [x] Aucun `steamcmd.exe` créé automatiquement.
- [x] Aucun processus SteamCMD lancé.
- [x] Fermeture de PlayStead propre, sans kill forcé.
- [x] Arbre Git resté propre après les smokes.

### Smoke réseau SteamCMD

Résultat réel :

```text
STEAMCMD_RESOLVED=False
STEAMCMD_AUTO_DOWNLOAD_ATTEMPTED=False
REAL_PUBLIC_BRANCH_QUERY_EXECUTED=False
REAL_PUBLIC_BRANCH_QUERY_RESULT=NOT_EXECUTED_PREREQUISITE_MISSING
TASK10_STEAMCMD_SMOKE=PREREQUISITE_MISSING
```

- [x] Le prérequis manquant est explicitement enregistré.
- [x] Aucune preuve réseau SteamCMD n'est revendiquée.
- [x] Les gates unitaires/intégration restent valides conformément au plan.
- [x] Aucun téléchargement automatique n'a été tenté.

Le smoke réel d'une branche beta n'est pas revendiqué sur cette machine ; le comportement de branche reste couvert par fixtures/tests.

## 6. Performance 0.2

Baseline `win-x64`, framework-dependent, premier lancement cold-ish best-effort puis trois runs warm :

```text
Publish files                  : 46
Publish size                   : 3,90 Mio
Cold-ish window ready          : 1 905 ms
Warm average                   : 532,67 ms
Warm median                    : 541 ms
Warm min / max                 : 508 / 549 ms
Cold-ish working set           : 156,80 Mio
Warm working set average       : 153,16 Mio
Cold-ish private memory        : 158,30 Mio
Warm private memory average    : 157,74 Mio
```

Comparaison historique 0.1 :

```text
0.1 cold-ish                   : 507 ms
0.1 warm average               : 452 ms
0.2 cold-ish delta             : +1 398 ms / +275,74 %
0.2 warm average delta         : +80,67 ms / +17,85 %
```

La régression apparente a été investiguée avant fermeture :

```text
First diagnostic process:
HOST_BUILD_MS                  : 949
PIPELINE_INITIALIZE_MS         : 184
HARNESS_TOTAL_MS               : 1 165

Following fresh processes:
HOST_BUILD_MS                  : 82–86
PIPELINE_INITIALIZE_MS         : 58–59
HARNESS_TOTAL_MS               : 172–178

Breakdown:
DB_INITIALIZE_MS               : 20
DB_QUICKCHECK_MS               : 2
LIBRARY_LOAD_MS                : 14
LOCAL_EVIDENCE_LOAD_MS         : 7
REMOTE_CACHE_LOOKUP_COUNT      : 28
REMOTE_CACHE_LOOKUP_TOTAL_MS   : 3
REFERENCE_RUNTIME_LOAD_CACHED  : 11 ms
VIEWMODEL_REFRESH_MS           : 11
HARNESS_TOTAL_MS               : 181
```

- [x] La baseline 0.2 est enregistrée.
- [x] La hausse cold-ish a été investiguée.
- [x] Aucune campagne réseau Steam n'est exécutée sur le chemin critique.
- [x] Le cache Steam 0.2 n'explique pas le pic cold-ish.
- [x] Les nombres sont documentés comme baseline de développement, pas comme SLA.

## 7. Données et sécurité locale

- [x] Les données utilisateur restent sous `%LOCALAPPDATA%\PlayStead`.
- [x] Aucune donnée utilisateur n'est écrite à côté de l'exécutable.
- [x] Aucune authentification Steam n'est requise.
- [x] Aucun mot de passe Steam n'est collecté.
- [x] Aucune clé Web API Steam n'est requise.
- [x] SteamCMD n'est pas téléchargé automatiquement.

## 8. CI Windows

- [x] Workflow Windows présent.
- [x] Runner `windows-latest`.
- [x] .NET 10.
- [x] Restore + Release build avec warnings traités comme erreurs.
- [x] Release test sans rebuild.
- [x] TRX / résultats de tests conservés.
- [x] Gate `failed == 0`.
- [x] Gate `skipped == 0`.
- [ ] Le seuil minimum de tests est porté à **186** pour la baseline 0.2 (à appliquer avant le gate final).

## 9. Rapports d'acceptance Task 10

Rapports produits dans `D:\Dev\PlayStead\02_RAPPORTS` :

```text
PLAYSTEAD_02_TASK10_PREFLIGHT_AUDIT_20260912-220418.txt
PLAYSTEAD_02_TASK10_RUNTIME_SMOKE_PERF_20260912-220624.txt
PLAYSTEAD_02_STEAMCMD_SMOKE_20260912-220942.txt
PLAYSTEAD_02_PERF_BASELINE_FIX02_20260912-222458.txt
PLAYSTEAD_02_TASK10_STARTUP_REGRESSION_DIAGNOSTIC_FIX01_20260912-224321.txt
```

## 10. Gate final requis avant fermeture

La Task 10 et PlayStead 0.2 ne sont considérés CLOSED qu'après un gate frais confirmant :

- documentation 0.2 présente ;
- CI 0.2 avec seuil minimum de **186 tests** ;
- `dotnet restore PlayStead.sln` réussi ;
- build `Release /warnaserror` réussi ;
- **186/186 tests PASS**, 0 failed, 0 skipped ;
- `git diff --check` propre ;
- commit final Task 10 créé ;
- arbre Git propre après commit.

> Cette section décrit le contrat du gate final ; elle ne s'auto-certifie pas. La fermeture effective dépend du rapport final frais et du commit Git final.
