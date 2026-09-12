# PlayStead

**Version actuelle : `0.2.0-dev`**

PlayStead est une application Windows locale destinée à construire une bibliothèque de jeux fiable à partir des installations réellement présentes sur la machine, puis à fournir des informations de référence sur leur état sans rendre le démarrage dépendant du réseau.

La version 0.2 conserve la philosophie **local-first / cache-first** de la fondation 0.1 et ajoute un moteur de référence Steam fondé sur des preuves locales et distantes.

## État du projet

`0.2.0-dev` est une version de développement. La version `1.0` reste réservée à la première release publique complète.

La portée actuelle reste volontairement limitée à **Windows 10/11** et à **Steam**. PlayStead 0.2 ne remplace pas Steam, n'installe pas de mises à jour et ne télécharge pas SteamCMD automatiquement.

L'interface de 0.2 reste volontairement fonctionnelle et minimale. Le vrai travail de refonte UX/UI est prévu dans une étape ultérieure.

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

Le gate final doit reproduire ces résultats avant fermeture de la version.

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

PlayStead 0.2 n'est pas encore un launcher universel et ne gère pas l'installation, la mise à jour ou le modding des jeux. Cette version établit la référence Steam : preuves locales, preuve distante anonyme optionnelle, cache 6 h, évaluation conservatrice et exposition UI minimale.

Les fonctionnalités ultérieures seront ajoutées progressivement dans les versions `0.x`.
