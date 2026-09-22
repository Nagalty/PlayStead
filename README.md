# PlayStead

**Version actuelle : `0.4.1-dev`**

PlayStead est une application Windows locale destinée à construire une bibliothèque de jeux fiable à partir des installations réellement présentes sur la machine, puis à fournir des informations de référence sur leur état sans rendre le démarrage dépendant du réseau.

La version 0.2 conserve la philosophie **local-first / cache-first** de la fondation 0.1 et ajoute un moteur de référence Steam fondé sur des preuves locales et distantes.

La version 0.3 ajoute le suivi local des sessions à partir des processus réellement observés, avec historique, détail et corrections manuelles traçables.

La version 0.4 modernise l'expérience utilisateur et la direction visuelle sans casser les fondations 0.1–0.3 : shell horizontal, Accueil, Bibliothèque Grille/Liste, recherche locale, panneau rapide, fiche jeu, lancement Steam, À signaler, Paramètres et Sessions.

## État du projet

`0.4.1-dev` est une version de développement. La version `1.0` reste réservée à la première release publique complète.

La portée actuelle reste volontairement limitée à **Windows 10/11** et à **Steam**. PlayStead ne remplace pas Steam, n'installe pas de mises à jour et ne télécharge pas SteamCMD automatiquement.

La version 0.4 applique la refonte visuelle globale validée en conservant les moteurs locaux, Steam et Sessions existants.

## Fondation média 0.4.1

- pipeline média **Steam-first** et provider-neutral pour les assets **Cover, Header, Hero et Logo** ;
- résolution **cache-first** : cache PlayStead, médias Steam locaux, puis CDN Steam ; le démarrage et le premier rendu de la Bibliothèque ne dépendent pas du réseau ;
- en mode hors ligne, les assets en cache restent disponibles et les autres jeux conservent leur fallback PlayStead ;
- l'architecture est prête pour IGDB, mais **IGDB reste désactivé** en 0.4.1 ; aucun secret IGDB n'est embarqué (`IGDB_SECRET_EMBEDDED=False`) ;
- les gates runtime et visuels font partie intégrante de l'acceptation, au même titre que le code, les tests et la cohérence produit.

## Fonctionnalités validées

### Fondation locale héritée de 0.1

- détection locale des bibliothèques et manifests Steam ;
- modèle provider-neutral pour préparer les futures sources sans les simuler ;
- persistance de la bibliothèque dans **SQLite** ;
- démarrage **cache-first**, puis rafraîchissement local ;
- comportement **single-instance** avec transfert d'invocation vers l'instance principale ;
- activation de la fenêtre principale lors d'une seconde invocation ;
- sauvegarde et restauration de la position, de la taille et de l'état maximisé de la fenêtre ;
- données utilisateur stockées sous `%LOCALAPPDATA%\PlayStead`, jamais à côté de l'exécutable.

### Référence Steam ajoutée en 0.2

- collecte locale de la branche Steam et des manifests de dépôts ;
- interrogation distante via **SteamCMD en mode anonyme**, sans compte Steam, mot de passe ni clé Web API ;
- cache persistant séparé des preuves locales ;
- durée de fraîcheur du cache distant fixée à **6 heures** ;
- démarrage à partir du cache sans attendre une campagne distante ;
- rafraîchissement automatique des données périmées en arrière-plan ;
- commande globale **« Vérifier Steam »** pour forcer une vérification ;
- une seule campagne de rafraîchissement distante à la fois ;
- conservation de la dernière preuve distante valide en cas d'échec ;
- état conservateur `Unknown` lorsque les preuves sont insuffisantes.

Les états exposés sont :

- `UpToDate` — installation considérée à jour lorsque les manifests de dépôts correspondent ;
- `UpdateAvailable` — divergence de manifest démontrée ;
- `NewVersionDetected` — changement distant détecté sans preuve suffisante d'une mise à jour installable ;
- `Unknown` — données insuffisantes ou branche distante indisponible ;
- `Checking` — état UI temporaire pendant une vérification manuelle.

Le **BuildID est un signal secondaire**. Il ne suffit jamais, à lui seul, à déclarer `UpdateAvailable`.

### Sessions locales ajoutées en 0.3

- processus observés comme **source de vérité**, identifiés par des signatures de processus ;
- polling toutes les **2 s**, confirmation après **2 snapshots** et heartbeat persisté toutes les **5 s** ;
- récupération après crash ou reboot à partir des dernières observations persistées, **sans temps inventé** ;
- fonctionnement en **systray** ;
- corrections manuelles traçables, séparées des observations d'origine ;
- séparation du **temps local PlayStead** et du **temps provider**, sans assimilation des deux mesures ;
- page **Sessions**, historique récent et détail des temps observés/effectifs ;
- badge live dans la **Bibliothèque** et composants UI réutilisables.

## Expérience utilisateur ajoutée en 0.4

- shell WPF horizontal sans sidebar, avec navigation principale **Accueil / Bibliothèque / À signaler / Paramètres** ;
- **Accueil** recentré sur les données locales réellement disponibles ;
- **Bibliothèque** disponible en modes **Grille** et **Liste**, avec état de vue restaurable ;
- recherche dynamique locale sur le **début du titre affiché** du jeu, insensible à la casse ;
- placeholder **« Rechercher un jeu »** et raccourci `Ctrl+K` pour focaliser la recherche ;
- panneau rapide de jeu et fiche détaillée avec retour vers le contexte de Bibliothèque ;
- bouton **Jouer** fondé sur les installations réellement lançables ; lancement Steam via URI `steam://rungameid/...` ;
- page **À signaler** limitée aux décisions/vérifications réellement déductibles des données existantes ;
- paramètres d'animation avec **réduction des mouvements** ;
- modernisation visuelle de **Sessions** sans modifier son moteur 0.3 ;
- retours d'erreur locaux et progressifs, sans spinner plein écran imposé.

Le **media preview n'est pas implémenté dans 0.4** (`MEDIA_PREVIEW_IMPLEMENTED=False`). Cette absence est volontaire : aucune source média suffisamment fiable n'a été introduite artificiellement pour remplir l'interface.

## SteamCMD

PlayStead 0.2 sait utiliser SteamCMD lorsqu'il est déjà disponible. Il ne l'installe et ne le télécharge pas automatiquement.

Ordre de résolution prévu :

1. chemin explicitement configuré par le runtime ;
2. `%LOCALAPPDATA%\PlayStead\Tools\SteamCMD\steamcmd.exe` ;
3. `steamcmd.exe` disponible dans `PATH`.

Si SteamCMD est absent ou échoue, PlayStead continue de fonctionner avec sa bibliothèque locale. Une preuve distante déjà valide reste exploitable ; sans preuve suffisante, l'état reste `Unknown`.

## Stack technique

- C# / **.NET 10**
- **WPF**
- SQLite
- Microsoft Generic Host / DI
- xUnit
- PowerShell 7 pour les gates, audits et smokes
- GitHub Actions pour la CI Windows

## Architecture

La solution est découpée en cinq projets de production :

- `PlayStead.Core` — domaine, contrats, preuves Steam et évaluation pure de l'état ;
- `PlayStead.Data` — SQLite, migrations, health check et persistance ;
- `PlayStead.Platform` — chemins Windows et instance unique ;
- `PlayStead.Providers` — découverte locale Steam et source distante SteamCMD ;
- `PlayStead.UI` — WPF, composition root, runtime cache-first et présentation.

Les tests sont regroupés dans cinq projets miroirs.

## Données utilisateur

PlayStead utilise le profil local Windows :

```text
%LOCALAPPDATA%\PlayStead
```

Exemples de données persistantes :

```text
%LOCALAPPDATA%\PlayStead\Data\playstead.db
%LOCALAPPDATA%\PlayStead\Data\window-placement.json
```

Aucune base SQLite ni préférence de fenêtre ne doit être créée dans le répertoire de l'application.

## Build et tests

Depuis la racine du repo :

```powershell
dotnet restore PlayStead.sln
dotnet build PlayStead.sln --configuration Release --no-restore /warnaserror
dotnet test PlayStead.sln --configuration Release --no-build
```

Baseline automatisée observée avant le gate final 0.2 :

```text
186/186 tests PASS
0 test failed
0 test skipped
Release build: 0 error / 0 warning
```

Cette baseline 0.2 est conservée comme référence historique.

Baseline 0.3 historique validée : **349/349 tests PASS**, build Release à **0 warning / 0 error**.

Baseline 0.4 pré-finale fraîche : **507/507 tests PASS**, **0 échec**, **0 skipped/not executed**. Le Launch smoke utilise un `IExternalUriLauncher` factice et confirme l'URI Steam sans ouvrir réellement Steam ni un jeu.

Baseline performance 0.4 enregistrée sans SLA inventé :

```text
Warm window readiness            : 645 ms
Idle shell                       : ~173 Mio working set / ~131 Mio private
Bibliothèque Grille              : ~235 Mio working set / ~191 Mio private
Sessions                         : ~240 Mio working set / ~195 Mio private
Recherche 5000 jeux (p95)        : A 1,775 ms / AR 1,818 ms / ARM 2,156 ms
Projection grille 5000 jeux p95  : 0,043 ms
```

La Task 12 0.4 est finalisée : gate final PASS, commit final créé et post-vérification PASS. Voir la [checklist d'acceptation 0.4](docs/PLAYSTEAD_0.4_ACCEPTANCE_CHECKLIST.md). Les checklists [0.1](docs/PLAYSTEAD_0.1_ACCEPTANCE_CHECKLIST.md), [0.2](docs/PLAYSTEAD_0.2_ACCEPTANCE_CHECKLIST.md) et [0.3](docs/PLAYSTEAD_0.3_ACCEPTANCE_CHECKLIST.md) restent des références historiques.

## Validation runtime 0.2

La machine de validation contient **28 installations Steam**.

Le smoke Windows réel a confirmé :

- ouverture de la fenêtre principale ;
- bibliothèque locale disponible sans SteamCMD ;
- commande **« Vérifier Steam »** fonctionnelle et réactivée après la campagne ;
- absence de téléchargement ou de création automatique de SteamCMD ;
- stabilité du processus ;
- fermeture propre.

SteamCMD n'était pas installé sur la machine de validation. Le smoke réseau public SteamCMD est donc enregistré comme **prérequis manquant** et aucune preuve réseau réelle n'est revendiquée.

Baseline performance 0.2 enregistrée :

```text
Publish win-x64 framework-dependent : 3,90 Mio / 46 fichiers
Cold-ish best-effort             : 1 905 ms
Warm moyen                       : 532,67 ms
Warm min / médiane / max         : 508 / 541 / 549 ms
Working set warm moyen           : 153,16 Mio
```

L'investigation du cold-ish élevé a confirmé que la campagne distante n'est pas sur le chemin critique. Sur des processus suivants, le chemin host + cache + bibliothèque tombe à environ **172–181 ms** ; le chargement du cache Steam lui-même mesure **11 ms**.

Ces nombres constituent une **baseline de développement**, pas une SLA.

## Portée actuelle

PlayStead 0.4 n'est pas encore un launcher universel et ne gère pas l'installation, la mise à jour ou le modding des jeux. Cette version conserve la référence Steam de 0.2 et le suivi local des sessions de 0.3, tout en livrant la refonte UX/visuelle 0.4.

Les fonctionnalités ultérieures seront ajoutées progressivement dans les versions `0.x`.

